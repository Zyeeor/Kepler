// 限流与防滥用行为端到端测试：429 触发 + Retry-After 头、events 单请求批量上限、
// 防自刷（owner == reporter 跳过）、UGC 上传格式白名单（.json / JSON 内容 / PNG 魔数）。
//
// 自包含：测试内启动独立服务器实例（独立端口 + 临时数据库 + Cleanup 清理），
// 限流保持默认开启（其余三套测试显式关闭限流，行为由本包覆盖）。
// 运行：go test ./tests/ratelimittest（或根目录 go test ./...）
package ratelimittest

import (
	"bytes"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"os"
	"path/filepath"
	"strings"
	"testing"
	"time"

	"possession/server/internal/elite"
	"possession/server/internal/httpapi"
)

// ============================================================================
// 测试驱动器
// ============================================================================

var portSeq = 18107

func startServer(t *testing.T) string {
	t.Helper()
	port := portSeq
	portSeq--

	dir, err := os.MkdirTemp("", "rl-test-*")
	if err != nil {
		t.Fatalf("mktemp: %v", err)
	}
	srv, err := httpapi.New(httpapi.Config{
		HTTPAddr: fmt.Sprintf("127.0.0.1:%d", port),
		DBPath:   filepath.Join(dir, "test.db"),
		UploadDir: filepath.Join(dir, "ugc"),
		Elite:    elite.DefaultEliteConfig(),
		// DisableRateLimit 保持 false：本包专测限流与防滥用行为
	})
	if err != nil {
		t.Fatalf("start server: %v", err)
	}
	t.Cleanup(func() {
		srv.Close()
		_ = os.RemoveAll(dir)
	})
	go func() { _ = srv.Run() }()

	base := fmt.Sprintf("http://127.0.0.1:%d", port)
	for i := 0; i < 100; i++ {
		resp, err := http.Get(base + "/api/health")
		if err == nil {
			resp.Body.Close()
			return base
		}
		time.Sleep(50 * time.Millisecond)
	}
	t.Fatal("server not ready")
	return ""
}

// uploadCreation 上传 UGC 内容，返回 HTTP 状态码。
func uploadCreation(t *testing.T, base, fileName string, fileData, thumbnail []byte) int {
	t.Helper()
	body, _ := json.Marshal(map[string]any{
		"creatorId": "rl-creator",
		"type":      "map",
		"name":      "RL Map",
		"fileName":  fileName,
		"fileData":  fileData,
		"thumbnail": thumbnail,
	})
	resp, err := http.Post(base+"/api/creations", "application/json", bytes.NewReader(body))
	if err != nil {
		t.Fatalf("upload: %v", err)
	}
	defer resp.Body.Close()
	_, _ = io.Copy(io.Discard, resp.Body)
	return resp.StatusCode
}

// ============================================================================
// 限流：429 + Retry-After
// ============================================================================

// TestRateLimitTriggers429 连续超突发容量（ugc_action burst=30）的请求应触发 429，
// 且 429 响应携带 Retry-After 头与 errorResponse 体。
func TestRateLimitTriggers429(t *testing.T) {
	base := startServer(t)

	const total = 40 // burst=30，rate=1/s：本机快速连发应有 30+ 通过、1+ 被拒
	ok200, limited := 0, 0
	retryAfter := ""
	var errBody map[string]any
	for i := 0; i < total; i++ {
		resp, err := http.Post(base+"/api/creations/c-fake/subscribe", "application/json",
			strings.NewReader(`{"playerId":"rl-p1","subscribe":true}`))
		if err != nil {
			t.Fatalf("subscribe #%d: %v", i, err)
		}
		if resp.StatusCode == http.StatusTooManyRequests {
			limited++
			retryAfter = resp.Header.Get("Retry-After")
			_ = json.NewDecoder(resp.Body).Decode(&errBody)
		} else if resp.StatusCode == http.StatusOK {
			ok200++
		}
		_, _ = io.Copy(io.Discard, resp.Body)
		resp.Body.Close()
	}

	if ok200 < 30 {
		t.Fatalf("expected >=30 requests to pass (burst=30), got %d", ok200)
	}
	if limited < 1 {
		t.Fatalf("expected >=1 request to be rate limited (429), got 0 (200=%d)", ok200)
	}
	if retryAfter == "" {
		t.Fatal("429 response missing Retry-After header")
	}
	if code, _ := errBody["code"].(float64); code != 429 {
		t.Fatalf("429 response body code = %v, want 429 (body=%v)", errBody["code"], errBody)
	}
}

// ============================================================================
// events 防滥用
// ============================================================================

// TestEventsBatchLimit 单请求批量上限 50 条：超限整批 400 拒绝。
func TestEventsBatchLimit(t *testing.T) {
	base := startServer(t)

	events := make([]map[string]any, 51) // 上限 50
	for i := range events {
		events[i] = map[string]any{
			"ownerPlayerId": "rl-owner", "ownerRunId": "rl-run",
			"sin": "lust", "type": "fatal",
			"eventId": fmt.Sprintf("batch-%d", i),
		}
	}
	body, _ := json.Marshal(map[string]any{"playerId": "rl-reporter", "events": events})
	resp, err := http.Post(base+"/api/elite/events", "application/json", bytes.NewReader(body))
	if err != nil {
		t.Fatalf("events: %v", err)
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusBadRequest {
		t.Fatalf("batch of 51 events: status = %d, want 400", resp.StatusCode)
	}
	var out struct {
		Code int    `json:"code"`
		Msg  string `json:"msg"`
	}
	_ = json.NewDecoder(resp.Body).Decode(&out)
	if !strings.Contains(out.Msg, "too many events") {
		t.Fatalf("batch limit error msg = %q, want contains 'too many events'", out.Msg)
	}
}

// TestEventsSelfReportSkipped 防自刷：owner == reporter 的事件逐条跳过（accepted 只计正常条目）。
func TestEventsSelfReportSkipped(t *testing.T) {
	base := startServer(t)

	// 同批两条：self（owner == reporter，应跳过）+ 正常（owner != reporter，应接受）
	events := []map[string]any{
		{"ownerPlayerId": "rl-reporter", "ownerRunId": "rl-run", "sin": "lust", "type": "bodyFatal", "eventId": "self-1"},
		{"ownerPlayerId": "rl-owner", "ownerRunId": "rl-run", "sin": "lust", "type": "bodyFatal", "eventId": "ok-1"},
	}
	body, _ := json.Marshal(map[string]any{"playerId": "rl-reporter", "events": events})
	resp, err := http.Post(base+"/api/elite/events", "application/json", bytes.NewReader(body))
	if err != nil {
		t.Fatalf("events: %v", err)
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		t.Fatalf("events status = %d, want 200", resp.StatusCode)
	}
	var out struct {
		OK       bool `json:"ok"`
		Accepted int  `json:"accepted"`
	}
	if err := json.NewDecoder(resp.Body).Decode(&out); err != nil {
		t.Fatalf("decode: %v", err)
	}
	if out.Accepted != 1 {
		t.Fatalf("accepted = %d, want 1 (self-report should be skipped)", out.Accepted)
	}
}

// ============================================================================
// UGC 上传格式白名单
// ============================================================================

// TestUGCUploadWhitelist 上传收紧：.json 后缀、合法 JSON 内容、PNG 缩略图魔数。
func TestUGCUploadWhitelist(t *testing.T) {
	base := startServer(t)

	pngHeader := []byte{0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A}

	if code := uploadCreation(t, base, "map.txt", []byte(`{}`), nil); code != http.StatusBadRequest {
		t.Fatalf("non-.json fileName: status = %d, want 400", code)
	}
	if code := uploadCreation(t, base, "map.json", []byte(`not-json`), nil); code != http.StatusBadRequest {
		t.Fatalf("non-JSON fileData: status = %d, want 400", code)
	}
	if code := uploadCreation(t, base, "map.json", []byte(`{}`), []byte{0, 1, 2, 3}); code != http.StatusBadRequest {
		t.Fatalf("non-PNG thumbnail: status = %d, want 400", code)
	}
	if code := uploadCreation(t, base, "map.json", []byte(`{"kind":"rl"}`), pngHeader); code != http.StatusOK {
		t.Fatalf("valid upload: status = %d, want 200", code)
	}
}
