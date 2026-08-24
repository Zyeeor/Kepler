// 精英怪战果回传端到端测试（策划案 §6.5）：事件上报 → 校验跳过 → 按构筑主人聚合 → 战绩查询。
//
// 自包含：测试内启动独立服务器实例（独立端口 + 临时数据库），无需预先 go run .
// 运行：go run ./test/eliteeventtest
package main

import (
	"bytes"
	"encoding/json"
	"fmt"
	"net/http"
	"os"
	"path/filepath"
	"time"

	"demo/server/internal/server"
	"demo/server/internal/service"
)

// ============================================================================
// 测试客户端（模拟前台：不 import 后台业务类型）
// ============================================================================

type snapIn struct {
	Sin         string          `json:"sin"`
	MonsterType string          `json:"monsterType"`
	BDCount     int             `json:"bdCount"`
	BDData      json.RawMessage `json:"bdData"`
	SourceWave  int             `json:"sourceWave"`
	GameTime    int64           `json:"gameTime"`
}

type pickSnapshot struct {
	SnapshotID     int64  `json:"snapshotId"`
	SourcePlayerID string `json:"sourcePlayerId"`
	RunID          string `json:"runId"`
	Sin            string `json:"sin"`
}

type pickResp struct {
	Snapshot *pickSnapshot `json:"snapshot"`
}

type eventIn struct {
	SnapshotID    int64  `json:"snapshotId"`
	OwnerPlayerID string `json:"ownerPlayerId"`
	OwnerRunID    string `json:"ownerRunId"`
	Sin           string `json:"sin"`
	Type          string `json:"type"`
	Wave          int    `json:"wave"`
}

func upload(base, player, run string, snaps ...snapIn) {
	body, _ := json.Marshal(map[string]any{
		"playerId":  player,
		"runId":     run,
		"snapshots": snaps,
	})
	resp, err := http.Post(base+"/api/bd-snapshots", "application/json", bytes.NewReader(body))
	if err != nil {
		panic(err)
	}
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusOK {
		panic(fmt.Sprintf("upload unexpected status: %s", resp.Status))
	}
}

func pick(base, player string, wave int) pickResp {
	body, _ := json.Marshal(map[string]any{"playerId": player, "wave": wave})
	resp, err := http.Post(base+"/api/elite/pick", "application/json", bytes.NewReader(body))
	if err != nil {
		panic(err)
	}
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusOK {
		panic(fmt.Sprintf("pick unexpected status: %s", resp.Status))
	}
	var out pickResp
	if err := json.NewDecoder(resp.Body).Decode(&out); err != nil {
		panic(err)
	}
	return out
}

// reportEvents 批量上报战果事件，返回 accepted。
func reportEvents(base, reporter string, events ...eventIn) int {
	body, _ := json.Marshal(map[string]any{
		"playerId": reporter,
		"events":   events,
	})
	resp, err := http.Post(base+"/api/elite/events", "application/json", bytes.NewReader(body))
	if err != nil {
		panic(err)
	}
	defer resp.Body.Close()
	var out struct {
		OK       bool `json:"ok"`
		Accepted int  `json:"accepted"`
	}
	if resp.StatusCode != http.StatusOK {
		panic(fmt.Sprintf("events unexpected status: %s", resp.Status))
	}
	if err := json.NewDecoder(resp.Body).Decode(&out); err != nil {
		panic(err)
	}
	return out.Accepted
}

type statsEntry struct {
	OwnerPlayerID string `json:"ownerPlayerId"`
	OwnerRunID    string `json:"ownerRunId"`
	Sin           string `json:"sin"`
	Deployed      int    `json:"deployed"`
	Fatal         int    `json:"fatal"`
	Possessed     int    `json:"possessed"`
	BodyFatal     int    `json:"bodyFatal"`
	RunFail       int    `json:"runFail"`
}

// fetchStats 查询构筑主人的战绩聚合。
func fetchStats(base, owner string) []statsEntry {
	resp, err := http.Get(base + "/api/elite/stats?playerId=" + owner)
	if err != nil {
		panic(err)
	}
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusOK {
		panic(fmt.Sprintf("stats unexpected status: %s", resp.Status))
	}
	var out struct {
		PlayerID string       `json:"playerId"`
		Stats    []statsEntry `json:"stats"`
	}
	if err := json.NewDecoder(resp.Body).Decode(&out); err != nil {
		panic(err)
	}
	return out.Stats
}

func bd(bdCount int) json.RawMessage {
	return json.RawMessage(fmt.Sprintf(`[{"cardId":"TEST-%03d","stack":1}]`, bdCount))
}

// ============================================================================
// 测试驱动器
// ============================================================================

var portSeq = 18089

func startServer() string {
	port := portSeq
	portSeq--

	dir, err := os.MkdirTemp("", "elite-event-test-*")
	if err != nil {
		panic(err)
	}
	srv, err := server.New(server.Config{
		HTTPAddr:  fmt.Sprintf("127.0.0.1:%d", port),
		DBPath:    filepath.Join(dir, "test.db"),
		UploadDir: filepath.Join(dir, "ugc"),
		Elite:     service.DefaultEliteConfig(),
	})
	if err != nil {
		panic(err)
	}
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
	panic("server not ready")
}

func main() {
	base := startServer()
	const playerA, playerB = "event-test-A", "event-test-B"

	// 1. A 上传快照 → B 投放命中（取 snapshotId / ownerRunId）
	upload(base, playerA, "run-A1", snapIn{Sin: "lust", MonsterType: "灵念师", BDCount: 2, BDData: bd(2), SourceWave: 6})
	r := pick(base, playerB, 5)
	if r.Snapshot == nil || r.Snapshot.SourcePlayerID != playerA {
		panic(fmt.Sprintf("pick should hit A's snapshot, got %+v", r.Snapshot))
	}
	snap := r.Snapshot

	// 2. B 回报 spawned + fatal → A 的聚合 deployed=1 / fatal=1
	if n := reportEvents(base, playerB,
		eventIn{SnapshotID: snap.SnapshotID, OwnerPlayerID: snap.SourcePlayerID, OwnerRunID: snap.RunID, Sin: snap.Sin, Type: "spawned", Wave: 5},
		eventIn{SnapshotID: snap.SnapshotID, OwnerPlayerID: snap.SourcePlayerID, OwnerRunID: snap.RunID, Sin: snap.Sin, Type: "fatal", Wave: 6},
	); n != 2 {
		panic(fmt.Sprintf("events accepted=%d, want 2", n))
	}
	stats := fetchStats(base, playerA)
	if len(stats) != 1 || stats[0].Deployed != 1 || stats[0].Fatal != 1 || stats[0].Possessed != 0 {
		panic(fmt.Sprintf("unexpected stats after spawned+fatal: %+v", stats))
	}
	fmt.Println("✓ 上报 spawned+fatal → 构筑主人聚合 deployed=1 fatal=1")

	// 3. 无主（本地 Preset）/ 非法 sin / 非法 type 逐条跳过，不整批失败
	if n := reportEvents(base, playerB,
		eventIn{OwnerPlayerID: "local-preset", OwnerRunID: "x", Sin: "lust", Type: "spawned"},
		eventIn{OwnerPlayerID: playerA, OwnerRunID: "run-A1", Sin: "not-a-sin", Type: "fatal"},
		eventIn{OwnerPlayerID: playerA, OwnerRunID: "run-A1", Sin: "lust", Type: "cheated"},
	); n != 0 {
		panic(fmt.Sprintf("invalid events accepted=%d, want 0", n))
	}
	stats = fetchStats(base, playerA)
	if len(stats) != 1 || stats[0].Deployed != 1 || stats[0].Fatal != 1 {
		panic(fmt.Sprintf("invalid events should not change stats: %+v", stats))
	}
	fmt.Println("✓ 无主/非法 sin/非法 type 逐条跳过，聚合不被污染")

	// 4. possessed + bodyFatal + runFail → 全计数器
	reportEvents(base, playerB,
		eventIn{OwnerPlayerID: snap.SourcePlayerID, OwnerRunID: snap.RunID, Sin: snap.Sin, Type: "possessed", Wave: 6},
		eventIn{OwnerPlayerID: snap.SourcePlayerID, OwnerRunID: snap.RunID, Sin: snap.Sin, Type: "bodyFatal", Wave: 6},
		eventIn{OwnerPlayerID: snap.SourcePlayerID, OwnerRunID: snap.RunID, Sin: snap.Sin, Type: "runFail", Wave: 7},
	)
	stats = fetchStats(base, playerA)
	if len(stats) != 1 || stats[0].Possessed != 1 || stats[0].BodyFatal != 1 || stats[0].RunFail != 1 {
		panic(fmt.Sprintf("unexpected stats after full lifecycle: %+v", stats))
	}
	fmt.Println("✓ possessed/bodyFatal/runFail → 五类计数器齐全")

	// 5. A 第二个 Sin 的快照独立聚合（同主人不同 (run, sin) 键）
	upload(base, playerA, "run-A2", snapIn{Sin: "wrath", MonsterType: "链狱冥兽", BDCount: 3, BDData: bd(3), SourceWave: 7})
	r = pick(base, playerB, 6)
	if r.Snapshot == nil || r.Snapshot.Sin != "wrath" {
		panic(fmt.Sprintf("pick should hit A's wrath snapshot, got %+v", r.Snapshot))
	}
	reportEvents(base, playerB,
		eventIn{OwnerPlayerID: r.Snapshot.SourcePlayerID, OwnerRunID: r.Snapshot.RunID, Sin: r.Snapshot.Sin, Type: "spawned", Wave: 6},
	)
	stats = fetchStats(base, playerA)
	if len(stats) != 2 {
		panic(fmt.Sprintf("owner should have 2 stat entries, got %d: %+v", len(stats), stats))
	}
	fmt.Println("✓ 同主人不同 (runId, sin) 独立聚合（荣誉殿堂按构筑维度展示）")

	fmt.Println("\n✅ All elite event tests passed!")
}
