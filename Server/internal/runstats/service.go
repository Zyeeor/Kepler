// Package runstats 整局运行数据（Run Analytics）接收域。
//
// 《Possession》单机游戏的第三块后端职责（与 UGC / 精英投放并列）：接收客户端
// 每局结束后的 RunStats 原始数据（POST /api/runs），按 (playerId, runId) 幂等
// upsert 存储。服务器只存原始值，不做评分——主/次倾向由后端分析（或配置化评分器）
// 基于原始数据计算（对接文档 §2.3-1）。
//
// 数据契约：Docs/00_Project/Engineering/RunStats_后端对接文档.md §4（v1.0）。
// 存储与精英快照库分开（不同生命周期：对局记录长期保留，不参与 FIFO 淘汰，§4.2-3）。
package runstats

import (
	"strconv"
	"strings"
	"time"

	"possession/server/internal/logx"
)

// maxPerSin perSin 条目数上限（七宗罪）。
const maxPerSin = 7

// validSins 合法七宗罪 wire 名（客户端 RunStatsUtil.WireName / EliteMonsterCatalog 同源）。
var validSins = map[string]bool{
	"pride": true, "sloth": true, "gluttony": true, "envy": true,
	"wrath": true, "greed": true, "lust": true,
}

// ValidationError 请求校验错误（客户端数据异常；前台应提示，不应重试）。
type ValidationError struct{ Msg string }

func (e *ValidationError) Error() string { return e.Msg }

// UploadRequest POST /api/runs 请求体（对接文档 §4：camelCase，sin 为 wire 名小写英文）。
type UploadRequest struct {
	SchemaVersion int    `json:"schemaVersion"`
	RunID         string `json:"runId"`
	PlayerID      string `json:"playerId"`

	// 身份 / 时间
	StartedAtUnix      int64   `json:"startedAtUnix"`
	EndedAtUnix        int64   `json:"endedAtUnix"`
	RunDurationSeconds float64 `json:"runDurationSeconds"`

	// 结果
	Won              bool   `json:"won"`
	EndPhase         string `json:"endPhase"`
	ReachedWaveIndex int    `json:"reachedWaveIndex"`
	FinalReached     bool   `json:"finalReached"`
	FinalCompleted   bool   `json:"finalCompleted"`

	// Global 计数（§2.3-3）
	TotalPossessions       int     `json:"totalPossessions"`
	VoluntaryReleases      int     `json:"voluntaryReleases"`
	DeathRelays            int     `json:"deathRelays"`
	SoulEnters             int     `json:"soulEnters"`
	ShrineRecovers         int     `json:"shrineRecovers"`
	LowHealthReleases      int     `json:"lowHealthReleases"`
	BulletTimeCount        int     `json:"bulletTimeCount"`
	BulletTimeTotalSeconds float64 `json:"bulletTimeTotalSeconds"`
	EliteFatalCount        int     `json:"eliteFatalCount"`
	ElitePossessionCount   int     `json:"elitePossessionCount"`
	DistinctSinsUsed       int     `json:"distinctSinsUsed"`
	TotalKills             int     `json:"totalKills"`

	// Per-Sin（§2.3-2）
	PerSin []PerSinRecord `json:"perSin"`
}

// PerSinRecord 单个 Sin 的分项统计（§2.3-2）。
type PerSinRecord struct {
	Sin                 string  `json:"sin"`
	ControlSeconds      float64 `json:"controlSeconds"`
	PossessionCount     int     `json:"possessionCount"`
	MovementCount       int     `json:"movementCount"`
	AttackCount         int     `json:"attackCount"`
	SpecialCount        int     `json:"specialCount"`
	CardInvestmentCount int     `json:"cardInvestmentCount"`
	Kills               int     `json:"kills"`
}

// Record 存储层数据结构（sqlite run_stats / run_stats_per_sin 两表）。
type Record struct {
	UploadRequest
	UploadedAtUnix int64 // 服务器接收时间（Unix 秒）
}

// Store 存储接口（sqlite 实现）。
type Store interface {
	// UpsertRun 幂等写入：同 (player_id, run_id) 覆盖重写（含 perSin 子表）。
	UpsertRun(rec *Record) error
}

// Service Run Analytics 接收服务。
type Service struct {
	store Store
}

// NewService 创建服务。
func NewService(st Store) *Service { return &Service{store: st} }

// Upload 校验并写入一局 RunStats（对接文档 §4）。
//
// 校验策略（§4.2）：身份字段必填；sin 必须 wire 名合法且不重复、条数 ≤ 7；
// 数值字段不做正负校验（原始数据存档定位）；未知 schemaVersion 容忍（日志留痕，
// 多余 JSON 字段自然忽略、缺失字段零值——不阻塞客户端版本迁移）。
func (s *Service) Upload(req *UploadRequest) error {
	if req.PlayerID == "" || req.RunID == "" {
		return &ValidationError{Msg: "missing playerId or runId"}
	}
	if len(req.PerSin) > maxPerSin {
		return &ValidationError{Msg: "too many perSin entries (max " + strconv.Itoa(maxPerSin) + ")"}
	}

	seen := make(map[string]bool, len(req.PerSin))
	for i := range req.PerSin {
		sin := strings.ToLower(strings.TrimSpace(req.PerSin[i].Sin))
		if !validSins[sin] {
			return &ValidationError{Msg: "perSin[" + strconv.Itoa(i) + "]: unknown sin '" + req.PerSin[i].Sin + "'"}
		}
		if seen[sin] {
			return &ValidationError{Msg: "perSin[" + strconv.Itoa(i) + "]: duplicate sin '" + sin + "'"}
		}
		seen[sin] = true
		req.PerSin[i].Sin = sin // 归一化（trim + 小写）后入库
	}

	if req.SchemaVersion != 1 {
		logx.Event("runstats upload · unknown schemaVersion=%d player=%s run=%s",
			req.SchemaVersion, req.PlayerID, req.RunID)
	}

	rec := &Record{UploadRequest: *req, UploadedAtUnix: time.Now().Unix()}
	if err := s.store.UpsertRun(rec); err != nil {
		return err
	}
	logx.Event("runstats stored · player=%s run=%s won=%t phase=%s wave=%d kills=%d perSin=%d",
		req.PlayerID, req.RunID, req.Won, req.EndPhase, req.ReachedWaveIndex,
		req.TotalKills, len(req.PerSin))
	return nil
}
