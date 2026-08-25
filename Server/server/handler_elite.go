// 精英怪 BD 快照投放 / 战果回传 HTTP 接口。
package server

import (
	"encoding/json"
	"errors"
	"net/http"
	"strconv"

	"demo/server/elite"
	"demo/server/tools/logx"
)

// snapshotJSON 对外（HTTP）的 BD 快照结构，camelCase；bdData/stats 原样透传。
type snapshotJSON struct {
	SnapshotID     int64           `json:"snapshotId"`
	SourcePlayerID string          `json:"sourcePlayerId"`
	RunID          string          `json:"runId"`
	Sin            string          `json:"sin"`
	MonsterType    string          `json:"monsterType"`
	BDData         json.RawMessage `json:"bdData"`
	BDCount        int             `json:"bdCount"`
	SourceWave     int             `json:"sourceWave"`
	GameTime       int64           `json:"gameTime"`
	Stats          json.RawMessage `json:"stats,omitempty"`
}

func toSnapshotJSON(snap *elite.BuildSnapshot) snapshotJSON {
	var stats json.RawMessage
	if snap.Stats != "" {
		stats = json.RawMessage(snap.Stats)
	}
	return snapshotJSON{
		SnapshotID:     snap.ID,
		SourcePlayerID: snap.PlayerID,
		RunID:          snap.RunID,
		Sin:            snap.Sin,
		MonsterType:    snap.MonsterType,
		BDData:         json.RawMessage(snap.BDData),
		BDCount:        snap.BDCount,
		SourceWave:     snap.SourceWave,
		GameTime:       snap.GameTime,
		Stats:          stats,
	}
}

// eliteStatsJSON 对外（HTTP）的战绩聚合结构，camelCase。
type eliteStatsJSON struct {
	OwnerPlayerID string `json:"ownerPlayerId"`
	OwnerRunID    string `json:"ownerRunId"`
	Sin           string `json:"sin"`
	Deployed      int    `json:"deployed"`  // 被投放次数
	Fatal         int    `json:"fatal"`     // 被其他玩家击杀次数
	Possessed     int    `json:"possessed"` // 被其他玩家 Possess 次数
	BodyFatal     int    `json:"bodyFatal"` // 造成 Body Fatal 次数
	RunFail       int    `json:"runFail"`   // 直接导致 Run Fail 次数
	UpdatedAt     int64  `json:"updatedAt"`
}

// handleSnapshotUpload 每轮选卡后批量滚动上传 BD 快照（策划案 §8.1；sourceWave = 上传时第几次投放精英怪）。
func (s *Server) handleSnapshotUpload(w http.ResponseWriter, r *http.Request) {
	var req elite.UploadSnapshotsRequest
	if err := decodeJSON(r, &req); err != nil {
		writeErr(w, http.StatusBadRequest, "bad request")
		return
	}

	accepted, err := s.eliteSvc.Upload(&req)
	if err != nil {
		writeErr(w, http.StatusBadRequest, err.Error())
		return
	}
	writeJSON(w, http.StatusOK, map[string]any{"ok": true, "accepted": accepted})
}

// handleUserBDUpload MonsterBuildEditor 工具在线上传构筑（严格校验 → 存文件 → 实时入池）。
//
// 数据校验失败返回 400（前台提示"数据有误，重新构筑"）；重复内容不算错误（返回 duplicates 计数）。
func (s *Server) handleUserBDUpload(w http.ResponseWriter, r *http.Request) {
	var req elite.UploadSnapshotsRequest
	if err := decodeJSON(r, &req); err != nil {
		logx.Event("userBD upload rejected · bad JSON body from %s: %v", r.RemoteAddr, err)
		writeErr(w, http.StatusBadRequest, "invalid data: request body is not valid JSON")
		return
	}

	stored, dups, err := s.eliteSvc.UploadUserBD(s.cfg.UserBDDir, &req)
	if err != nil {
		var ve *elite.ValidationError
		if errors.As(err, &ve) {
			writeErr(w, http.StatusBadRequest, "invalid build data: "+ve.Msg)
			return
		}
		writeErr(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusOK, map[string]any{
		"ok":         true,
		"stored":     stored, // 新入库（实时进入投放候选池）
		"duplicates": dups,   // 内容重复（已在池中，不计错误）
	})
}

// handleElitePick 第 N 次投放请求精英怪（§3/§5；wave = 投放序号）。
// snapshot == null = 本次不投放（兜底 3，正常业务分支）。
func (s *Server) handleElitePick(w http.ResponseWriter, r *http.Request) {
	var req struct {
		PlayerID string `json:"playerId"`
		Wave     int    `json:"wave"`    // 本次投放序号 N（第几次投放精英怪；sourceWave 语义/编码由前台决定，透传比较）
		WaveGap  int    `json:"waveGap"` // 投放序号差（客户端难度设置，0=同投放序号，1=越一级投放）
	}
	if err := decodeJSON(r, &req); err != nil {
		writeErr(w, http.StatusBadRequest, "bad request")
		return
	}
	if req.PlayerID == "" {
		writeErr(w, http.StatusBadRequest, "playerId is required")
		return
	}
	if req.Wave < 1 {
		writeErr(w, http.StatusBadRequest, "wave must be >= 1 (elite injection count, 1-based)")
		return
	}

	snap, relaxed, err := s.eliteSvc.Pick(req.PlayerID, req.Wave, req.WaveGap)
	if err != nil {
		writeErr(w, http.StatusInternalServerError, err.Error())
		return
	}
	if snap == nil {
		writeJSON(w, http.StatusOK, map[string]any{"snapshot": nil, "relaxed": false})
		return
	}
	writeJSON(w, http.StatusOK, map[string]any{
		"snapshot": toSnapshotJSON(snap),
		"relaxed":  relaxed, // 命中放宽投放序号的兜底路径（仅观测）
	})
}

// handleEliteEvents 战果回传（策划案 §6.5）：精英在他人游戏中的战果事件批量上报，
// 按构筑主人 (ownerPlayerId, ownerRunId, sin) 聚合。无主/非法条目逐条跳过，不整批失败。
func (s *Server) handleEliteEvents(w http.ResponseWriter, r *http.Request) {
	var req elite.RecordEventsRequest
	if err := decodeJSON(r, &req); err != nil {
		writeErr(w, http.StatusBadRequest, "bad request")
		return
	}

	accepted, err := s.eliteSvc.RecordEvents(&req)
	if err != nil {
		writeErr(w, http.StatusBadRequest, err.Error())
		return
	}
	writeJSON(w, http.StatusOK, map[string]any{"ok": true, "accepted": accepted})
}

// handleEliteLeaderboard 荣誉殿堂排行榜（策划案 §5.4/§5.8）：击杀玩家次数（bodyFatal）
// 最多的 Top N 个 BD 怪物，按构筑维度（owner + run + sin）聚合展示。
//
// GET /api/elite/leaderboard?limit=20（limit 默认 20，范围 1–100；非法值 400）。
// 被容量治理淘汰的悬空聚合行不上榜（怪物与构筑信息已不可考）；
// bdData 原样透传（卡数组结构由前台定义）。
func (s *Server) handleEliteLeaderboard(w http.ResponseWriter, r *http.Request) {
	limit := elite.LeaderboardDefaultLimit
	if v := r.URL.Query().Get("limit"); v != "" {
		n, err := strconv.Atoi(v)
		if err != nil || n < 1 {
			writeErr(w, http.StatusBadRequest, "invalid limit (must be a positive integer)")
			return
		}
		limit = n
	}

	entries, err := s.eliteSvc.Leaderboard(limit)
	if err != nil {
		writeErr(w, http.StatusInternalServerError, err.Error())
		return
	}

	out := make([]map[string]any, 0, len(entries))
	for i, e := range entries {
		out = append(out, map[string]any{
			"rank":          i + 1,
			"snapshotId":    e.SnapshotID,
			"ownerPlayerId": e.OwnerPlayerID,
			"ownerRunId":    e.OwnerRunID,
			"sin":           e.Sin,
			"monsterType":   e.MonsterType,
			"bdCount":       e.BDCount,
			"sourceWave":    e.SourceWave,
			"bdData":        json.RawMessage(e.BDData), // 透传前台定义的卡数组
			"stats": map[string]any{
				"deployed":  e.Deployed,
				"fatal":     e.Fatal,
				"possessed": e.Possessed,
				"bodyFatal": e.BodyFatal, // 排序主键：击杀玩家次数
				"runFail":   e.RunFail,
			},
		})
	}
	writeJSON(w, http.StatusOK, map[string]any{"entries": out})
}

// handleEliteStats 查询构筑主人的异步战绩聚合（荣誉殿堂数据出口，§5.4/§5.8）。
// query: playerId = 构筑主人（非回报者）。
func (s *Server) handleEliteStats(w http.ResponseWriter, r *http.Request) {
	owner := r.URL.Query().Get("playerId")
	stats, err := s.eliteSvc.OwnerEliteStats(owner)
	if err != nil {
		writeErr(w, http.StatusBadRequest, err.Error())
		return
	}

	out := make([]eliteStatsJSON, 0, len(stats))
	for _, st := range stats {
		out = append(out, eliteStatsJSON{
			OwnerPlayerID: st.OwnerPlayerID,
			OwnerRunID:    st.OwnerRunID,
			Sin:           st.Sin,
			Deployed:      st.Deployed,
			Fatal:         st.Fatal,
			Possessed:     st.Possessed,
			BodyFatal:     st.BodyFatal,
			RunFail:       st.RunFail,
			UpdatedAt:     st.UpdatedAt,
		})
	}
	writeJSON(w, http.StatusOK, map[string]any{"playerId": owner, "stats": out})
}
