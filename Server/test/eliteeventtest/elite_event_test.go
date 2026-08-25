// 精英怪战果回传端到端测试（策划案 §6.5）：事件上报 → 校验跳过 → 按构筑主人聚合 → 战绩查询。
//
// 自包含：测试内启动独立服务器实例（独立端口 + 临时数据库 + Cleanup 清理）。
// 运行：go test ./test/eliteeventtest（或根目录 go test ./...）
package eliteeventtest

import (
	"bytes"
	"encoding/json"
	"fmt"
	"net/http"
	"os"
	"path/filepath"
	"testing"
	"time"

	"demo/server/elite"
	"demo/server/server"
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
	EventID       string `json:"eventId,omitempty"` // 幂等去重键（空 = 旧客户端兼容路径）
}

func upload(t *testing.T, base, player, run string, snaps ...snapIn) {
	t.Helper()
	body, _ := json.Marshal(map[string]any{
		"playerId":  player,
		"runId":     run,
		"snapshots": snaps,
	})
	resp, err := http.Post(base+"/api/bd-snapshots", "application/json", bytes.NewReader(body))
	if err != nil {
		t.Fatalf("upload: %v", err)
	}
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusOK {
		t.Fatalf("upload unexpected status: %s", resp.Status)
	}
}

func pick(t *testing.T, base, player string, wave int) pickResp {
	t.Helper()
	body, _ := json.Marshal(map[string]any{"playerId": player, "wave": wave})
	resp, err := http.Post(base+"/api/elite/pick", "application/json", bytes.NewReader(body))
	if err != nil {
		t.Fatalf("pick: %v", err)
	}
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusOK {
		t.Fatalf("pick unexpected status: %s", resp.Status)
	}
	var out pickResp
	if err := json.NewDecoder(resp.Body).Decode(&out); err != nil {
		t.Fatalf("pick decode: %v", err)
	}
	return out
}

// reportEvents 批量上报战果事件，返回 accepted。
func reportEvents(t *testing.T, base, reporter string, events ...eventIn) int {
	t.Helper()
	body, _ := json.Marshal(map[string]any{
		"playerId": reporter,
		"events":   events,
	})
	resp, err := http.Post(base+"/api/elite/events", "application/json", bytes.NewReader(body))
	if err != nil {
		t.Fatalf("reportEvents: %v", err)
	}
	defer resp.Body.Close()
	var out struct {
		OK       bool `json:"ok"`
		Accepted int  `json:"accepted"`
	}
	if resp.StatusCode != http.StatusOK {
		t.Fatalf("events unexpected status: %s", resp.Status)
	}
	if err := json.NewDecoder(resp.Body).Decode(&out); err != nil {
		t.Fatalf("events decode: %v", err)
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
func fetchStats(t *testing.T, base, owner string) []statsEntry {
	t.Helper()
	resp, err := http.Get(base + "/api/elite/stats?playerId=" + owner)
	if err != nil {
		t.Fatalf("fetchStats: %v", err)
	}
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusOK {
		t.Fatalf("stats unexpected status: %s", resp.Status)
	}
	var out struct {
		PlayerID string       `json:"playerId"`
		Stats    []statsEntry `json:"stats"`
	}
	if err := json.NewDecoder(resp.Body).Decode(&out); err != nil {
		t.Fatalf("stats decode: %v", err)
	}
	return out.Stats
}

type leaderboardEntry struct {
	Rank          int             `json:"rank"`
	SnapshotID    int64           `json:"snapshotId"`
	OwnerPlayerID string          `json:"ownerPlayerId"`
	OwnerRunID    string          `json:"ownerRunId"`
	Sin           string          `json:"sin"`
	MonsterType   string          `json:"monsterType"`
	BDCount       int             `json:"bdCount"`
	BDData        json.RawMessage `json:"bdData"`
	Stats         struct {
		Deployed  int `json:"deployed"`
		BodyFatal int `json:"bodyFatal"`
	} `json:"stats"`
}

// fetchLeaderboard 查询荣誉殿堂排行榜（limit<=0 表示省略参数，走服务端默认）。
func fetchLeaderboard(t *testing.T, base string, limit int) []leaderboardEntry {
	t.Helper()
	url := base + "/api/elite/leaderboard"
	if limit > 0 {
		url += fmt.Sprintf("?limit=%d", limit)
	}
	resp, err := http.Get(url)
	if err != nil {
		t.Fatalf("fetchLeaderboard: %v", err)
	}
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusOK {
		t.Fatalf("leaderboard unexpected status: %s", resp.Status)
	}
	var out struct {
		Entries []leaderboardEntry `json:"entries"`
	}
	if err := json.NewDecoder(resp.Body).Decode(&out); err != nil {
		t.Fatalf("leaderboard decode: %v", err)
	}
	return out.Entries
}

func bd(bdCount int) json.RawMessage {
	return json.RawMessage(fmt.Sprintf(`[{"cardId":"TEST-%03d","stack":1}]`, bdCount))
}

// ============================================================================
// 测试驱动器
// ============================================================================

var portSeq = 18089

func startServer(t *testing.T) string {
	t.Helper()
	port := portSeq
	portSeq--

	dir, err := os.MkdirTemp("", "elite-event-test-*")
	if err != nil {
		t.Fatalf("mktemp: %v", err)
	}
	srv, err := server.New(server.Config{
		HTTPAddr:  fmt.Sprintf("127.0.0.1:%d", port),
		DBPath:    filepath.Join(dir, "test.db"),
		UploadDir: filepath.Join(dir, "ugc"),
		Elite:     elite.DefaultEliteConfig(),
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

func TestEliteEventFlow(t *testing.T) {
	base := startServer(t)
	const playerA, playerB = "event-test-A", "event-test-B"

	// 1. A 上传快照 → B 投放命中（取 snapshotId / ownerRunId）
	upload(t, base, playerA, "run-A1", snapIn{Sin: "lust", MonsterType: "灵念师", BDCount: 2, BDData: bd(2), SourceWave: 6})
	r := pick(t, base, playerB, 5)
	if r.Snapshot == nil || r.Snapshot.SourcePlayerID != playerA {
		t.Fatalf("pick should hit A's snapshot, got %+v", r.Snapshot)
	}
	snap := r.Snapshot

	// 2. B 回报 spawned + fatal → A 的聚合 deployed=1 / fatal=1
	if n := reportEvents(t, base, playerB,
		eventIn{SnapshotID: snap.SnapshotID, OwnerPlayerID: snap.SourcePlayerID, OwnerRunID: snap.RunID, Sin: snap.Sin, Type: "spawned", Wave: 5},
		eventIn{SnapshotID: snap.SnapshotID, OwnerPlayerID: snap.SourcePlayerID, OwnerRunID: snap.RunID, Sin: snap.Sin, Type: "fatal", Wave: 6},
	); n != 2 {
		t.Fatalf("events accepted=%d, want 2", n)
	}
	stats := fetchStats(t, base, playerA)
	if len(stats) != 1 || stats[0].Deployed != 1 || stats[0].Fatal != 1 || stats[0].Possessed != 0 {
		t.Fatalf("unexpected stats after spawned+fatal: %+v", stats)
	}
	t.Log("✓ 上报 spawned+fatal → 构筑主人聚合 deployed=1 fatal=1")

	// 3. 无主（本地 Preset）/ 非法 sin / 非法 type 逐条跳过，不整批失败
	if n := reportEvents(t, base, playerB,
		eventIn{OwnerPlayerID: "local-preset", OwnerRunID: "x", Sin: "lust", Type: "spawned"},
		eventIn{OwnerPlayerID: playerA, OwnerRunID: "run-A1", Sin: "not-a-sin", Type: "fatal"},
		eventIn{OwnerPlayerID: playerA, OwnerRunID: "run-A1", Sin: "lust", Type: "cheated"},
	); n != 0 {
		t.Fatalf("invalid events accepted=%d, want 0", n)
	}
	stats = fetchStats(t, base, playerA)
	if len(stats) != 1 || stats[0].Deployed != 1 || stats[0].Fatal != 1 {
		t.Fatalf("invalid events should not change stats: %+v", stats)
	}
	t.Log("✓ 无主/非法 sin/非法 type 逐条跳过，聚合不被污染")

	// 4. possessed + bodyFatal + runFail → 全计数器
	reportEvents(t, base, playerB,
		eventIn{OwnerPlayerID: snap.SourcePlayerID, OwnerRunID: snap.RunID, Sin: snap.Sin, Type: "possessed", Wave: 6},
		eventIn{OwnerPlayerID: snap.SourcePlayerID, OwnerRunID: snap.RunID, Sin: snap.Sin, Type: "bodyFatal", Wave: 6},
		eventIn{OwnerPlayerID: snap.SourcePlayerID, OwnerRunID: snap.RunID, Sin: snap.Sin, Type: "runFail", Wave: 7},
	)
	stats = fetchStats(t, base, playerA)
	if len(stats) != 1 || stats[0].Possessed != 1 || stats[0].BodyFatal != 1 || stats[0].RunFail != 1 {
		t.Fatalf("unexpected stats after full lifecycle: %+v", stats)
	}
	t.Log("✓ possessed/bodyFatal/runFail → 五类计数器齐全")

	// 5. A 第二个 Sin 的快照独立聚合（同主人不同 (run, sin) 键）
	upload(t, base, playerA, "run-A2", snapIn{Sin: "wrath", MonsterType: "链狱冥兽", BDCount: 3, BDData: bd(3), SourceWave: 7})
	r = pick(t, base, playerB, 6)
	if r.Snapshot == nil || r.Snapshot.Sin != "wrath" {
		t.Fatalf("pick should hit A's wrath snapshot, got %+v", r.Snapshot)
	}
	reportEvents(t, base, playerB,
		eventIn{OwnerPlayerID: r.Snapshot.SourcePlayerID, OwnerRunID: r.Snapshot.RunID, Sin: r.Snapshot.Sin, Type: "spawned", Wave: 6},
	)
	stats = fetchStats(t, base, playerA)
	if len(stats) != 2 {
		t.Fatalf("owner should have 2 stat entries, got %d: %+v", len(stats), stats)
	}
	t.Log("✓ 同主人不同 (runId, sin) 独立聚合（荣誉殿堂按构筑维度展示）")

	// 6. 荣誉殿堂排行榜：击杀玩家次数（bodyFatal）最多的 Top N BD 怪物
	// 6.1 无击杀的条目不上榜：当前只有 run-A1/lust 有 bodyFatal=1
	lb := fetchLeaderboard(t, base, 0) // limit 省略 → 服务端默认 20
	if len(lb) != 1 || lb[0].Sin != "lust" || lb[0].Stats.BodyFatal != 1 {
		t.Fatalf("leaderboard should only list lust (bodyFatal=1): %+v", lb)
	}
	t.Log("✓ 排行榜只上有击杀的条目（bodyFatal>0）")

	// 6.2 wrath 追加 3 次击杀 → 反超 lust 登顶（按 bodyFatal 降序，条目含怪物与 BD 数据）
	reportEvents(t, base, playerB,
		eventIn{OwnerPlayerID: playerA, OwnerRunID: "run-A2", Sin: "wrath", Type: "bodyFatal", Wave: 6},
		eventIn{OwnerPlayerID: playerA, OwnerRunID: "run-A2", Sin: "wrath", Type: "bodyFatal", Wave: 6},
		eventIn{OwnerPlayerID: playerA, OwnerRunID: "run-A2", Sin: "wrath", Type: "bodyFatal", Wave: 7},
	)
	lb = fetchLeaderboard(t, base, 20)
	if len(lb) != 2 || lb[0].Sin != "wrath" || lb[0].Stats.BodyFatal != 3 || lb[1].Sin != "lust" {
		t.Fatalf("leaderboard order wrong (want wrath=3 > lust=1): %+v", lb)
	}
	if lb[0].Rank != 1 || lb[0].MonsterType != "链狱冥兽" || lb[0].BDCount != 3 || len(lb[0].BDData) == 0 {
		t.Fatalf("leaderboard entry missing BD info: %+v", lb[0])
	}
	t.Log("✓ 排行榜按击杀玩家次数降序，条目含怪物与 BD 数据")

	// 6.3 limit 截断：limit=1 只取榜首
	lb = fetchLeaderboard(t, base, 1)
	if len(lb) != 1 || lb[0].Sin != "wrath" {
		t.Fatalf("leaderboard limit=1 should return only wrath: %+v", lb)
	}
	t.Log("✓ 排行榜 limit 参数生效")

	// 7. 事件幂等去重（P1）：eventId 窗口内重发 → 跳过，聚合不重复计数
	dup := eventIn{SnapshotID: snap.SnapshotID, OwnerPlayerID: snap.SourcePlayerID, OwnerRunID: snap.RunID, Sin: snap.Sin, Type: "fatal", Wave: 6, EventID: "evt-dup-001"}
	if n := reportEvents(t, base, playerB, dup); n != 1 {
		t.Fatalf("first send accepted=%d, want 1", n)
	}
	fatalBefore := fetchStats(t, base, playerA)[0].Fatal
	if n := reportEvents(t, base, playerB, dup); n != 0 {
		t.Fatalf("duplicate send accepted=%d, want 0", n)
	}
	if fatalAfter := fetchStats(t, base, playerA)[0].Fatal; fatalAfter != fatalBefore {
		t.Fatalf("duplicate event changed fatal count: %d -> %d", fatalBefore, fatalAfter)
	}
	if n := reportEvents(t, base, playerB, eventIn{SnapshotID: snap.SnapshotID, OwnerPlayerID: snap.SourcePlayerID, OwnerRunID: snap.RunID, Sin: snap.Sin, Type: "fatal", Wave: 6, EventID: "evt-distinct-002"}); n != 1 {
		t.Fatalf("distinct eventId accepted=%d, want 1", n)
	}
	t.Log("✓ 事件幂等去重：eventId 重复跳过、新 eventId 正常计数")
}
