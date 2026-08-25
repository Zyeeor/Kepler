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

// ValidationError 用户 BD 上传的数据校验错误（前台应提示"数据有误，重新构筑"）。
type ValidationError struct{ Msg string }

func (e *ValidationError) Error() string { return e.Msg }

// validateUserBD 严格校验工具上传的构筑数据（与导入路径的宽松跳过不同：上传任一条目非法即拒绝）。
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

// UploadUserBD 工具在线上传构筑：严格校验 → 通过则保存文件到 userBD/{playerId}/
// 并按内容指纹去重入库（实时进入投放候选池）；返回入库/去重条数。
// 数据问题返回 *ValidationError（前台提示重新构筑）；重复内容不算错误（已在池中）。
func (s *EliteService) UploadUserBD(dir string, req *UploadSnapshotsRequest) (stored, dups int, err error) {
	logx.Event("userBD upload · player=%s run=%s · %d snapshots", req.PlayerID, req.RunID, len(req.Snapshots))
	if err := validateUserBD(req); err != nil {
		logx.Event("userBD upload rejected · player=%s run=%s → %v", req.PlayerID, req.RunID, err)
		return 0, 0, err
	}

	// 保存文件（持久源，重启时重放）：userBD/{playerId}/bd-{runId}.json
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
		if err := os.WriteFile(fileName, data, 0o644); err != nil {
			return 0, 0, fmt.Errorf("write file: %w", err)
		}
		logx.Detail("saved %s", fileName)
	}

	// 内容指纹去重入库（实时生效：入库即可被 pick 命中）
	seen, err := s.loadContentFingerprints()
	if err != nil {
		return 0, 0, fmt.Errorf("load fingerprints: %w", err)
	}
	snaps := make([]*BuildSnapshot, 0, len(req.Snapshots))
	for _, in := range req.Snapshots {
		fp := contentFingerprint(in.Sin, in.BDData)
		if _, ok := seen[fp]; ok {
			logx.Detail("dup upload · sin=%s monsterType=%s bdCount=%d cards=[%s] → already in pool, skipped",
				in.Sin, in.MonsterType, in.BDCount, cardIDList(in.BDData))
			dups++
			continue
		}
		seen[fp] = struct{}{}
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
			return stored, dups, fmt.Errorf("upsert: %w", err)
		}
		stored = len(snaps)
		s.enforceCapacity(req.PlayerID)
		s.invalidateLeaderboard() // 快照入库 / 淘汰改变榜单 JOIN 内容 → 失效缓存
		for _, snap := range snaps {
			logx.Detail("stored upload · sin=%s monsterType=%s bdCount=%d sourceWave=%d cards=[%s] (player=%s run=%s)",
				snap.Sin, snap.MonsterType, snap.BDCount, snap.SourceWave, cardIDList(json.RawMessage(snap.BDData)), snap.PlayerID, snap.RunID)
		}
	}
	logx.Event("userBD upload done · player=%s run=%s → stored=%d duplicates=%d", req.PlayerID, req.RunID, stored, dups)
	return stored, dups, nil
}

// sanitizeFileName 清洗文件/目录名（playerId/runId 来自客户端，防路径注入）：
// 仅保留字母数字、下划线、连字符，其余替换为 _；空结果回退 unknown。
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

// ImportUserBD 扫描用户 BD 目录（MonsterBuildEditor 上传/导出的构筑 JSON），逐文件导入候选库。
// 每次启动都执行，不要求空库。遍历范围：顶层 *.json 与一级子目录 {playerId}/*.json
// （在线上传按玩家分目录保存；顶层散文件为手工放置的历史格式，继续兼容）。
//
// 去重保证（内容级）：BD 内容指纹 = sin + 排序后的 cardId 集合（对装配顺序不敏感）。
// 启动时加载全库指纹集合，与本次已导入指纹合并去重——同一文件重启重复导入、
// 或同一构筑存成多个文件（工具随机 playerId/runId 不同）均只入库一份；
// 内容不同的构筑正常入库。字段校验与容量治理与 Upload 同规则（宽松：非法条目跳过并记日志）。
func (s *EliteService) ImportUserBD(dir string) error {
	if dir == "" {
		return nil
	}
	if _, err := os.Stat(dir); os.IsNotExist(err) {
		// 目录不存在时自动创建，给出明确的文件投放路径；失败则静默跳过
		if err := os.MkdirAll(dir, 0o755); err != nil {
			logx.Event("userBD import skipped · cannot create dir %s: %v", dir, err)
			return nil
		}
	}
	entries, err := os.ReadDir(dir)
	if err != nil {
		return fmt.Errorf("userBD read dir: %w", err)
	}

	// 全库内容指纹集合（受全局容量上限约束，启动一次性加载开销可控）
	seen, err := s.loadContentFingerprints()
	if err != nil {
		return fmt.Errorf("userBD load fingerprints: %w", err)
	}

	stored, dups, invalid, files := 0, 0, 0, 0
	importFile := func(path string) {
		n, d, inv, err := s.importUserBDFile(path, seen)
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
			// 一级子目录（在线上传按玩家分目录）：扫描其中 *.json，文件内 playerId/runId 为准
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

// importUserBDFile 解析单个工具导出文件，内容指纹去重后入库；返回入库/去重/非法条数。
// 每条快照均输出明细日志：stored 入库 / dup 内容重复跳过 / skip 字段非法跳过。
func (s *EliteService) importUserBDFile(path string, seen map[string]struct{}) (stored, dups, invalid int, err error) {
	fileName := filepath.Base(path)
	data, err := os.ReadFile(path)
	if err != nil {
		return 0, 0, 0, err
	}
	// 剥离 UTF-8 BOM（Windows 工具保存的 JSON 可能带 BOM，Go json 不接受）
	data = bytes.TrimPrefix(data, []byte{0xEF, 0xBB, 0xBF})
	var f userBDFile
	if err := json.Unmarshal(data, &f); err != nil {
		return 0, 0, 0, fmt.Errorf("parse JSON: %w", err)
	}

	// seeds 包装格式展开为多条；裸对象格式取顶级字段
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
		snaps := make([]*BuildSnapshot, 0, len(en.snaps))
		for i, in := range en.snaps {
			// 字段校验（与 Upload 同规则：sin/monsterType 非空、bdCount>=1、bdData 非 null）
			if in.Sin == "" || in.MonsterType == "" || in.BDCount < 1 || len(in.BDData) == 0 || string(in.BDData) == "null" {
				logx.Detail("skip %s entry[%d] · invalid fields: sin=%q monsterType=%q bdCount=%d bdData=%s → dropped",
					fileName, i, in.Sin, in.MonsterType, in.BDCount, bdDataSummary(in.BDData))
				invalid++
				continue
			}
			fp := contentFingerprint(in.Sin, in.BDData)
			if _, ok := seen[fp]; ok {
				logx.Detail("dup %s · sin=%s monsterType=%s bdCount=%d cards=[%s] → already in pool, skipped",
					fileName, in.Sin, in.MonsterType, in.BDCount, cardIDList(in.BDData))
				dups++
				continue
			}
			seen[fp] = struct{}{}
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

// loadContentFingerprints 加载全库 BD 内容指纹集合（userBD 导入去重用）。
func (s *EliteService) loadContentFingerprints() (map[string]struct{}, error) {
	snaps, err := s.store.ListAllSnapshots()
	if err != nil {
		return nil, err
	}
	seen := make(map[string]struct{}, len(snaps))
	for _, snap := range snaps {
		seen[contentFingerprint(snap.Sin, json.RawMessage(snap.BDData))] = struct{}{}
	}
	return seen, nil
}

// contentFingerprint BD 内容指纹：sin + 排序后的 cardId 集合（对装配顺序不敏感）。
// bdData 解析失败或为空时退化为 sin + 原文（此类条目会被字段校验拒绝，不会入库）。
func contentFingerprint(sin string, bdData json.RawMessage) string {
	return sin + "|" + cardIDList(bdData)
}

// cardIDList 从 bdData 提取排序后的 cardId 逗号列表（日志展示与内容指纹共用）。
// 解析失败或为空时返回 bdData 原文（截断到 60 字符防止日志刷屏）。
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

// bdDataSummary bdData 概要（非法条目的 skip 明细日志用，截断防刷屏）。
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

// userBDFile 工具导出文件的两种结构合体解析（裸对象 / seeds 包装）。
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
