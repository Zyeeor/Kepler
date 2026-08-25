// HTTP 中间件：访问日志 + CORS。
package server

import (
	"context"
	"net/http"
	"time"

	"demo/server/tools/logx"
)

// statusWriter 捕获响应状态码的 ResponseWriter 包装。
type statusWriter struct {
	http.ResponseWriter
	status int
}

func (w *statusWriter) WriteHeader(code int) {
	w.status = code
	w.ResponseWriter.WriteHeader(code)
}

// accessInfo 单次请求的访问日志信息（handler 函数名由 named 包装器在路由命中后回填）。
type accessInfo struct{ handler string }

// ctxKeyAccess accessInfo 的 context 键。
type ctxKeyAccess struct{}

// named 标记路由对应的 handler 函数名，访问日志据此显示（如 handleElitePick → 200）。
func named(name string, h http.HandlerFunc) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if info, ok := r.Context().Value(ctxKeyAccess{}).(*accessInfo); ok {
			info.handler = name
		}
		h(w, r)
	})
}

// logRequests 访问日志中间件：日志显示 Server handler 函数名（未匹配路由回退为 URL 路径），
// 附状态码、耗时、来源地址。同时处理 CORS（MonsterBuildEditor 工具为浏览器页面，直连本服务需跨源许可与 OPTIONS 预检）。
func logRequests(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Access-Control-Allow-Origin", "*")
		w.Header().Set("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
		w.Header().Set("Access-Control-Allow-Headers", "Content-Type")
		if r.Method == http.MethodOptions {
			w.WriteHeader(http.StatusNoContent)
			return
		}
		start := time.Now()
		sw := &statusWriter{ResponseWriter: w}
		info := &accessInfo{}
		next.ServeHTTP(sw, r.WithContext(context.WithValue(r.Context(), ctxKeyAccess{}, info)))
		endpoint := info.handler
		if endpoint == "" {
			endpoint = r.URL.Path // 未匹配到路由（404）：显示请求路径
		}
		logx.Event("%s → %d · %s · %s",
			endpoint, sw.status,
			time.Since(start).Round(time.Millisecond), r.RemoteAddr)
	})
}
