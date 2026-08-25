// 请求/响应 JSON 辅助（handler 共用）。
package server

import (
	"encoding/json"
	"net/http"

	"demo/server/tools/logx"
)

func writeJSON(w http.ResponseWriter, code int, v any) {
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.WriteHeader(code)
	_ = json.NewEncoder(w).Encode(v)
}

func writeErr(w http.ResponseWriter, code int, msg string) {
	if code >= 500 {
		logx.Event("error %d · %s", code, msg) // 5xx 落服务端日志；4xx 属正常业务分支，访问日志已覆盖
	}
	writeJSON(w, code, map[string]any{"code": code, "msg": msg})
}

func decodeJSON(r *http.Request, v any) error {
	return json.NewDecoder(r.Body).Decode(v)
}
