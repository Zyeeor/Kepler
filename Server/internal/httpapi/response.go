// JSON 编解码与统一响应辅助（handler 共用）。
//
// 响应体约定：成功响应由各域的显式 Response DTO 定义（编译期字段检查）；
// 错误响应统一为 errorResponse（{"code":<http 状态>,"msg":"..."}）。后续如需引入业务
// 错误码，在 errorResponse 上扩展字段，不改变既有 wire 键名。
package httpapi

import (
	"encoding/json"
	"net/http"

	"possession/server/internal/logx"
)

// errorResponse 统一错误响应体。
type errorResponse struct {
	Code int    `json:"code"`
	Msg  string `json:"msg"`
}

// okResponse 通用成功响应（{"ok":true}）。
type okResponse struct {
	OK bool `json:"ok"`
}

func writeJSON(w http.ResponseWriter, code int, v any) {
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.WriteHeader(code)
	_ = json.NewEncoder(w).Encode(v)
}

func writeErr(w http.ResponseWriter, code int, msg string) {
	if code >= 500 {
		logx.Event("error %d · %s", code, msg) // 5xx 落服务端日志；4xx 属正常业务分支，访问日志已覆盖
	}
	writeJSON(w, code, errorResponse{Code: code, Msg: msg})
}

func decodeJSON(r *http.Request, v any) error {
	return json.NewDecoder(r.Body).Decode(v)
}
