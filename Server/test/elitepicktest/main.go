// 精英怪投放端到端测试：上传 upsert → 四步筛选 → 三级兜底 → 玩家隔离
// → TOP_BAND 双模式 → 每玩家上限 / 全局 FIFO。
//
// 自包含：测试内启动独立服务器实例（独立端口 + 临时数据库），无需预先 go run .
// 运行：go run ./test/elitepicktest
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
	SnapshotID     int64           `json:"snapshotId"`
	SourcePlayerID string          `json:"sourcePlayerId"`
	Sin            string          `json:"sin"`
	MonsterType    string          `json:"monsterType"`
	BDCount        int             `json:"bdCount"`
	SourceWave     int             `json:"sourceWave"`
	BDData         json.RawMessage `json:"bdData"`
}

type pickResp struct {
	Snapshot *pickSnapshot `json:"snapshot"`
	Relaxed  bool          `json:"relaxed"`
}

// upload 模拟每波选卡后的批量滚动上传，返回 accepted。
func upload(base, player, run string, snaps ...snapIn) int {
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
	var out struct {
		OK       bool `json:"ok"`
		Accepted int  `json:"accepted"`
	}
	if resp.StatusCode != http.StatusOK {
		panic(fmt.Sprintf("upload unexpected status: %s", resp.Status))
	}
	if err := json.NewDecoder(resp.Body).Decode(&out); err != nil {
		panic(err)
	}
	return out.Accepted
}

// pick 模拟第 N 波精英怪请求（waveGap=1：越一级波次差——测试各步断言按 WAVE_GAP=1 语义编写；
// 当前设计 waveGap 完全由客户端指定，服务端默认 0 不再叠加，故请求须显式携带）。
func pick(base, player string, wave int) pickResp {
	body, _ := json.Marshal(map[string]any{"playerId": player, "wave": wave, "waveGap": 1})
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

func bd(bdCount int) json.RawMessage {
	// 模拟「卡 ID + 层数」的 bdData（结构由前台定义，后台不解析）
	return json.RawMessage(fmt.Sprintf(`[{"cardId":"TEST-%03d","stack":1}]`, bdCount))
}

// ============================================================================
// 测试驱动器
// ============================================================================

var portSeq = 18099

// startServer 在测试进程内启动一个独立服务器实例，返回 base URL。
func startServer(elite service.EliteConfig) string {
	port := portSeq
	portSeq--

	dir, err := os.MkdirTemp("", "elite-test-*")
	if err != nil {
		panic(err)
	}
	srv, err := server.New(server.Config{
		HTTPAddr:  fmt.Sprintf("127.0.0.1:%d", port),
		DBPath:    filepath.Join(dir, "test.db"),
		UploadDir: filepath.Join(dir, "ugc"),
		Elite:     elite,
	})
	if err != nil {
		panic(err)
	}
	go func() { _ = srv.Run() }()

	base := fmt.Sprintf("http://127.0.0.1:%d", port)
	for i := 0; i < 100; i++ {
		resp, err := http.Get(base + "/api/creations?page=1&pageSize=1")
		if err == nil {
			resp.Body.Close()
			return base
		}
		time.Sleep(50 * time.Millisecond)
	}
	panic("server not ready")
}

func main() {
	testPickAndFallback()
	testCapacity()
	testTopKMode()

	fmt.Println("\n✅ All elite pick tests passed!")
}

// testPickAndFallback 默认配置（percent 0.2）：筛选主路径 / 隔离 / 三级兜底 / upsert / 防御。
func testPickAndFallback() {
	base := startServer(service.DefaultEliteConfig())
	const playerA, playerB = "elite-test-A", "elite-test-B"

	// 1. 空库：兜底 3（本波不投放）
	if r := pick(base, playerB, 5); r.Snapshot != nil {
		panic("empty store should return null snapshot")
	}
	fmt.Println("✓ 兜底3：空库返回 null（本波不投放）")

	// 2. bdCount=0 条目被防御性跳过
	n := upload(base, playerA, "run-1", snapIn{Sin: "lust", MonsterType: "灵念师", BDCount: 0, BDData: bd(0), SourceWave: 6})
	if n != 0 {
		panic(fmt.Sprintf("bdCount=0 entries should be skipped, accepted=%d", n))
	}
	fmt.Println("✓ 防御：bdCount=0 条目不入库")

	// 3. 玩家 A 上传一条 wave6/bd2 快照
	if n := upload(base, playerA, "run-1", snapIn{Sin: "lust", MonsterType: "灵念师", BDCount: 2, BDData: bd(2), SourceWave: 6, GameTime: 300}); n != 1 {
		panic(fmt.Sprintf("upload accepted=%d, want 1", n))
	}

	// 4. 玩家隔离：A 自己 pick 不到自己的快照（库里仅有 A 的数据）
	if r := pick(base, playerA, 5); r.Snapshot != nil {
		panic("self pick should be isolated")
	}
	fmt.Println("✓ Step3 玩家隔离：请求者拿不到自己的快照")

	// 5. 主路径：B 第 5 波 → 命中 A 的 wave6 快照（WAVE_GAP=1 → sourceWave>=6）
	r := pick(base, playerB, 5)
	if r.Snapshot == nil || r.Relaxed {
		panic("main path should hit A's snapshot without relax")
	}
	if r.Snapshot.SourcePlayerID != playerA || r.Snapshot.SourceWave != 6 || r.Snapshot.BDCount != 2 {
		panic(fmt.Sprintf("main path unexpected snapshot: %+v", r.Snapshot))
	}
	fmt.Println("✓ 主路径：B@W5 拿到 A 的 W6 快照（他人 + 波次差 + bdCount>=MIN_BD）")

	// 6. 兜底 1：B 第 6 波 → 主路径 sourceWave>=7 为空 → 放宽 WAVE_GAP=0 命中 wave6
	if r := pick(base, playerB, 6); r.Snapshot == nil || !r.Relaxed {
		panic("fallback 1 should hit with relaxed=true")
	}
	fmt.Println("✓ 兜底1：WAVE_GAP 放宽到 0，relaxed=true")

	// 7. 兜底 2：B 第 8 波 → 放宽后仍空（6<8）→ 全库最高波次档取 bdCount 最大
	if r := pick(base, playerB, 8); r.Snapshot == nil || !r.Relaxed || r.Snapshot.SourceWave != 6 {
		panic(fmt.Sprintf("fallback 2 unexpected: %+v", r.Snapshot))
	}
	fmt.Println("✓ 兜底2：sourceWave 最高档中 bdCount 最大")

	// 8. upsert：A 同 (run, sin) 再上传更深版本（wave7/bd3）→ 覆盖旧版本
	upload(base, playerA, "run-1", snapIn{Sin: "lust", MonsterType: "灵念师", BDCount: 3, BDData: bd(3), SourceWave: 7, GameTime: 400})
	r = pick(base, playerB, 6) // 主路径 sourceWave >= 7 → 只可能是新版本
	if r.Snapshot == nil || r.Relaxed || r.Snapshot.SourceWave != 7 || r.Snapshot.BDCount != 3 {
		panic(fmt.Sprintf("upsert should overwrite with wave7/bd3, got %+v", r.Snapshot))
	}
	fmt.Println("✓ upsert：同 (playerId, runId, sin) 后波覆盖前波")

	// 9. TOP_BAND percent：候选 5 条 → band=ceil(5*0.2)=1 → 必返回 bdCount 最高
	upload(base, "elite-test-C", "run-C",
		snapIn{Sin: "wrath", MonsterType: "链狱冥兽", BDCount: 1, BDData: bd(1), SourceWave: 10},
		snapIn{Sin: "greed", MonsterType: "万手藏主", BDCount: 2, BDData: bd(2), SourceWave: 10},
		snapIn{Sin: "sloth", MonsterType: "机械之灵", BDCount: 4, BDData: bd(4), SourceWave: 10},
		snapIn{Sin: "envy", MonsterType: "激光异形", BDCount: 5, BDData: bd(5), SourceWave: 10},
	)
	// 候选 = A(wave7/bd3) + C×4(wave10/bd 1,2,4,5) = 5 条
	for i := 0; i < 10; i++ {
		r := pick(base, playerB, 5) // minWave=6 → 5 条候选
		if r.Snapshot == nil || r.Snapshot.BDCount != 5 {
			panic(fmt.Sprintf("TOP_BAND percent should always pick bdCount=5, got %+v", r.Snapshot))
		}
	}
	fmt.Println("✓ TOP_BAND percent：band=1 时必取 bdCount 最高（多样性由 band>1 的加权随机保证）")
}

// testCapacity 容量治理：每玩家上限 + 全局 FIFO + upsert 不占新额度。
func testCapacity() {
	cfg := service.DefaultEliteConfig()
	cfg.MaxSnapshotsPerPlayer = 2
	cfg.MaxSnapshots = 3
	base := startServer(cfg)
	const playerE, playerF, playerG, playerH = "cap-E", "cap-F", "cap-G", "cap-H"

	// 1. 每玩家上限=2：E 上传 3 条 → FIFO 淘汰最早的 sin1(bd9)
	upload(base, playerE, "run-E",
		snapIn{Sin: "sin1", MonsterType: "m1", BDCount: 9, BDData: bd(9), SourceWave: 10},
		snapIn{Sin: "sin2", MonsterType: "m2", BDCount: 1, BDData: bd(1), SourceWave: 10},
		snapIn{Sin: "sin3", MonsterType: "m3", BDCount: 1, BDData: bd(1), SourceWave: 10},
	)
	// 候选 2 条（band=ceil(2*0.2)=1）→ 返回 bd 最高；若 sin1 未被淘汰则返回 bd9
	for i := 0; i < 10; i++ {
		r := pick(base, playerF, 1)
		if r.Snapshot == nil || r.Snapshot.BDCount != 1 || r.Snapshot.Sin == "sin1" {
			panic(fmt.Sprintf("per-player trim failed, got %+v", r.Snapshot))
		}
	}
	fmt.Println("✓ 每玩家上限：超出后 FIFO 淘汰该玩家最早快照")

	// 2. upsert 更新已有条目不占新额度：E 把 sin2 加深到 bd99（库中 E 仍 2 条）
	upload(base, playerE, "run-E", snapIn{Sin: "sin2", MonsterType: "m2", BDCount: 99, BDData: bd(99), SourceWave: 10})

	// 3. 全局上限=3：G 上传 2 条 → 总 4 > 3 → 全局 FIFO 淘汰最旧（E 的 sin2, bd99）
	upload(base, playerG, "run-G",
		snapIn{Sin: "g1", MonsterType: "g1", BDCount: 2, BDData: bd(2), SourceWave: 10},
		snapIn{Sin: "g2", MonsterType: "g2", BDCount: 3, BDData: bd(3), SourceWave: 10},
	)
	// 候选 3 条（sin3/bd1, g1/bd2, g2/bd3）→ band=1 → 返回 bd3；若 sin2(bd99) 未被淘汰则返回 99
	for i := 0; i < 10; i++ {
		r := pick(base, playerH, 1)
		if r.Snapshot == nil || r.Snapshot.BDCount != 3 {
			panic(fmt.Sprintf("global FIFO failed, got %+v", r.Snapshot))
		}
	}
	fmt.Println("✓ 全局 FIFO：超上限淘汰最早快照；upsert 覆盖不占新额度")
}

// testTopKMode TOP_BAND topk 模式：band=前 K 条加权随机。
func testTopKMode() {
	cfg := service.DefaultEliteConfig()
	cfg.TopBandMode = "topk"
	cfg.TopBandTopK = 2
	base := startServer(cfg)

	upload(base, "topk-D", "run-D",
		snapIn{Sin: "d1", MonsterType: "d1", BDCount: 1, BDData: bd(1), SourceWave: 10},
		snapIn{Sin: "d2", MonsterType: "d2", BDCount: 5, BDData: bd(5), SourceWave: 10},
		snapIn{Sin: "d3", MonsterType: "d3", BDCount: 9, BDData: bd(9), SourceWave: 10},
	)
	// 候选 3 条按 bd 降序 [9,5,1] → band=topk=2 → 返回 bd ∈ {9,5}，永不返回 band 外的 bd1
	for i := 0; i < 20; i++ {
		r := pick(base, "topk-I", 1)
		if r.Snapshot == nil || r.Snapshot.BDCount < 5 {
			panic(fmt.Sprintf("TOP_BAND topk should pick within top-2, got %+v", r.Snapshot))
		}
	}
	fmt.Println("✓ TOP_BAND topk：前 K 条内加权随机，band 外永不命中")
}
