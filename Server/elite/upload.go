// 快照滚动上传（§8.1：每波选卡后）与容量治理（§8.2/§8.4）。
package elite

import (
	"encoding/json"
	"fmt"

	"demo/server/tools/logx"
)

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

	snaps := make([]*BuildSnapshot, 0, len(req.Snapshots))
	skippedReasons := []string{}
	for i, in := range req.Snapshots {
		if in.Sin == "" || in.MonsterType == "" || in.BDCount < 1 {
			skippedReasons = append(skippedReasons, fmt.Sprintf("skip entry[%d] · sin=%q monsterType=%q bdCount=%d (invalid fields)", i, in.Sin, in.MonsterType, in.BDCount))
			continue
		}
		if len(in.BDData) == 0 || string(in.BDData) == "null" {
			skippedReasons = append(skippedReasons, fmt.Sprintf("skip entry[%d] · sin=%s bdData is empty/null", i, in.Sin))
			continue
		}
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
	if len(snaps) == 0 {
		if len(skippedReasons) > 0 {
			logx.Event("upload player=%s run=%s → all %d entries skipped",
				req.PlayerID, req.RunID, len(req.Snapshots))
			for _, reason := range skippedReasons {
				logx.Detail("%s", reason)
			}
		}
		return 0, nil
	}

	if err := s.store.UpsertSnapshots(snaps); err != nil {
		return 0, fmt.Errorf("upsert snapshots: %w", err)
	}

	// 详细记录每条入库快照
	logx.Event("upload player=%s run=%s · entries=%d stored=%d skipped=%d",
		req.PlayerID, req.RunID, len(req.Snapshots), len(snaps), len(req.Snapshots)-len(snaps))
	for _, snap := range snaps {
		logx.Detail("stored · sin=%s monsterType=%s bdCount=%d sourceWave=%d gameTime=%d (upsert %s/%s/%s)",
			snap.Sin, snap.MonsterType, snap.BDCount, snap.SourceWave, snap.GameTime,
			snap.PlayerID, snap.RunID, snap.Sin)
	}
	for _, reason := range skippedReasons {
		logx.Detail("%s", reason)
	}

	// 容量治理：每玩家上限 → 全局 FIFO（覆盖更新不占新额度）。
	s.enforceCapacity(req.PlayerID)

	return len(snaps), nil
}

// enforceCapacity 容量治理：每玩家上限 → 全局 FIFO（Upload 与 userBD 导入共用）。
func (s *EliteService) enforceCapacity(playerID string) {
	if count, err := s.store.CountSnapshotsByPlayer(playerID); err == nil && count > s.cfg.MaxSnapshotsPerPlayer {
		if removed, err := s.store.TrimOldestSnapshotsByPlayer(playerID, s.cfg.MaxSnapshotsPerPlayer); err == nil && removed > 0 {
			logx.Event("trim player=%s · removed=%d (per-player cap=%d)", playerID, removed, s.cfg.MaxSnapshotsPerPlayer)
		}
	} else if err == nil {
		logx.Detail("capacity · player=%s %d/%d (within limit)", playerID, count, s.cfg.MaxSnapshotsPerPlayer)
	}
	if count, err := s.store.CountSnapshots(); err == nil && count > s.cfg.MaxSnapshots {
		if removed, err := s.store.TrimOldestSnapshots(s.cfg.MaxSnapshots); err == nil && removed > 0 {
			logx.Event("trim global · removed=%d (cap=%d)", removed, s.cfg.MaxSnapshots)
		}
	} else if err == nil {
		logx.Detail("capacity · global %d/%d (within limit)", count, s.cfg.MaxSnapshots)
	}
}
