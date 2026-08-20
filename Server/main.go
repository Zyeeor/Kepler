// UGC 内容服务器入口：地图/怪物模版分享平台 + 精英怪 BD 快照投放。
//
// 《Possession》为单机游戏，本服务只承担两块后端职责：
//   - UGC 内容平台（HTTP JSON）：上传、下载、列表、搜索、订阅、评分
//   - 精英怪投放：其他玩家 BD 快照的存储与筛选（仅此系统联网）
//
// 用法：
//
//	./server                    # 默认配置（监听 :8080，日志落 data/server.log）
//	./server -addr :9000        # 自定义端口
//	./server -log ""            # 传空禁用文件日志，只输出到控制台
package main

import (
	"flag"
	"io"
	"log"
	"os"
	"path/filepath"

	"demo/server/internal/server"
	"demo/server/internal/service"
)

func main() {
	addr := flag.String("addr", ":8080", "HTTP 监听地址")
	dbPath := flag.String("db", "data/game.db", "SQLite 数据库文件路径")
	uploadDir := flag.String("upload", "data/ugc", "UGC 文件上传目录")
	logPath := flag.String("log", "data/server.log", "日志文件路径（追加写入，同时输出到控制台；传空禁用文件日志）")
	seedFile := flag.String("seedFile", "data/seed_snapshots.json", "精英怪种子快照文件路径（空=不 seed）")

	// 精英怪投放参数（策划案 §6 TUNABLE + §8.2/§8.4 容量，默认值为首版 Baseline）
	elite := service.DefaultEliteConfig()
	flag.IntVar(&elite.MinBD, "minBd", elite.MinBD, "精英怪筛选：最低 BD 数量门槛 MIN_BD")
	flag.IntVar(&elite.WaveGap, "waveGap", elite.WaveGap, "精英怪筛选：服务端兜底波次差（客户端未传 waveGap 时使用，默认 0=不叠加）")
	flag.StringVar(&elite.TopBandMode, "topBandMode", elite.TopBandMode, `精英怪筛选：TOP_BAND 模式 "percent" | "topk"`)
	flag.Float64Var(&elite.TopBandPercent, "topBandPercent", elite.TopBandPercent, "percent 模式：高分档比例（如 0.2 = 前 20%）")
	flag.IntVar(&elite.TopBandTopK, "topBandTopK", elite.TopBandTopK, "topk 模式：高分档条数")
	flag.IntVar(&elite.MaxSnapshots, "maxSnapshots", elite.MaxSnapshots, "候选库全局上限（FIFO 淘汰最早快照）")
	flag.IntVar(&elite.MaxSnapshotsPerPlayer, "maxSnapshotsPerPlayer", elite.MaxSnapshotsPerPlayer, "每玩家快照上限")
	flag.Parse()

	// 日志初始化：控制台 + 文件双写（追加模式，不轮转——单机 Demo 阶段人工管理即可）。
	if *logPath != "" {
		if dir := filepath.Dir(*logPath); dir != "." && dir != "" {
			if err := os.MkdirAll(dir, 0o755); err != nil {
				log.Fatalf("create log dir: %v", err)
			}
		}
		logFile, err := os.OpenFile(*logPath, os.O_CREATE|os.O_WRONLY|os.O_APPEND, 0o644)
		if err != nil {
			log.Fatalf("open log file: %v", err)
		}
		defer logFile.Close()
		log.SetOutput(io.MultiWriter(os.Stderr, logFile))
		log.Printf("log file: %s", *logPath)
	}

	srv, err := server.New(server.Config{
		HTTPAddr:  *addr,
		DBPath:    *dbPath,
		UploadDir: *uploadDir,
		Elite:     elite,
		SeedFile:  *seedFile,
	})
	if err != nil {
		log.Fatalf("init server: %v", err)
	}
	defer srv.Close()

	if err := srv.Run(); err != nil {
		log.Fatalf("server exit: %v", err)
	}
}
