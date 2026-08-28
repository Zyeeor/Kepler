// Run Analytics 对局数据上传端到端测试（RunStats_后端对接文档.md §4）：
// 上传 → 校验拒绝 → 同 runId 幂等覆盖（直连临时库验证行数与字段）。
//
// 自包含：测试内启动独立服务器实例（独立端口 + 临时数据库 + Cleanup 清理）。
// 运行：go test ./tests/runstatstest（或根目录 go test ./...）
package runstatstest

import (
	"bytes"
	"database/sql"
	"encoding/json"
	"fmt"
	"net/http"
	"os"
	"path/filepath"
	"testing"
	"time"

	_ "modernc.org/sqlite"

	"possession/server/internal/elite"
	"possession/server/internal/httpapi"
)

// ============================================================================
// 测试客户端（模拟前台：不 import 后台业务类型）
// ============================================================================

type perSinIn struct {
	Sin                 string  `json:"sin"`
	ControlSeconds      float64 `json:"controlSeconds"`
	PossessionCount     int     `json:"possessionCount"`
	MovementCount       int     `json:"movementCount"`
	AttackCount         int     `json:"attackCount"`
	SpecialCount        int     `json:"specialCount"`
	CardInvestmentCount int     `json:"cardInvestmentCount"`
	Kills               int     `json:"kills"`
}

// uploadRun 上传对局数据，返回 HTTP 状态码。
func uploadRun(t *testing.T, base string, body map[string]any) int {
	t.Helper()
	raw, _ := json.Marshal(body)
	resp, err := http.Post(base+"/api/runs", "application/json", bytes.NewReader(raw))
	if err != nil {
		t.Fatalf("uploadRun: %v", err)
	}
	defer resp.Body.Close()
	var out struct {
		OK  bool   `json:"ok"`
		Err string `json:"error,omitempty"`
	}
	_ = json.NewDecoder(resp.Body).Decode(&out)
	return resp.StatusCode
}

// validBody 一份合法的完整请求体（按需覆盖字段）。
func validBody() map[string]any {
	return map[string]any{
		"schemaVersion": 1,
		"runId":         "run-R1",
		"playerId":      "runstats-test-A",
		"startedAtUnix": 1787800000,
		"endedAtUnix":   1787801020,
		"runDurationSeconds": 1020.5,
		"won":                true,
		"endPhase":           "Result",
		"reachedWaveIndex":   7,
		"finalReached":       true,
		"finalCompleted":     true,
		"totalPossessions":   12,
		"voluntaryReleases":  4,
		"deathRelays":        0,
		"soulEnters":         5,
		"shrineRecovers":     0,
		"lowHealthReleases":  1,
		"bulletTimeCount":    3,
		"bulletTimeTotalSeconds": 6.0,
		"eliteFatalCount":        2,
		"elitePossessionCount":   1,
		"distinctSinsUsed":       2,
		"totalKills":             38,
		"perSin": []perSinIn{
			{Sin: "gluttony", ControlSeconds: 320, PossessionCount: 4, MovementCount: 12, AttackCount: 45, SpecialCount: 8, CardInvestmentCount: 3, Kills: 15},
			{Sin: "lust", ControlSeconds: 180, PossessionCount: 3, MovementCount: 6, AttackCount: 20, SpecialCount: 5, CardInvestmentCount: 2, Kills: 9},
		},
	}
}

// ============================================================================
// 测试驱动器
// ============================================================================

var portSeq = 18079

func startServer(t *testing.T) (base, dbPath string) {
	t.Helper()
	port := portSeq
	portSeq--

	dir, err := os.MkdirTemp("", "runstats-test-*")
	if err != nil {
		t.Fatalf("mktemp: %v", err)
	}
	dbPath = filepath.Join(dir, "test.db")
	srv, err := httpapi.New(httpapi.Config{
		HTTPAddr:         fmt.Sprintf("127.0.0.1:%d", port),
		DBPath:           dbPath,
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

	base = fmt.Sprintf("http://127.0.0.1:%d", port)
	for i := 0; i < 100; i++ {
		resp, err := http.Get(base + "/api/health")
		if err == nil {
			resp.Body.Close()
			return base, dbPath
		}
		time.Sleep(50 * time.Millisecond)
	}
	t.Fatal("server not ready")
	return "", ""
}

// openDB 直连临时库（WAL 模式外部访问，busy_timeout 容忍与服务器单连接的短暂竞争）。
func openDB(t *testing.T, dbPath string) *sql.DB {
	t.Helper()
	db, err := sql.Open("sqlite", dbPath+"?_pragma=busy_timeout(5000)")
	if err != nil {
		t.Fatalf("open db: %v", err)
	}
	t.Cleanup(func() { _ = db.Close() })
	return db
}

// ============================================================================
// 用例
// ============================================================================

func TestRunStatsUploadFlow(t *testing.T) {
	base, dbPath := startServer(t)

	// 1. 正常上传 → 200
	if code := uploadRun(t, base, validBody()); code != http.StatusOK {
		t.Fatalf("upload should be 200, got %d", code)
	}

	// 2. 校验拒绝 → 400（缺身份 / 非法 sin / 重复 sin / 超条数）
	noPlayer := validBody()
	delete(noPlayer, "playerId")
	if code := uploadRun(t, base, noPlayer); code != http.StatusBadRequest {
		t.Fatalf("missing playerId should be 400, got %d", code)
	}

	badSin := validBody()
	badSin["runId"] = "run-R2"
	badSin["perSin"] = []perSinIn{{Sin: "greed-is-not-a-sin", Kills: 1}}
	if code := uploadRun(t, base, badSin); code != http.StatusBadRequest {
		t.Fatalf("unknown sin should be 400, got %d", code)
	}

	dupSin := validBody()
	dupSin["runId"] = "run-R3"
	dupSin["perSin"] = []perSinIn{{Sin: "lust"}, {Sin: "lust"}}
	if code := uploadRun(t, base, dupSin); code != http.StatusBadRequest {
		t.Fatalf("duplicate sin should be 400, got %d", code)
	}

	tooMany := validBody()
	tooMany["runId"] = "run-R4"
	tooMany["perSin"] = []perSinIn{
		{Sin: "pride"}, {Sin: "sloth"}, {Sin: "gluttony"}, {Sin: "envy"},
		{Sin: "wrath"}, {Sin: "greed"}, {Sin: "lust"}, {Sin: "pride"},
	}
	if code := uploadRun(t, base, tooMany); code != http.StatusBadRequest {
		t.Fatalf("too many perSin should be 400, got %d", code)
	}

	// 3. 幂等：同 runId 重传（数据变化 + perSin 收敛为 1 条）→ 200，且库内不产生重复行
	rewrite := validBody()
	rewrite["totalKills"] = 40
	rewrite["perSin"] = []perSinIn{{Sin: "gluttony", Kills: 40}}
	if code := uploadRun(t, base, rewrite); code != http.StatusOK {
		t.Fatalf("re-upload should be 200 (idempotent), got %d", code)
	}

	db := openDB(t, dbPath)
	var runs, perSin, kills int
	if err := db.QueryRow(`SELECT COUNT(*) FROM run_stats`).Scan(&runs); err != nil {
		t.Fatalf("count run_stats: %v", err)
	}
	if runs != 1 {
		t.Fatalf("run_stats should have exactly 1 row after re-upload, got %d", runs)
	}
	if err := db.QueryRow(`SELECT COUNT(*) FROM run_stats_per_sin`).Scan(&perSin); err != nil {
		t.Fatalf("count run_stats_per_sin: %v", err)
	}
	if perSin != 1 {
		t.Fatalf("per_sin should be overwritten to 1 row, got %d", perSin)
	}
	if err := db.QueryRow(`SELECT total_kills FROM run_stats WHERE run_id='run-R1'`).Scan(&kills); err != nil {
		t.Fatalf("read total_kills: %v", err)
	}
	if kills != 40 {
		t.Fatalf("total_kills should be overwritten to 40, got %d", kills)
	}
}
