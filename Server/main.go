// UGC 内容服务器入口：地图/怪物模版分享平台 + 精英怪 BD 快照投放。
//
// 《Possession》为单机游戏，本服务只承担两块后端职责：
//   - UGC 内容平台（HTTP JSON）：上传、下载、列表、搜索、订阅、评分
//   - 精英怪投放：其他玩家 BD 快照的存储与筛选（仅此系统联网）
//
// 用法：
//
//	./server                    # 默认配置（监听 :8080，日志按天落 log/）
//	./server -addr :9000        # 自定义端口
//	./server -log ""            # 传空禁用文件日志，只输出到控制台
package main

import (
	"flag"
	"io"
	"log"
	"os"

	"demo/server/elite"
	"demo/server/server"
	"demo/server/tools/logx"
)

func main() {
	addr := flag.String("addr", ":8080", "HTTP listen address")
	dbPath := flag.String("db", "data/game.db", "SQLite database file path")
	uploadDir := flag.String("upload", "repo/ugc", "UGC file upload directory")
	logDir := flag.String("log", "log", "log directory (daily files YYYY-MM-DD.log, append; empty = console only)")
	seedFile := flag.String("seedFile", "config/seed_snapshots.json", "elite seed snapshot file (empty = no seed)")
	userBDDir := flag.String("userBDDir", "repo", "user BD import directory (MonsterBuildEditor exports; imported on every startup, content-dedup)")
	detailLog := flag.Bool("detail", true, "log detail lines (stored/skip/cand/capacity checks...; set false to reduce noise on long runs)")

	// 精英怪投放参数（策划案 §6 TUNABLE + §8.2/§8.4 容量，默认值为首版 Baseline）
	eliteCfg := elite.DefaultEliteConfig()
	flag.IntVar(&eliteCfg.MinBD, "minBd", eliteCfg.MinBD, "elite pick: minimum BD count threshold MIN_BD")
	flag.IntVar(&eliteCfg.WaveGap, "waveGap", eliteCfg.WaveGap, "elite pick: server-side fallback spawn-index gap (wave = which elite injection, 1-based; used when client omits waveGap; 0 = no extra gap)")
	flag.StringVar(&eliteCfg.TopBandMode, "topBandMode", eliteCfg.TopBandMode, `elite pick: TOP_BAND mode "percent" | "topk"`)
	flag.Float64Var(&eliteCfg.TopBandPercent, "topBandPercent", eliteCfg.TopBandPercent, "percent mode: top band ratio (e.g. 0.2 = top 20%)")
	flag.IntVar(&eliteCfg.TopBandTopK, "topBandTopK", eliteCfg.TopBandTopK, "topk mode: top band size")
	flag.IntVar(&eliteCfg.MaxSnapshots, "maxSnapshots", eliteCfg.MaxSnapshots, "global snapshot pool cap (FIFO evicts oldest)")
	flag.IntVar(&eliteCfg.MaxSnapshotsPerPlayer, "maxSnapshotsPerPlayer", eliteCfg.MaxSnapshotsPerPlayer, "per-player snapshot cap")
	flag.Parse()
	logx.EnableDetail(*detailLog)

	// 日志初始化：控制台 + 文件双写；文件按天一个（log/YYYY-MM-DD.log，追加，不轮转——单机 Demo 阶段人工管理即可）。
	if *logDir != "" {
		w, err := logx.NewDailyWriter(*logDir)
		if err != nil {
			log.Fatalf("init log dir: %v", err)
		}
		defer w.Close()
		log.SetOutput(io.MultiWriter(os.Stderr, w))
		logx.Event("log dir · %s (daily)", *logDir)
	}

	srv, err := server.New(server.Config{
		HTTPAddr:  *addr,
		DBPath:    *dbPath,
		UploadDir: *uploadDir,
		Elite:     eliteCfg,
		SeedFile:  *seedFile,
		UserBDDir: *userBDDir,
	})
	if err != nil {
		log.Fatalf("init server: %v", err)
	}
	defer srv.Close()

	if err := srv.Run(); err != nil {
		log.Fatalf("server exit: %v", err)
	}
}
