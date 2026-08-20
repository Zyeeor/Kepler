// Package service — 精英怪 BD 快照服务：上传（滚动 upsert）与筛选投放。
//
// 设计依据：《精英怪筛选 — 他人 BD 怪物投放 · 策划案》§3–§6/§8。
// 筛选只依赖 bdCount 与 sourceWave；透传原则见 store.BuildSnapshot。
package service

import (
	"encoding/json"
	"fmt"
	"log"
	"math"
	"math/rand"

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
		WaveGap:               1,
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
	for _, in := range req.Snapshots {
		// 防御：正常客户端只上传 bdCount >= 1 的 Sin（F7）；无效条目不污染候选库。
		if in.Sin == "" || in.MonsterType == "" || in.BDCount < 1 {
			continue
		}
		if len(in.BDData) == 0 || string(in.BDData) == "null" {
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
		return 0, nil
	}

	if err := s.store.UpsertSnapshots(snaps); err != nil {
		return 0, fmt.Errorf("upsert snapshots: %w", err)
	}

	// 容量治理：每玩家上限 → 全局 FIFO（覆盖更新不占新额度）。
	if count, err := s.store.CountSnapshotsByPlayer(req.PlayerID); err == nil && count > s.cfg.MaxSnapshotsPerPlayer {
		if removed, err := s.store.TrimOldestSnapshotsByPlayer(req.PlayerID, s.cfg.MaxSnapshotsPerPlayer); err == nil && removed > 0 {
			log.Printf("[elite] trim player=%s removed=%d (per-player cap=%d)", req.PlayerID, removed, s.cfg.MaxSnapshotsPerPlayer)
		}
	}
	if count, err := s.store.CountSnapshots(); err == nil && count > s.cfg.MaxSnapshots {
		if removed, err := s.store.TrimOldestSnapshots(s.cfg.MaxSnapshots); err == nil && removed > 0 {
			log.Printf("[elite] trim global removed=%d (cap=%d)", removed, s.cfg.MaxSnapshots)
		}
	}

	log.Printf("[elite] upload player=%s run=%s entries=%d stored=%d skipped=%d",
		req.PlayerID, req.RunID, len(req.Snapshots), len(snaps), len(req.Snapshots)-len(snaps))
	return len(snaps), nil
}

// ============================================================================
// 筛选投放（§3 四步 + §5 三级兜底）
// ============================================================================

// Pick 第 N 波请求精英怪：返回一条「确为他人在更高波次 BD 过」的快照。
//
// 返回值：snap == nil 表示本波不投放（兜底 3，正常业务分支）；
// relaxed 表示命中了放宽波次条件的兜底路径（仅观测用，前台无需处理）。
func (s *EliteService) Pick(playerID string, wave int) (snap *store.BuildSnapshot, relaxed bool, err error) {
	// 主路径（Step 1–4）：bdCount >= MIN_BD 且 sourceWave >= N + WAVE_GAP 且他人。
	cands, err := s.store.PickCandidates(s.cfg.MinBD, wave+s.cfg.WaveGap, playerID)
	if err != nil {
		return nil, false, err
	}
	if len(cands) > 0 {
		snap := s.pickInBand(cands)
		s.logPick("main", wave, playerID, snap, len(cands))
		return snap, false, nil
	}

	// 兜底 1：放宽 WAVE_GAP 到 0（允许同波次的 BD 怪）。Step 1/Step 3 保持。
	cands, err = s.store.PickCandidates(s.cfg.MinBD, wave, playerID)
	if err != nil {
		return nil, false, err
	}
	if len(cands) > 0 {
		snap := s.pickInBand(cands)
		s.logPick("relaxed:wave-gap=0", wave, playerID, snap, len(cands))
		return snap, true, nil
	}

	// 兜底 2：全库 sourceWave 最高档中 bdCount 最大的一条。Step 1/Step 3 保持。
	cands, err = s.store.TopWaveCandidates(s.cfg.MinBD, playerID)
	if err != nil {
		return nil, false, err
	}
	if len(cands) > 0 {
		snap := cands[0]
		s.logPick("relaxed:top-wave", wave, playerID, snap, len(cands))
		return snap, true, nil
	}

	// 兜底 3：本波不投放精英怪。
	log.Printf("[elite] pick wave=%d player=%s -> none (empty pool)", wave, playerID)
	return nil, false, nil
}

// logPick 记录一次筛选命中（含命中路径与候选规模，供运营观测筛选健康度）。
func (s *EliteService) logPick(path string, wave int, playerID string, snap *store.BuildSnapshot, candidates int) {
	log.Printf("[elite] pick wave=%d player=%s -> sin=%s bdCount=%d sourceWave=%d by=%s (%s, candidates=%d)",
		wave, playerID, snap.Sin, snap.BDCount, snap.SourceWave, snap.PlayerID, path, candidates)
}

// pickInBand TOP_BAND 内加权随机取 1（§3 Step 4）。
//
// cands 已按 bdCount 降序；高档内以 bdCount 为权重随机，
// 避免每局精英怪永远是同一只 BD 最满的怪；候选很少（band <= 1）时直接取最高。
func (s *EliteService) pickInBand(cands []*store.BuildSnapshot) *store.BuildSnapshot {
	n := s.topBandSize(len(cands))
	if n <= 1 {
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
	for i := 0; i < n; i++ {
		r -= cands[i].BDCount
		if r < 0 {
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
