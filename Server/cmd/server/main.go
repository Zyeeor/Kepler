// UGC 内容服务器入口：地图/怪物模版分享平台 + 精英怪 BD 快照投放。
//
// 《Possession》为单机游戏，本服务只承担两块后端职责：
//   - UGC 内容平台（HTTP JSON）：上传、下载、列表、搜索、订阅、评分
//   - 精英怪投放：其他玩家 BD 快照的存储与筛选（仅此系统联网）
//
// 用法（构建方式见根目录 start-server 脚本与 README）：
//
//	go run ./cmd/server                # 默认配置（监听 :8080，日志按天落 log/）
//	go run ./cmd/server -addr :9000    # 自定义端口
//	go run ./cmd/server -log ""        # 传空禁用文件日志，只输出到控制台
//	go run ./cmd/server -config ""     # 禁用配置文件，仅用 flag
//
// 配置解析（flag + JSON 配置文件合并）见 internal/config。
package main

import (
	"io"
	"log"
	"os"

	"possession/server/internal/config"
	"possession/server/internal/httpapi"
	"possession/server/internal/logx"
)

func main() {
	cfg, err := config.Parse(os.Args[1:])
	if err != nil {
		log.Fatalf("config: %v", err)
	}
	logx.EnableDetail(cfg.DetailLog)

	// 日志初始化：控制台 + 文件双写；文件按天一个（log/YYYY-MM-DD.log，追加，不轮转——单机 Demo 阶段人工管理即可）。
	if cfg.LogDir != "" {
		w, err := logx.NewDailyWriter(cfg.LogDir)
		if err != nil {
			log.Fatalf("init log dir: %v", err)
		}
		defer w.Close()
		log.SetOutput(io.MultiWriter(os.Stderr, w))
		logx.Event("log dir · %s (daily)", cfg.LogDir)
	}

	srv, err := httpapi.New(httpapi.Config{
		HTTPAddr:         cfg.HTTPAddr,
		DBPath:           cfg.DBPath,
		UploadDir:        cfg.UploadDir,
		Elite:            cfg.Elite,
		SeedFile:         cfg.SeedFile,
		UserBDDir:        cfg.UserBDDir,
		DisableRateLimit: !cfg.RateLimit,
	})
	if err != nil {
		log.Fatalf("init server: %v", err)
	}
	defer srv.Close()

	if err := srv.Run(); err != nil {
		log.Fatalf("server exit: %v", err)
	}
}
