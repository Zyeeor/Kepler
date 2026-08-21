// Package service — 精英怪 BD 快照服务：上传（滚动 upsert）与筛选投放。
//
// 设计依据：《精英怪筛选 — 他人 BD 怪物投放 · 策划案》§3–§6/§8。
// 筛选只依赖 bdCount 与 sourceWave；透传原则见 store.BuildSnapshot。
package service

import (
	"bytes"
	"encoding/json"
	"fmt"
	"log"
	"math"
	"math/rand"
	"os"
	"path/filepath"
	"sort"
	"strings"

	"demo/server/internal/store"
)

// EliteConfig 精英怪投放可调参数（策划案 §6 TUNABLE + §8.2/§8.4 容量治理）。
type EliteConfig struct {
	MinBD                 int     // MIN_BD：最低 BD 数量门槛（Step 1）
	WaveGap               int     // WAVE_GAP：波次差（Step 2）
	TopBandMode           string  // TOP_BAND 模式："percent"（前 X%）| "topk"（前 K 条），双模式可切换（§8.4）
	TopBandPercent        float64 // percent 模式：高分档比例（如 0.2 = 前 20%）
	TopBandTopK           int     // topk 模式：高分档条数
	MaxSnapshots          int     // 候选库全局上限（FIFO 淘汰最早快照，§8.2）
	MaxSnapshotsPerPlayer int     // 每玩家快照上限（§8.4，与全局 FIFO 并行生效）
}

// DefaultEliteConfig 默认参数（首版 Baseline，需 Playable 验证后调整，不代表数值冻结）。
func DefaultEliteConfig() EliteConfig {
	return EliteConfig{
		MinBD:                 1,
		WaveGap:               0, // 服务端不叠加难度，waveGap 完全由客户端指定
		TopBandMode:           "percent",
		TopBandPercent:        0.2,
		TopBandTopK:           5,
		MaxSnapshots:          10000,
		MaxSnapshotsPerPlayer: 100,
	}
}

// normalize 钳制非法参数到安全范围。
func (c EliteConfig) normalize() EliteConfig {
	if c.MinBD < 1 {
		c.MinBD = 1 // Step 1「bdCount=0 直接丢弃」是核心承诺，下限为 1
	}
	if c.WaveGap < 0 {
		c.WaveGap = 0
	}
	if c.TopBandMode != "topk" {
		c.TopBandMode = "percent"
	}
	if c.TopBandPercent <= 0 || c.TopBandPercent > 1 {
		c.TopBandPercent = 0.2
	}
	if c.TopBandTopK < 1 {
		c.TopBandTopK = 1
	}
	if c.MaxSnapshots < 1 {
		c.MaxSnapshots = 10000
	}
	if c.MaxSnapshotsPerPlayer < 1 {
		c.MaxSnapshotsPerPlayer = 100
	}
	return c
}

// EliteService 精英怪 BD 快照服务。
type EliteService struct {
	store store.EliteStore
	cfg   EliteConfig
}

// NewEliteService 创建服务（配置在构造时归一化）。
func NewEliteService(st store.EliteStore, cfg EliteConfig) *EliteService {
	return &EliteService{store: st, cfg: cfg.normalize()}
}

// SeedIfEmpty 种子数据注入：候选库为空时从 JSON 文件加载预设快照，保障首位玩家也能遇到精英怪。
// 库中已有数据时跳过（幂等）；seedFile 为空或文件不存在时静默跳过。
func (s *EliteService) SeedIfEmpty(seedFile string) error {
	if seedFile == "" {
		return nil
	}
	count, err := s.store.CountSnapshots()
	if err != nil {
		return fmt.Errorf("seed check count: %w", err)
	}
	if count > 0 {
		log.Printf("[elite] seed skipped: pool already has %d snapshots", count)
		return nil
	}

	data, err := os.ReadFile(seedFile)
	if err != nil {
		if os.IsNotExist(err) {
			log.Printf("[elite] seed skipped: file not found (%s)", seedFile)
			return nil
		}
		return fmt.Errorf("seed read file: %w", err)
	}

	var seedFile_ seedFileFormat
	if err := json.Unmarshal(data, &seedFile_); err != nil {
		return fmt.Errorf("seed parse JSON: %w", err)
	}

	total := 0
	for _, entry := range seedFile_.Seeds {
		snaps := make([]*store.BuildSnapshot, 0, len(entry.Snapshots))
		for _, in := range entry.Snapshots {
			if in.Sin == "" || in.BDCount < 1 || len(in.BDData) == 0 {
				continue
			}
			bdJSON, _ := json.Marshal(in.BDData)
			snaps = append(snaps, &store.BuildSnapshot{
				PlayerID:    entry.PlayerID,
				RunID:       entry.RunID,
				Sin:         in.Sin,
				MonsterType: in.MonsterType,
				BDData:      string(bdJSON),
				BDCount:     in.BDCount,
				SourceWave:  in.SourceWave,
				GameTime:    in.GameTime,
			})
		}
		if len(snaps) == 0 {
			continue
		}
		if err := s.store.UpsertSnapshots(snaps); err != nil {
			return fmt.Errorf("seed upsert: %w", err)
		}
		total += len(snaps)
	}

	log.Printf("[elite] seeded %d snapshots from %s (pool was empty)", total, seedFile)
	return nil
}

// ============================================================================
// 用户 BD：工具上传（在线）+ 目录导入（启动）
// ============================================================================

// ValidationError 用户 BD 上传的数据校验错误（前台应提示"数据有误，重新构筑"）。
type ValidationError struct{ Msg string }

func (e *ValidationError) Error() string { return e.Msg }

// validSins 合法七宗罪 wire 名（客户端 EliteMonsterCatalog.WireName 同源）。
var validSins = map[string]bool{
	"pride": true, "sloth": true, "gluttony": true, "envy": true,
	"wrath": true, "greed": true, "lust": true,
}

// validateUserBD 严格校验工具上传的构筑数据（与导入路径的宽松跳过不同：上传任一条目非法即拒绝）。
func validateUserBD(req *UploadSnapshotsRequest) error {
	if req.PlayerID == "" || req.RunID == "" {
		return &ValidationError{Msg: "缺少 playerId 或 runId"}
	}
	if len(req.Snapshots) == 0 {
		return &ValidationError{Msg: "没有任何快照（至少装配 1 张专属卡）"}
	}
	for i, in := range req.Snapshots {
		no := fmt.Sprintf("第%d条快照", i+1)
		if in.Sin == "" {
			return &ValidationError{Msg: no + "缺少 sin"}
		}
		if !validSins[in.Sin] {
			return &ValidationError{Msg: no + " sin 非法: " + in.Sin}
		}
		if in.MonsterType == "" {
			return &ValidationError{Msg: no + "缺少 monsterType"}
		}
		if in.BDCount < 1 {
			return &ValidationError{Msg: no + " bdCount 至少为 1"}
		}
		if len(in.BDData) == 0 || string(in.BDData) == "null" {
			return &ValidationError{Msg: no + " bdData 为空"}
		}
		var cards []struct {
			CardID string `json:"cardId"`
		}
		if err := json.Unmarshal(in.BDData, &cards); err != nil || len(cards) == 0 {
			return &ValidationError{Msg: no + " bdData 不是合法的卡牌数组"}
		}
		if len(cards) != in.BDCount {
			return &ValidationError{Msg: fmt.Sprintf("%s bdCount=%d 与实际卡数=%d 不一致", no, in.BDCount, len(cards))}
		}
		for j, c := range cards {
			if c.CardID == "" {
				return &ValidationError{Msg: fmt.Sprintf("%s 第%d张卡缺少 cardId", no, j+1)}
			}
		}
	}
	return nil
}

// UploadUserBD 工具在线上传构筑：严格校验 → 通过则保存文件到 userBD/{playerId}/
// 并按内容指纹去重入库（实时进入投放候选池）；返回入库/去重条数。
// 数据问题返回 *ValidationError（前台提示重新构筑）；重复内容不算错误（已在池中）。
func (s *EliteService) UploadUserBD(dir string, req *UploadSnapshotsRequest) (stored, dups int, err error) {
	log.Printf("[elite] userBD upload received: player=%s run=%s snapshots=%d", req.PlayerID, req.RunID, len(req.Snapshots))
	if err := validateUserBD(req); err != nil {
		log.Printf("[elite] userBD upload REJECTED: player=%s run=%s -> %v", req.PlayerID, req.RunID, err)
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
		log.Printf("[elite] userBD upload: saved file %s", fileName)
	}

	// 内容指纹去重入库（实时生效：入库即可被 pick 命中）
	seen, err := s.loadContentFingerprints()
	if err != nil {
		return 0, 0, fmt.Errorf("load fingerprints: %w", err)
	}
	snaps := make([]*store.BuildSnapshot, 0, len(req.Snapshots))
	for _, in := range req.Snapshots {
		fp := contentFingerprint(in.Sin, in.BDData)
		if _, ok := seen[fp]; ok {
			log.Printf("  [dup] upload sin=%s monsterType=%s bdCount=%d cards=[%s] -> same content already in pool, skipped",
				in.Sin, in.MonsterType, in.BDCount, cardIDList(in.BDData))
			dups++
			continue
		}
		seen[fp] = struct{}{}
		snaps = append(snaps, &store.BuildSnapshot{
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
		for _, snap := range snaps {
			log.Printf("  [stored] upload sin=%s monsterType=%s bdCount=%d sourceWave=%d cards=[%s] (player=%s run=%s)",
				snap.Sin, snap.MonsterType, snap.BDCount, snap.SourceWave, cardIDList(json.RawMessage(snap.BDData)), snap.PlayerID, snap.RunID)
		}
	}
	log.Printf("[elite] userBD upload done: player=%s run=%s stored=%d duplicates=%d", req.PlayerID, req.RunID, stored, dups)
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
			log.Printf("[elite] userBD import skipped: cannot create dir %s: %v", dir, err)
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
			log.Printf("[elite] userBD import file %s failed (non-fatal): %v", filepath.Base(path), err)
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
				log.Printf("[elite] userBD import subdir %s read failed (non-fatal): %v", e.Name(), err)
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
	log.Printf("[elite] userBD import done: %d stored, %d duplicate skipped, %d invalid, %d json file(s) in %s",
		stored, dups, invalid, files, dir)
	return nil
}

// importUserBDFile 解析单个工具导出文件，内容指纹去重后入库；返回入库/去重/非法条数。
// 每条快照均输出明细日志：[stored] 入库 / [dup] 内容重复跳过 / [skip] 字段非法跳过。
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
			log.Printf("  [skip] file=%s playerId/runId missing (playerId=%q runId=%q) -> entry dropped", fileName, en.playerID, en.runID)
			invalid += len(en.snaps)
			continue
		}
		snaps := make([]*store.BuildSnapshot, 0, len(en.snaps))
		for i, in := range en.snaps {
			// 字段校验（与 Upload 同规则：sin/monsterType 非空、bdCount>=1、bdData 非 null）
			if in.Sin == "" || in.MonsterType == "" || in.BDCount < 1 || len(in.BDData) == 0 || string(in.BDData) == "null" {
				log.Printf("  [skip] file=%s entry[%d] invalid fields: sin=%q monsterType=%q bdCount=%d bdData=%s -> dropped",
					fileName, i, in.Sin, in.MonsterType, in.BDCount, bdDataSummary(in.BDData))
				invalid++
				continue
			}
			fp := contentFingerprint(in.Sin, in.BDData)
			if _, ok := seen[fp]; ok {
				log.Printf("  [dup] file=%s sin=%s monsterType=%s bdCount=%d cards=[%s] -> same content already in pool, skipped",
					fileName, in.Sin, in.MonsterType, in.BDCount, cardIDList(in.BDData))
				dups++
				continue
			}
			seen[fp] = struct{}{}
			snaps = append(snaps, &store.BuildSnapshot{
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
			log.Printf("  [stored] file=%s sin=%s monsterType=%s bdCount=%d sourceWave=%d cards=[%s] (player=%s run=%s)",
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

// bdDataSummary bdData 概要（非法条目的 [skip] 日志用，截断防刷屏）。
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

// seedFileFormat 种子数据文件格式。
type seedFileFormat struct {
	Seeds []seedEntry `json:"seeds"`
}

type seedEntry struct {
	PlayerID  string             `json:"playerId"`
	RunID     string             `json:"runId"`
	Snapshots []seedSnapshotItem `json:"snapshots"`
}

type seedSnapshotItem struct {
	Sin         string          `json:"sin"`
	MonsterType string          `json:"monsterType"`
	BDCount     int             `json:"bdCount"`
	BDData      json.RawMessage `json:"bdData"`
	SourceWave  int             `json:"sourceWave"`
	GameTime    int64           `json:"gameTime"`
}

// ============================================================================
// 上传（§8.1：每波选卡后滚动上传）
// ============================================================================

// SnapshotInput 单条快照上传条目（bdData/stats 为不透明 JSON，原样存储）。
type SnapshotInput struct {
	Sin         string          `json:"sin"`
	MonsterType string          `json:"monsterType"`
	BDCount     int             `json:"bdCount"`
	BDData      json.RawMessage `json:"bdData"`
	SourceWave  int             `json:"sourceWave"`
	GameTime    int64           `json:"gameTime"`
	Stats       json.RawMessage `json:"stats,omitempty"`
}

// UploadSnapshotsRequest 批量上传请求。
type UploadSnapshotsRequest struct {
	PlayerID  string          `json:"playerId"` // 设备特征码（F5，客户端生成）
	RunID     string          `json:"runId"`    // 每局一个（F6）
	Snapshots []SnapshotInput `json:"snapshots"`
}

// Upload 批量 upsert 快照，返回实际入库条数（无效条目静默跳过）。
//
// 同 (playerId, runId, sin) 后波覆盖前波——库中始终保留该局该 Sin 的最深版本；
// 上传后执行容量治理：每玩家上限 + 全局 FIFO（§8.2/§8.4）。
func (s *EliteService) Upload(req *UploadSnapshotsRequest) (int, error) {
	if req.PlayerID == "" || req.RunID == "" {
		return 0, fmt.Errorf("playerId and runId are required")
	}

	snaps := make([]*store.BuildSnapshot, 0, len(req.Snapshots))
	skippedReasons := []string{}
	for i, in := range req.Snapshots {
		if in.Sin == "" || in.MonsterType == "" || in.BDCount < 1 {
			skippedReasons = append(skippedReasons, fmt.Sprintf("  [skip] entry[%d] sin=%q monsterType=%q bdCount=%d (invalid fields)", i, in.Sin, in.MonsterType, in.BDCount))
			continue
		}
		if len(in.BDData) == 0 || string(in.BDData) == "null" {
			skippedReasons = append(skippedReasons, fmt.Sprintf("  [skip] entry[%d] sin=%s bdData is empty/null", i, in.Sin))
			continue
		}
		snaps = append(snaps, &store.BuildSnapshot{
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
	if len(snaps) == 0 {
		if len(skippedReasons) > 0 {
			log.Printf("[elite] upload player=%s run=%s -> all %d entries skipped:\n%s",
				req.PlayerID, req.RunID, len(req.Snapshots), joinLines(skippedReasons))
		}
		return 0, nil
	}

	if err := s.store.UpsertSnapshots(snaps); err != nil {
		return 0, fmt.Errorf("upsert snapshots: %w", err)
	}

	// 详细记录每条入库快照
	log.Printf("[elite] upload player=%s run=%s entries=%d stored=%d skipped=%d",
		req.PlayerID, req.RunID, len(req.Snapshots), len(snaps), len(req.Snapshots)-len(snaps))
	for _, snap := range snaps {
		log.Printf("  [stored] sin=%s monsterType=%s bdCount=%d sourceWave=%d gameTime=%d (upsert key=(%s,%s,%s))",
			snap.Sin, snap.MonsterType, snap.BDCount, snap.SourceWave, snap.GameTime,
			snap.PlayerID, snap.RunID, snap.Sin)
	}
	for _, reason := range skippedReasons {
		log.Println(reason)
	}

	// 容量治理：每玩家上限 → 全局 FIFO（覆盖更新不占新额度）。
	s.enforceCapacity(req.PlayerID)

	return len(snaps), nil
}

// enforceCapacity 容量治理：每玩家上限 → 全局 FIFO（Upload 与 userBD 导入共用）。
func (s *EliteService) enforceCapacity(playerID string) {
	if count, err := s.store.CountSnapshotsByPlayer(playerID); err == nil && count > s.cfg.MaxSnapshotsPerPlayer {
		if removed, err := s.store.TrimOldestSnapshotsByPlayer(playerID, s.cfg.MaxSnapshotsPerPlayer); err == nil && removed > 0 {
			log.Printf("[elite] trim player=%s removed=%d (kept=%d, per-player cap=%d)", playerID, removed, s.cfg.MaxSnapshotsPerPlayer, s.cfg.MaxSnapshotsPerPlayer)
		}
	} else if err == nil {
		log.Printf("  [capacity] player=%s snapshots=%d/%d (within limit)", playerID, count, s.cfg.MaxSnapshotsPerPlayer)
	}
	if count, err := s.store.CountSnapshots(); err == nil && count > s.cfg.MaxSnapshots {
		if removed, err := s.store.TrimOldestSnapshots(s.cfg.MaxSnapshots); err == nil && removed > 0 {
			log.Printf("[elite] trim global removed=%d (kept=%d, cap=%d)", removed, s.cfg.MaxSnapshots, s.cfg.MaxSnapshots)
		}
	} else if err == nil {
		log.Printf("  [capacity] global snapshots=%d/%d (within limit)", count, s.cfg.MaxSnapshots)
	}
}

// ============================================================================
// 筛选投放（§3 四步 + §5 三级兜底）
// ============================================================================

// Pick 第 N 波请求精英怪：返回一条「确为他人在更高波次 BD 过」的快照。
//
// 返回值：snap == nil 表示本波不投放（兜底 3，正常业务分支）；
// relaxed 表示命中了放宽波次条件的兜底路径（仅观测用，前台无需处理）。
// waveGap 由客户端传入（难度设置），<0 时回退到服务端配置默认值。
func (s *EliteService) Pick(playerID string, wave int, waveGap int) (snap *store.BuildSnapshot, relaxed bool, err error) {
	if waveGap < 0 {
		waveGap = s.cfg.WaveGap
	}
	log.Printf("[elite] pick START wave=%d player=%s waveGap=%d config={minBD=%d, topBandMode=%s, topBandPercent=%.2f, topBandTopK=%d}",
		wave, playerID, waveGap, s.cfg.MinBD, s.cfg.TopBandMode, s.cfg.TopBandPercent, s.cfg.TopBandTopK)

	// 主路径（Step 1–4）：bdCount >= MIN_BD 且 sourceWave >= N + waveGap 且他人。
	minWave := wave + waveGap
	cands, err := s.store.PickCandidates(s.cfg.MinBD, minWave, playerID)
	if err != nil {
		return nil, false, err
	}
	log.Printf("  [step1-3] query: bdCount>=%d AND sourceWave>=%d AND player!=%s -> candidates=%d",
		s.cfg.MinBD, minWave, playerID, len(cands))
	if len(cands) > 0 {
		s.logCandidates(cands)
		snap := s.pickInBand(cands)
		s.logPickResult("main", wave, playerID, snap, len(cands))
		return snap, false, nil
	}

	// 兜底 1：放宽 WAVE_GAP 到 0（允许同波次的 BD 怪）。Step 1/Step 3 保持。
	log.Printf("  [fallback1] main path empty, relaxing waveGap: sourceWave>=%d (was >=%d)", wave, minWave)
	cands, err = s.store.PickCandidates(s.cfg.MinBD, wave, playerID)
	if err != nil {
		return nil, false, err
	}
	log.Printf("  [fallback1] query: bdCount>=%d AND sourceWave>=%d AND player!=%s -> candidates=%d",
		s.cfg.MinBD, wave, playerID, len(cands))
	if len(cands) > 0 {
		s.logCandidates(cands)
		snap := s.pickInBand(cands)
		s.logPickResult("relaxed:wave-gap=0", wave, playerID, snap, len(cands))
		return snap, true, nil
	}

	// 兜底 2：全库 sourceWave 最高档中 bdCount 最大的一条。Step 1/Step 3 保持。
	log.Printf("  [fallback2] fallback1 empty, trying top-wave tier (global max sourceWave, bdCount>=%d, player!=%s)", s.cfg.MinBD, playerID)
	cands, err = s.store.TopWaveCandidates(s.cfg.MinBD, playerID)
	if err != nil {
		return nil, false, err
	}
	log.Printf("  [fallback2] top-wave candidates=%d", len(cands))
	if len(cands) > 0 {
		s.logCandidates(cands)
		snap := cands[0]
		s.logPickResult("relaxed:top-wave", wave, playerID, snap, len(cands))
		return snap, true, nil
	}

	// 兜底 3：本波不投放精英怪。
	log.Printf("[elite] pick RESULT wave=%d player=%s -> none (all paths exhausted, pool empty)", wave, playerID)
	return nil, false, nil
}

// logPickResult 记录筛选最终结果。
func (s *EliteService) logPickResult(path string, wave int, playerID string, snap *store.BuildSnapshot, candidates int) {
	log.Printf("[elite] pick RESULT wave=%d player=%s -> sin=%s bdCount=%d sourceWave=%d by=%s (path=%s, candidates=%d)",
		wave, playerID, snap.Sin, snap.BDCount, snap.SourceWave, snap.PlayerID, path, candidates)
}

// logCandidates 打印候选列表摘要（最多前 5 条 + 尾部 1 条）。
func (s *EliteService) logCandidates(cands []*store.BuildSnapshot) {
	show := len(cands)
	if show > 5 {
		show = 5
	}
	for i := 0; i < show; i++ {
		c := cands[i]
		log.Printf("  [candidate#%d] id=%d sin=%s bdCount=%d sourceWave=%d player=%s run=%s",
			i+1, c.ID, c.Sin, c.BDCount, c.SourceWave, c.PlayerID, c.RunID)
	}
	if len(cands) > 5 {
		c := cands[len(cands)-1]
		log.Printf("  [candidate#%d] id=%d sin=%s bdCount=%d sourceWave=%d player=%s run=%s (... %d more omitted)",
			len(cands), c.ID, c.Sin, c.BDCount, c.SourceWave, c.PlayerID, c.RunID, len(cands)-show-1)
	}
}

// pickInBand TOP_BAND 内加权随机取 1（§3 Step 4）。
//
// cands 已按 bdCount 降序；高档内以 bdCount 为权重随机，
// 避免每局精英怪永远是同一只 BD 最满的怪；候选很少（band <= 1）时直接取最高。
func (s *EliteService) pickInBand(cands []*store.BuildSnapshot) *store.BuildSnapshot {
	n := s.topBandSize(len(cands))
	log.Printf("  [step4-topband] mode=%s bandSize=%d (from %d candidates, percent=%.2f, topK=%d)",
		s.cfg.TopBandMode, n, len(cands), s.cfg.TopBandPercent, s.cfg.TopBandTopK)
	if n <= 1 {
		log.Printf("  [step4-topband] band<=1, pick top: id=%d sin=%s bdCount=%d", cands[0].ID, cands[0].Sin, cands[0].BDCount)
		return cands[0]
	}
	total := 0
	for i := 0; i < n; i++ {
		total += cands[i].BDCount
	}
	if total <= 0 {
		return cands[0]
	}
	r := rand.Intn(total)
	log.Printf("  [step4-topband] weighted random: totalWeight=%d, roll=%d", total, r)
	for i := 0; i < n; i++ {
		log.Printf("    band[%d] id=%d sin=%s bdCount=%d (weight=%d, remaining=%d)",
			i, cands[i].ID, cands[i].Sin, cands[i].BDCount, cands[i].BDCount, r)
		r -= cands[i].BDCount
		if r < 0 {
			log.Printf("  [step4-topband] -> selected band[%d] id=%d sin=%s bdCount=%d", i, cands[i].ID, cands[i].Sin, cands[i].BDCount)
			return cands[i]
		}
	}
	return cands[n-1]
}

// topBandSize 计算参与加权随机的高分档条数（双模式，§8.4）。
func (s *EliteService) topBandSize(n int) int {
	if n <= 1 {
		return n
	}
	if s.cfg.TopBandMode == "topk" {
		if n < s.cfg.TopBandTopK {
			return n
		}
		return s.cfg.TopBandTopK
	}
	size := int(math.Ceil(float64(n) * s.cfg.TopBandPercent))
	if size < 1 {
		size = 1
	}
	if size > n {
		size = n
	}
	return size
}

func joinLines(lines []string) string {
	return strings.Join(lines, "\n")
}
