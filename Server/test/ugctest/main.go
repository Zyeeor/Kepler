// UGC 内容服务测试：上传地图 → 列表查询 → 搜索 → 下载 → 订阅 → 评分。
// 先启动服务器（go run .），再运行本测试（go run ./test/ugctest）。
package main

import (
	"bytes"
	"encoding/json"
	"fmt"
	"net/http"
)

const base = "http://localhost:8080"

func main() {
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
		panic(err)
	}
	var upResp struct {
		CreationID string `json:"creationId"`
		FileURL    string `json:"fileUrl"`
	}
	check(resp, &upResp)
	fmt.Println("✓ Upload OK, creationId:", upResp.CreationID)

	// 2. 列表查询
	resp, err = http.Get(base + "/api/creations?type=map&page=1&pageSize=10&sortBy=created_at&descending=true")
	if err != nil {
		panic(err)
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
	check(resp, &listResp)
	fmt.Printf("✓ List OK, total=%d, got %d creations\n", listResp.Total, len(listResp.Creations))
	if len(listResp.Creations) > 0 {
		c := listResp.Creations[0]
		fmt.Printf("  - %s by %s (downloads=%d, rating=%.1f)\n", c.Name, c.CreatorName, c.Downloads, c.Rating)
	}

	// 3. 搜索
	resp, err = http.Get(base + "/api/creations/search?keyword=%E5%9C%B0%E7%89%A2&type=map&page=1&pageSize=10")
	if err != nil {
		panic(err)
	}
	var searchResp struct {
		Total int `json:"total"`
	}
	check(resp, &searchResp)
	fmt.Printf("✓ Search OK, found %d results for '地牢'\n", searchResp.Total)

	// 4. 下载
	resp, err = http.Get(base + "/api/creations/" + upResp.CreationID + "/download")
	if err != nil {
		panic(err)
	}
	var dlResp struct {
		Name     string `json:"name"`
		FileData []byte `json:"fileData"`
		Version  int    `json:"version"`
	}
	check(resp, &dlResp)
	fmt.Printf("✓ Download OK, name=%s, size=%d bytes, version=%d\n", dlResp.Name, len(dlResp.FileData), dlResp.Version)

	// 5. 订阅
	subBody, _ := json.Marshal(map[string]any{"playerId": playerID, "subscribe": true})
	resp, err = http.Post(base+"/api/creations/"+upResp.CreationID+"/subscribe", "application/json", bytes.NewReader(subBody))
	if err != nil {
		panic(err)
	}
	var okResp struct {
		OK bool `json:"ok"`
	}
	check(resp, &okResp)
	fmt.Println("✓ Subscribe OK:", okResp.OK)

	// 6. 评分
	rateBody, _ := json.Marshal(map[string]any{"playerId": playerID, "rating": 5, "comment": "great map"})
	resp, err = http.Post(base+"/api/creations/"+upResp.CreationID+"/rate", "application/json", bytes.NewReader(rateBody))
	if err != nil {
		panic(err)
	}
	check(resp, &okResp)
	fmt.Println("✓ Rate OK:", okResp.OK)

	fmt.Println("\n✅ All UGC tests passed!")
}

// check 校验响应状态码并解码 JSON。
func check(resp *http.Response, out any) {
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusOK {
		panic(fmt.Sprintf("unexpected status: %s", resp.Status))
	}
	if err := json.NewDecoder(resp.Body).Decode(out); err != nil {
		panic(err)
	}
}
