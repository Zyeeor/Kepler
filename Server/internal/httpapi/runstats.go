// Run Analytics 域（RunStats_后端对接文档.md §4）：整局运行数据上传。
// 服务器只存原始值（幂等 upsert，不做评分）。
package httpapi

import (
	"errors"
	"net/http"

	"possession/server/internal/runstats"
)

// registerRunStatsRoutes 注册 Run Analytics 路由。
func (s *Server) registerRunStatsRoutes(mux *http.ServeMux) {
	mux.Handle("POST /api/runs", named("handleRunStatsUpload",
		s.limited("runs", s.handleRunStatsUpload)))
}

// handleRunStatsUpload 对局数据上传（每局结束一次；同 runId 重传覆盖 = 幂等，
// 客户端可安全重试）。校验失败 400（数据异常，前台提示）；存储失败 500。
func (s *Server) handleRunStatsUpload(w http.ResponseWriter, r *http.Request) {
	var req runstats.UploadRequest
	if err := decodeJSON(r, &req); err != nil {
		writeErr(w, http.StatusBadRequest, "bad request")
		return
	}

	if err := s.runStatsSvc.Upload(&req); err != nil {
		var ve *runstats.ValidationError
		if errors.As(err, &ve) {
			writeErr(w, http.StatusBadRequest, ve.Msg)
			return
		}
		writeErr(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusOK, okResponse{OK: true})
}
