// UGC 内容服务测试：上传地图 → 列表查询 → 搜索 → 下载 → 订阅 → 评分。
// 自包含：测试内启动独立服务器实例（独立端口 + 临时数据库 + Cleanup 清理）。
// 运行：go test ./tests/ugctest（或根目录 go test ./...）
package ugctest

import (
	"bytes"
	"encoding/json"
	"fmt"
	"net/http"
	"os"
	"path/filepath"
	"testing"
	"time"

	"possession/server/internal/elite"
	"possession/server/internal/httpapi"
)

// ============================================================================
// 测试驱动器
// ============================================================================

var portSeq = 18109

func startServer(t *testing.T) string {
	t.Helper()
	port := portSeq
	portSeq--

	dir, err := os.MkdirTemp("", "ugc-test-*")
	if err != nil {
		t.Fatalf("mktemp: %v", err)
	}
	srv, err := httpapi.New(httpapi.Config{
		HTTPAddr:         fmt.Sprintf("127.0.0.1:%d", port),
		DBPath:           filepath.Join(dir, "test.db"),
		UploadDir:        filepath.Join(dir, "ugc"),
		Elite:            elite.DefaultEliteConfig(),
		DisableRateLimit: true, // 测试高频请求，关闭限流（限流行为由 ratelimittest 覆盖）
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

// check 校验响应状态码并解码 JSON。
func check(t *testing.T, resp *http.Response, out any) {
	t.Helper()
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusOK {
		t.Fatalf("unexpected status: %s", resp.Status)
	}
	if err := json.NewDecoder(resp.Body).Decode(out); err != nil {
		t.Fatalf("decode: %v", err)
	}
}

func TestUgcFlow(t *testing.T) {
	base := startServer(t)
	playerID := "utest-player-0001"

	// 1. 上传地图
	mapData := []byte(`{
		"version": "1.0",
		"mapId": "test_map_001",
		"name": "暗黑地牢",
		"size": {"width": 50, "height": 50},
		"tiles": [],
		"spawnPoints": [{"x": 10, "y": 10}],
		"difficulty": 3
	}`)

	uploadBody, _ := json.Marshal(map[string]any{
		"creatorId":   playerID,
		"creatorName": "Creator",
		"type":        "map",
		"name":        "暗黑地牢 v1.0",
		"description": "一个充满陷阱的地牢地图",
		"tags":        []string{"roguelike", "dungeon", "hard"},
		"fileName":    "map.json",
		"fileData":    mapData, // encoding/json 自动 base64
	})
	resp, err := http.Post(base+"/api/creations", "application/json", bytes.NewReader(uploadBody))
	if err != nil {
		t.Fatalf("upload: %v", err)
	}
	var upResp struct {
		CreationID string `json:"creationId"`
		FileURL    string `json:"fileUrl"`
	}
	check(t, resp, &upResp)
	if upResp.CreationID == "" {
		t.Fatal("upload should return a creationId")
	}
	t.Logf("✓ Upload OK, creationId: %s", upResp.CreationID)

	// 2. 列表查询
	resp, err = http.Get(base + "/api/creations?type=map&page=1&pageSize=10&sortBy=created_at&descending=true")
	if err != nil {
		t.Fatalf("list: %v", err)
	}
	var listResp struct {
		Creations []struct {
			Name        string  `json:"name"`
			CreatorName string  `json:"creatorName"`
			Downloads   int     `json:"downloads"`
			Rating      float64 `json:"rating"`
		} `json:"creations"`
		Total int `json:"total"`
	}
	check(t, resp, &listResp)
	if listResp.Total < 1 || len(listResp.Creations) < 1 {
		t.Fatalf("list should contain the uploaded creation, total=%d len=%d", listResp.Total, len(listResp.Creations))
	}
	c := listResp.Creations[0]
	t.Logf("✓ List OK, total=%d, top: %s by %s (downloads=%d, rating=%.1f)", listResp.Total, c.Name, c.CreatorName, c.Downloads, c.Rating)

	// 3. 搜索（中文关键词，顺带覆盖 P2 LIKE 转义路径）
	resp, err = http.Get(base + "/api/creations/search?keyword=%E5%9C%B0%E7%89%A2&type=map&page=1&pageSize=10")
	if err != nil {
		t.Fatalf("search: %v", err)
	}
	var searchResp struct {
		Total int `json:"total"`
	}
	check(t, resp, &searchResp)
	if searchResp.Total < 1 {
		t.Fatalf("search '地牢' should find the uploaded map, got total=%d", searchResp.Total)
	}
	t.Logf("✓ Search OK, found %d results for '地牢'", searchResp.Total)

	// 4. 下载
	resp, err = http.Get(base + "/api/creations/" + upResp.CreationID + "/download")
	if err != nil {
		t.Fatalf("download: %v", err)
	}
	var dlResp struct {
		Name     string `json:"name"`
		FileData []byte `json:"fileData"`
		Version  int    `json:"version"`
	}
	check(t, resp, &dlResp)
	if len(dlResp.FileData) == 0 {
		t.Fatal("download should return non-empty fileData")
	}
	t.Logf("✓ Download OK, name=%s, size=%d bytes, version=%d", dlResp.Name, len(dlResp.FileData), dlResp.Version)

	// 5. 订阅
	subBody, _ := json.Marshal(map[string]any{"playerId": playerID, "subscribe": true})
	resp, err = http.Post(base+"/api/creations/"+upResp.CreationID+"/subscribe", "application/json", bytes.NewReader(subBody))
	if err != nil {
		t.Fatalf("subscribe: %v", err)
	}
	var okResp struct {
		OK bool `json:"ok"`
	}
	check(t, resp, &okResp)
	if !okResp.OK {
		t.Fatal("subscribe should return ok=true")
	}
	t.Log("✓ Subscribe OK")

	// 6. 评分
	rateBody, _ := json.Marshal(map[string]any{"playerId": playerID, "rating": 5, "comment": "great map"})
	resp, err = http.Post(base+"/api/creations/"+upResp.CreationID+"/rate", "application/json", bytes.NewReader(rateBody))
	if err != nil {
		t.Fatalf("rate: %v", err)
	}
	check(t, resp, &okResp)
	if !okResp.OK {
		t.Fatal("rate should return ok=true")
	}
	t.Log("✓ Rate OK")
}
