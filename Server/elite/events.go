// 战果回传（策划案 §6.5）：精英在他人游戏中的战果事件 → 按构筑主人聚合。
package elite

import (
	"fmt"

	"demo/server/tools/logx"
)

// localPresetOwner 客户端本地 Preset 兜底快照的占位主人（EliteBuildCarrier 默认值）：
// 无真实玩家可归属战绩，回报无意义，逐条跳过。
const localPresetOwner = "local-preset"

// eliteEventTypes 合法战果事件类型（§6.5 五类）。
var eliteEventTypes = map[string]bool{
	"spawned":   true, // 精英成功生成（= 被投放）
	"fatal":     true, // 精英 Fatal（被击杀）
	"possessed": true, // 精英被 Possess
	"bodyFatal": true, // 精英造成玩家 Body Fatal
	"runFail":   true, // 精英直接导致 Soul Death / Run Fail
}

// EliteEventInput 客户端上报的单条战果事件。
type EliteEventInput struct {
	SnapshotID    int64  `json:"snapshotId"`    // 投放命中的快照 ID（观测）
	OwnerPlayerID string `json:"ownerPlayerId"` // 构筑主人（聚合键）
	OwnerRunID    string `json:"ownerRunId"`    // 构筑主人 Run ID（聚合键）
	Sin           string `json:"sin"`           // 七宗罪 wire 名（聚合键）
	Type          string `json:"type"`          // spawned / fatal / possessed / bodyFatal / runFail
	Wave          int    `json:"wave"`          // 事件发生时的投放序号 = 第几次投放精英怪（观测，透传）
	GameTime      int64  `json:"gameTime"`      // 事件发生游戏时间（观测，透传）
}

// RecordEventsRequest 战果回传请求体（批量）。
type RecordEventsRequest struct {
	PlayerID string            `json:"playerId"` // 回报玩家（观测，不参与聚合键）
	Events   []EliteEventInput `json:"events"`
}

// RecordEvents 校验并聚合战果事件（荣誉殿堂异步战绩的数据源，§6.5）。
//
// 无主事件（本地 Preset / 缺 owner）与非法 sin/type 逐条跳过，不整批失败；
// 返回实际聚合写入条数。
func (s *EliteService) RecordEvents(req *RecordEventsRequest) (int, error) {
	if req.PlayerID == "" {
		return 0, fmt.Errorf("playerId is required")
	}
	if len(req.Events) == 0 {
		return 0, nil
	}

	events := make([]*EliteEvent, 0, len(req.Events))
	for i, in := range req.Events {
		if in.OwnerPlayerID == "" || in.OwnerPlayerID == localPresetOwner {
			logx.Detail("skip event[%d] · type=%s owner=%q (no real owner)", i, in.Type, in.OwnerPlayerID)
			continue
		}
		if !validSins[in.Sin] || !eliteEventTypes[in.Type] {
			logx.Detail("skip event[%d] · sin=%q type=%q (invalid)", i, in.Sin, in.Type)
			continue
		}
		events = append(events, &EliteEvent{
			OwnerPlayerID: in.OwnerPlayerID,
			OwnerRunID:    in.OwnerRunID,
			Sin:           in.Sin,
			Type:          in.Type,
			SnapshotID:    in.SnapshotID,
			ReporterID:    req.PlayerID,
			Wave:          in.Wave,
			GameTime:      in.GameTime,
		})
	}
	if len(events) == 0 {
		logx.Event("events · reporter=%s → all %d entries skipped", req.PlayerID, len(req.Events))
		return 0, nil
	}

	n, err := s.store.RecordEliteEvents(events)
	if err != nil {
		return 0, fmt.Errorf("record elite events: %w", err)
	}
	if n > 0 {
		s.invalidateLeaderboard() // 战果聚合改变榜单 → 失效缓存
	}
	logx.Event("events · reporter=%s accepted=%d/%d", req.PlayerID, n, len(req.Events))
	return n, nil
}

// OwnerEliteStats 查询构筑主人的异步战绩聚合（荣誉殿堂数据出口，§5.4/§5.8）。
func (s *EliteService) OwnerEliteStats(ownerPlayerID string) ([]*EliteBuildStats, error) {
	if ownerPlayerID == "" {
		return nil, fmt.Errorf("playerId is required")
	}
	return s.store.GetEliteBuildStats(ownerPlayerID)
}
