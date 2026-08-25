// 用户 BD：MonsterBuildEditor 工具在线上传（严格校验）+ 启动目录导入（宽松跳过）+ 内容指纹去重。
package elite

import (
	"bytes"
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"sort"
	"strings"

	"demo/server/tools/logx"
)

// ValidationError 用户 BD 上传的校验错误（前台应提示"数据有误，重新构筑"）。
type ValidationError struct{ Msg string }

func (e *ValidationError) Error() string { return e.Msg }

// validateUserBD 严格校验（任一条目非法即拒绝；导入路径为宽松跳过）。
func validateUserBD(req *UploadSnapshotsRequest) error {
	if req.PlayerID == "" || req.RunID == "" {
		return &ValidationError{Msg: "missing playerId or runId"}
	}
	if len(req.Snapshots) == 0 {
		return &ValidationError{Msg: "no snapshots (equip at least 1 exclusive card)"}
	}
	for i, in := range req.Snapshots {
		no := fmt.Sprintf("snapshot[%d]", i+1)
		if in.Sin == "" {
			return &ValidationError{Msg: no + ": missing sin"}
		}
		if !validSins[in.Sin] {
			return &ValidationError{Msg: no + ": invalid sin: " + in.Sin}
		}
		if in.MonsterType == "" {
			return &ValidationError{Msg: no + ": missing monsterType"}
		}
		if in.BDCount < 1 {
			return &ValidationError{Msg: no + ": bdCount must be >= 1"}
		}
		if len(in.BDData) == 0 || string(in.BDData) == "null" {
			return &ValidationError{Msg: no + ": bdData is empty"}
		}
		var cards []struct {
			CardID string `json:"cardId"`
		}
		if err := json.Unmarshal(in.BDData, &cards); err != nil || len(cards) == 0 {
			return &ValidationError{Msg: no + ": bdData is not a valid card array"}
		}
		if len(cards) != in.BDCount {
			return &ValidationError{Msg: fmt.Sprintf("%s: bdCount=%d does not match actual card count=%d", no, in.BDCount, len(cards))}
		}
		for j, c := range cards {
			if c.CardID == "" {
				return &ValidationError{Msg: fmt.Sprintf("%s: card[%d] missing cardId", no, j+1)}
			}
		}
	}
	return nil
}

// UploadUserBD 工具在线上传：严格校验 → 原子保存文件 → 指纹去重入库（实时进入投放候选池）。
// 返回入库/去重条数；数据问题返回 *ValidationError，重复内容不算错误。
func (s *EliteService) UploadUserBD(dir string, req *UploadSnapshotsRequest) (stored, dups int, err error) {
	logx.Event("userBD upload · player=%s run=%s · %d snapshots", req.PlayerID, req.RunID, len(req.Snapshots))
	if err := validateUserBD(req); err != nil {
		logx.Event("userBD upload rejected · player=%s run=%s → %v", req.PlayerID, req.RunID, err)
		return 0, 0, err
	}

	// 保存文件（持久源，重启时重放）：userBD/{playerId}/bd-{runId}.json；tmp+rename 原子写。
	if dir != "" {
		pdir := filepath.Join(dir, sanitizeFileName(req.PlayerID))
		if err := os.MkdirAll(pdir, 0o755); err != nil {
			return 0, 0, fmt.Errorf("create player dir: %w", err)
		}
		data, err := json.MarshalIndent(req, "", "  ")
		if err != nil {
			return 0, 0, fmt.Errorf("encode JSON: %w", err)
		}
		fileName := filepath.Join(pdir, "bd-"+sanitizeFileName(req.RunID)+".json")
		tmpName := fileName + ".tmp"
		if err := os.WriteFile(tmpName, data, 0o644); err != nil {
			return 0, 0, fmt.Errorf("write tmp file: %w", err)
		}
		if err := os.Rename(tmpName, fileName); err != nil {
			_ = os.Remove(tmpName)
			return 0, 0, fmt.Errorf("rename file: %w", err)
		}
		logx.Detail("saved %s", fileName)
	}

	// 指纹去重入库（缓存 O(1) 查重；入库失败回滚占用）
	if err := s.initFingerprints(); err != nil {
		return 0, 0, fmt.Errorf("load fingerprints: %w", err)
	}
	var claimed []string
	snaps := make([]*BuildSnapshot, 0, len(req.Snapshots))
	for _, in := range req.Snapshots {
		fp := contentFingerprint(in.Sin, in.BDData)
		if s.claimFingerprint(fp) {
			logx.Detail("dup upload · sin=%s monsterType=%s bdCount=%d cards=[%s] → already in pool, skipped",
				in.Sin, in.MonsterType, in.BDCount, cardIDList(in.BDData))
			dups++
			continue
		}
		claimed = append(claimed, fp)
		snaps = append(snaps, &BuildSnapshot{
			PlayerID:    req.PlayerID,
			RunID:       req.RunID,
			Sin:         in.Sin,
			MonsterType: in.MonsterType,
			BDData:      string(in.BDData),
			BDCount:     in.BDCount,
			SourceWave:  in.SourceWave,
			GameTime:    in.GameTime,
			Stats:       string(in.Stats),
		})
	}
	if len(snaps) > 0 {
		if err := s.store.UpsertSnapshots(snaps); err != nil {
			s.releaseFingerprints(claimed)
			return stored, dups, fmt.Errorf("upsert: %w", err)
		}
		stored = len(snaps)
		s.enforceCapacity(req.PlayerID)
		s.invalidateLeaderboard()
		for _, snap := range snaps {
			logx.Detail("stored upload · sin=%s monsterType=%s bdCount=%d sourceWave=%d cards=[%s] (player=%s run=%s)",
				snap.Sin, snap.MonsterType, snap.BDCount, snap.SourceWave, cardIDList(json.RawMessage(snap.BDData)), snap.PlayerID, snap.RunID)
		}
	}
	logx.Event("userBD upload done · player=%s run=%s → stored=%d duplicates=%d", req.PlayerID, req.RunID, stored, dups)
	return stored, dups, nil
}

// sanitizeFileName 清洗文件/目录名（防路径注入）：仅保留字母数字、下划线、连字符；空结果回退 unknown。
func sanitizeFileName(s string) string {
	var b strings.Builder
	for _, r := range s {
		if (r >= 'a' && r <= 'z') || (r >= 'A' && r <= 'Z') || (r >= '0' && r <= '9') || r == '-' || r == '_' {
			b.WriteRune(r)
		} else {
			b.WriteRune('_')
		}
	}
	if b.Len() == 0 {
		return "unknown"
	}
	return b.String()
}

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

// ── 指纹缓存（性能）：首次全库加载常驻内存，入库点增量维护，查重 O(1) ──

// initFingerprints 首次调用时全库加载内容指纹缓存（幂等）。
func (s *EliteService) initFingerprints() error {
	s.fpMu.Lock()
	defer s.fpMu.Unlock()
	if s.fpReady {
		return nil
	}
	snaps, err := s.store.ListAllSnapshots()
	if err != nil {
		return err
	}
	s.fpSeen = make(map[string]struct{}, len(snaps))
	for _, snap := range snaps {
		s.fpSeen[contentFingerprint(snap.Sin, json.RawMessage(snap.BDData))] = struct{}{}
	}
	s.fpReady = true
	return nil
}

// claimFingerprint 原子检查并占用指纹：已存在返回 true（重复）。
func (s *EliteService) claimFingerprint(fp string) bool {
	s.fpMu.Lock()
	defer s.fpMu.Unlock()
	if _, ok := s.fpSeen[fp]; ok {
		return true
	}
	s.fpSeen[fp] = struct{}{}
	return false
}

// releaseFingerprints 入库失败时释放本次占用的指纹（避免后续误判重复）。
func (s *EliteService) releaseFingerprints(fps []string) {
	s.fpMu.Lock()
	defer s.fpMu.Unlock()
	for _, fp := range fps {
		delete(s.fpSeen, fp)
	}
}

// trackFingerprints 入库成功后并入指纹缓存（客户端 Upload 等非 userBD 入库点维护缓存一致性）。
func (s *EliteService) trackFingerprints(snaps []*BuildSnapshot) {
	if len(snaps) == 0 {
		return
	}
	s.fpMu.Lock()
	defer s.fpMu.Unlock()
	if s.fpSeen == nil {
		return // 缓存未初始化：首次 initFingerprints 全库加载时会补上
	}
	for _, snap := range snaps {
		s.fpSeen[contentFingerprint(snap.Sin, json.RawMessage(snap.BDData))] = struct{}{}
	}
}

// contentFingerprint BD 内容指纹：sin + 排序后的 cardId 集合（对装配顺序不敏感）。
func contentFingerprint(sin string, bdData json.RawMessage) string {
	return sin + "|" + cardIDList(bdData)
}

// cardIDList 提取排序后的 cardId 逗号列表（指纹与日志展示共用；解析失败返回截断原文）。
func cardIDList(bdData json.RawMessage) string {
	var cards []struct {
		CardID string `json:"cardId"`
	}
	if err := json.Unmarshal(bdData, &cards); err != nil || len(cards) == 0 {
		s := string(bdData)
		if len(s) > 60 {
			s = s[:60] + "..."
		}
		return s
	}
	ids := make([]string, 0, len(cards))
	for _, c := range cards {
		ids = append(ids, c.CardID)
	}
	sort.Strings(ids)
	return strings.Join(ids, ",")
}

// bdDataSummary bdData 概要（skip 日志用，截断防刷屏）。
func bdDataSummary(bdData json.RawMessage) string {
	s := string(bdData)
	if len(s) > 60 {
		s = s[:60] + "..."
	}
	if s == "" {
		s = "<empty>"
	}
	return s
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
