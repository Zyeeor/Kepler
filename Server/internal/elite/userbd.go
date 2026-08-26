// 用户 BD 工具在线上传（MonsterBuildEditor → POST /api/user-bd）：严格校验 + 原子存文件
// + 指纹去重入库（实时进入投放候选池）。启动目录导入见 userbd_import.go；指纹缓存见
// fingerprint.go。
package elite

import (
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"strings"

	"possession/server/internal/logx"
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
