// 精英怪种子快照注入：候选库为空时加载预设快照（首位玩家体验保障）。
package elite

import (
	"encoding/json"
	"fmt"
	"os"

	"demo/server/tools/logx"
)

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
		logx.Event("seed skipped · pool already has %d snapshots", count)
		return nil
	}

	data, err := os.ReadFile(seedFile)
	if err != nil {
		if os.IsNotExist(err) {
			logx.Event("seed skipped · file not found (%s)", seedFile)
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
		snaps := make([]*BuildSnapshot, 0, len(entry.Snapshots))
		for _, in := range entry.Snapshots {
			if in.Sin == "" || in.BDCount < 1 || len(in.BDData) == 0 {
				continue
			}
			bdJSON, _ := json.Marshal(in.BDData)
			snaps = append(snaps, &BuildSnapshot{
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

	logx.Event("seeded %d snapshots from %s (pool was empty)", total, seedFile)
	return nil
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
