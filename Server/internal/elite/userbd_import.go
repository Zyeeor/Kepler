// 用户 BD 启动目录导入：每次启动扫描 userBD 目录（MonsterBuildEditor 工具导出 JSON），
// 内容指纹去重后导入候选库。字段校验宽松（非法条目跳过记日志），与在线上传的严格校验
// 相区分。
package elite

import (
	"bytes"
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"strings"

	"possession/server/internal/logx"
)

// ImportUserBD 启动时扫描用户 BD 目录（顶层 *.json + 一级子目录 {playerId}/*.json），
// 内容指纹去重后导入候选库——同一构筑重复导入/多文件存放只入库一份；
// 字段校验与容量治理同 Upload 规则（宽松：非法条目跳过记日志）。
func (s *EliteService) ImportUserBD(dir string) error {
	if dir == "" {
		return nil
	}
	if _, err := os.Stat(dir); os.IsNotExist(err) {
		if err := os.MkdirAll(dir, 0o755); err != nil {
			logx.Event("userBD import skipped · cannot create dir %s: %v", dir, err)
			return nil
		}
	}
	entries, err := os.ReadDir(dir)
	if err != nil {
		return fmt.Errorf("userBD read dir: %w", err)
	}

	if err := s.initFingerprints(); err != nil {
		return fmt.Errorf("userBD load fingerprints: %w", err)
	}

	stored, dups, invalid, files := 0, 0, 0, 0
	importFile := func(path string) {
		n, d, inv, err := s.importUserBDFile(path)
		if err != nil {
			logx.Event("userBD import failed · file %s: %v (non-fatal)", filepath.Base(path), err)
			return
		}
		stored += n
		dups += d
		invalid += inv
	}

	for _, e := range entries {
		if e.IsDir() {
			sub, err := os.ReadDir(filepath.Join(dir, e.Name()))
			if err != nil {
				logx.Event("userBD import failed · subdir %s read: %v (non-fatal)", e.Name(), err)
				continue
			}
			for _, se := range sub {
				if se.IsDir() || !strings.EqualFold(filepath.Ext(se.Name()), ".json") {
					continue
				}
				files++
				importFile(filepath.Join(dir, e.Name(), se.Name()))
			}
			continue
		}
		if !strings.EqualFold(filepath.Ext(e.Name()), ".json") {
			continue
		}
		files++
		importFile(filepath.Join(dir, e.Name()))
	}
	logx.Event("userBD import done · stored=%d duplicates=%d invalid=%d · %d json file(s) in %s",
		stored, dups, invalid, files, dir)
	return nil
}

// importUserBDFile 解析单个工具导出文件（seeds 包装 / 裸对象两种格式），指纹去重后入库。
func (s *EliteService) importUserBDFile(path string) (stored, dups, invalid int, err error) {
	fileName := filepath.Base(path)
	data, err := os.ReadFile(path)
	if err != nil {
		return 0, 0, 0, err
	}
	data = bytes.TrimPrefix(data, []byte{0xEF, 0xBB, 0xBF}) // 剥离 UTF-8 BOM（Go json 不接受）
	var f userBDFile
	if err := json.Unmarshal(data, &f); err != nil {
		return 0, 0, 0, fmt.Errorf("parse JSON: %w", err)
	}

	type fileEntry struct {
		playerID string
		runID    string
		snaps    []SnapshotInput
	}
	var list []fileEntry
	if len(f.Seeds) > 0 {
		for _, seed := range f.Seeds {
			list = append(list, fileEntry{seed.PlayerID, seed.RunID, seed.Snapshots})
		}
	} else if len(f.Snapshots) > 0 {
		list = append(list, fileEntry{f.PlayerID, f.RunID, f.Snapshots})
	} else {
		return 0, 0, 0, fmt.Errorf("no snapshots found (neither top-level \"snapshots\" nor \"seeds\")")
	}

	for _, en := range list {
		if en.playerID == "" || en.runID == "" {
			logx.Detail("skip %s · playerId/runId missing (playerId=%q runId=%q) → entry dropped", fileName, en.playerID, en.runID)
			invalid += len(en.snaps)
			continue
		}
		var claimed []string
		snaps := make([]*BuildSnapshot, 0, len(en.snaps))
		for i, in := range en.snaps {
			if in.Sin == "" || in.MonsterType == "" || in.BDCount < 1 || len(in.BDData) == 0 || string(in.BDData) == "null" {
				logx.Detail("skip %s entry[%d] · invalid fields: sin=%q monsterType=%q bdCount=%d bdData=%s → dropped",
					fileName, i, in.Sin, in.MonsterType, in.BDCount, bdDataSummary(in.BDData))
				invalid++
				continue
			}
			fp := contentFingerprint(in.Sin, in.BDData)
			if s.claimFingerprint(fp) {
				logx.Detail("dup %s · sin=%s monsterType=%s bdCount=%d cards=[%s] → already in pool, skipped",
					fileName, in.Sin, in.MonsterType, in.BDCount, cardIDList(in.BDData))
				dups++
				continue
			}
			claimed = append(claimed, fp)
			snaps = append(snaps, &BuildSnapshot{
				PlayerID:    en.playerID,
				RunID:       en.runID,
				Sin:         in.Sin,
				MonsterType: in.MonsterType,
				BDData:      string(in.BDData),
				BDCount:     in.BDCount,
				SourceWave:  in.SourceWave,
				GameTime:    in.GameTime,
				Stats:       string(in.Stats),
			})
		}
		if len(snaps) == 0 {
			continue
		}
		if err := s.store.UpsertSnapshots(snaps); err != nil {
			s.releaseFingerprints(claimed)
			return stored, dups, invalid, fmt.Errorf("upsert: %w", err)
		}
		stored += len(snaps)
		s.enforceCapacity(en.playerID)
		for _, snap := range snaps {
			logx.Detail("stored %s · sin=%s monsterType=%s bdCount=%d sourceWave=%d cards=[%s] (player=%s run=%s)",
				fileName, snap.Sin, snap.MonsterType, snap.BDCount, snap.SourceWave, cardIDList(json.RawMessage(snap.BDData)), snap.PlayerID, snap.RunID)
		}
	}
	return stored, dups, invalid, nil
}

// userBDFile 工具导出文件格式（裸对象 / seeds 包装合体解析）。
type userBDFile struct {
	PlayerID  string          `json:"playerId"`
	RunID     string          `json:"runId"`
	Snapshots []SnapshotInput `json:"snapshots"`
	Seeds     []struct {
		PlayerID  string          `json:"playerId"`
		RunID     string          `json:"runId"`
		Snapshots []SnapshotInput `json:"snapshots"`
	} `json:"seeds"`
}
