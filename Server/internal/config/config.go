// Package config 服务配置：命令行 flag 解析 + JSON 配置文件合并。
//
// 优先级：显式 flag > 配置文件 > 代码默认值。配置文件默认 config/server.json
// （-config 指定路径，传空禁用），键名与 flag 名一致（camelCase）；文件不存在时
// 静默跳过（保持纯 flag 行为，现有部署零影响），文件存在但解析失败则返回错误。
package config

import (
	"encoding/json"
	"flag"
	"fmt"
	"os"

	"possession/server/internal/elite"
	"possession/server/internal/logx"
)

// Config 服务完整配置（flag 与配置文件合并后的产物）。
type Config struct {
	HTTPAddr  string            // HTTP 监听地址
	DBPath    string            // SQLite 数据库文件路径
	UploadDir string            // UGC 文件上传目录
	LogDir    string            // 日志目录（空 = 只输出控制台）
	SeedFile  string            // 精英怪种子快照文件路径（空 = 不 seed）
	UserBDDir string            // 用户 BD 构筑导入目录
	DetailLog bool              // Detail 级日志开关
	RateLimit bool              // 按 IP 限流总开关（规则见 httpapi/ratelimit.go）
	Elite     elite.EliteConfig // 精英怪投放参数（TUNABLE）
}

// Parse 解析命令行参数与配置文件（优先级：显式 flag > 配置文件 > 代码默认值），
// 返回合并后的配置。args 为命令行参数（不含程序名）。
func Parse(args []string) (*Config, error) {
	fs := flag.NewFlagSet("server", flag.ContinueOnError)

	addr := fs.String("addr", ":8080", "HTTP listen address")
	dbPath := fs.String("db", "data/game.db", "SQLite database file path")
	uploadDir := fs.String("upload", "repo/ugc", "UGC file upload directory")
	logDir := fs.String("log", "log", "log directory (daily files YYYY-MM-DD.log, append; empty = console only)")
	seedFile := fs.String("seedFile", "config/seed_snapshots.json", "elite seed snapshot file (empty = no seed)")
	userBDDir := fs.String("userBDDir", "repo", "user BD import directory (MonsterBuildEditor exports; imported on every startup, content-dedup)")
	detailLog := fs.Bool("detail", true, "log detail lines (stored/skip/cand/capacity checks...; set false to reduce noise on long runs)")
	rateLimit := fs.Bool("rateLimit", true, "enable per-IP rate limiting (rules see httpapi/ratelimit.go)")
	configPath := fs.String("config", "config/server.json", "JSON config file, keys = flag names (precedence: explicit flag > file > default; empty = disabled)")

	// 精英怪投放参数（策划案 §6 TUNABLE + §8.2/§8.4 容量，默认值为首版 Baseline）
	eliteCfg := elite.DefaultEliteConfig()
	fs.IntVar(&eliteCfg.MinBD, "minBd", eliteCfg.MinBD, "elite pick: minimum BD count threshold MIN_BD")
	fs.IntVar(&eliteCfg.WaveGap, "waveGap", eliteCfg.WaveGap, "elite pick: server-side fallback spawn-index gap (wave = which elite injection, 1-based; used when client omits waveGap; 0 = no extra gap)")
	fs.StringVar(&eliteCfg.TopBandMode, "topBandMode", eliteCfg.TopBandMode, `elite pick: TOP_BAND mode "percent" | "topk"`)
	fs.Float64Var(&eliteCfg.TopBandPercent, "topBandPercent", eliteCfg.TopBandPercent, "percent mode: top band ratio (e.g. 0.2 = top 20%)")
	fs.IntVar(&eliteCfg.TopBandTopK, "topBandTopK", eliteCfg.TopBandTopK, "topk mode: top band size")
	fs.IntVar(&eliteCfg.MaxSnapshots, "maxSnapshots", eliteCfg.MaxSnapshots, "global snapshot pool cap (FIFO evicts oldest)")
	fs.IntVar(&eliteCfg.MaxSnapshotsPerPlayer, "maxSnapshotsPerPlayer", eliteCfg.MaxSnapshotsPerPlayer, "per-player snapshot cap")

	if err := fs.Parse(args); err != nil {
		return nil, err
	}

	// 配置文件合并：未显式设置的项允许文件值覆盖。
	if err := applyConfigFile(fs, *configPath, addr, dbPath, uploadDir, logDir, seedFile, userBDDir, detailLog, rateLimit, &eliteCfg); err != nil {
		return nil, err
	}

	return &Config{
		HTTPAddr:  *addr,
		DBPath:    *dbPath,
		UploadDir: *uploadDir,
		LogDir:    *logDir,
		SeedFile:  *seedFile,
		UserBDDir: *userBDDir,
		DetailLog: *detailLog,
		RateLimit: *rateLimit,
		Elite:     eliteCfg,
	}, nil
}

// fileConfig 配置文件结构（config/server.json）。全字段指针：nil = 文件未设置该项，
// 合并时不覆盖对应配置。
type fileConfig struct {
	Addr                  *string  `json:"addr"`
	DB                    *string  `json:"db"`
	Upload                *string  `json:"upload"`
	Log                   *string  `json:"log"`
	SeedFile              *string  `json:"seedFile"`
	UserBDDir             *string  `json:"userBDDir"`
	DetailLog             *bool    `json:"detail"`
	RateLimit             *bool    `json:"rateLimit"`
	MinBd                 *int     `json:"minBd"`
	WaveGap               *int     `json:"waveGap"`
	TopBandMode           *string  `json:"topBandMode"`
	TopBandPercent        *float64 `json:"topBandPercent"`
	TopBandTopK           *int     `json:"topBandTopK"`
	MaxSnapshots          *int     `json:"maxSnapshots"`
	MaxSnapshotsPerPlayer *int     `json:"maxSnapshotsPerPlayer"`
}

// applyConfigFile 读取 JSON 配置文件，填充未被显式 flag 设置的配置项。
func applyConfigFile(fs *flag.FlagSet, path string, addr, dbPath, uploadDir, logDir, seedFile, userBDDir *string, detailLog, rateLimit *bool, eliteCfg *elite.EliteConfig) error {
	if path == "" {
		return nil // -config "" 显式禁用
	}
	data, err := os.ReadFile(path)
	if err != nil {
		if os.IsNotExist(err) {
			return nil // 文件不存在：静默跳过
		}
		return fmt.Errorf("read %s: %w", path, err)
	}
	var fc fileConfig
	if err := json.Unmarshal(data, &fc); err != nil {
		return fmt.Errorf("parse %s: %w", path, err)
	}

	// fs.Visit 只遍历显式设置的 flag——未显式设置时文件值才生效。
	set := map[string]bool{}
	fs.Visit(func(f *flag.Flag) { set[f.Name] = true })

	if !set["addr"] && fc.Addr != nil {
		*addr = *fc.Addr
	}
	if !set["db"] && fc.DB != nil {
		*dbPath = *fc.DB
	}
	if !set["upload"] && fc.Upload != nil {
		*uploadDir = *fc.Upload
	}
	if !set["log"] && fc.Log != nil {
		*logDir = *fc.Log
	}
	if !set["seedFile"] && fc.SeedFile != nil {
		*seedFile = *fc.SeedFile
	}
	if !set["userBDDir"] && fc.UserBDDir != nil {
		*userBDDir = *fc.UserBDDir
	}
	if !set["detail"] && fc.DetailLog != nil {
		*detailLog = *fc.DetailLog
	}
	if !set["rateLimit"] && fc.RateLimit != nil {
		*rateLimit = *fc.RateLimit
	}
	if !set["minBd"] && fc.MinBd != nil {
		eliteCfg.MinBD = *fc.MinBd
	}
	if !set["waveGap"] && fc.WaveGap != nil {
		eliteCfg.WaveGap = *fc.WaveGap
	}
	if !set["topBandMode"] && fc.TopBandMode != nil {
		eliteCfg.TopBandMode = *fc.TopBandMode
	}
	if !set["topBandPercent"] && fc.TopBandPercent != nil {
		eliteCfg.TopBandPercent = *fc.TopBandPercent
	}
	if !set["topBandTopK"] && fc.TopBandTopK != nil {
		eliteCfg.TopBandTopK = *fc.TopBandTopK
	}
	if !set["maxSnapshots"] && fc.MaxSnapshots != nil {
		eliteCfg.MaxSnapshots = *fc.MaxSnapshots
	}
	if !set["maxSnapshotsPerPlayer"] && fc.MaxSnapshotsPerPlayer != nil {
		eliteCfg.MaxSnapshotsPerPlayer = *fc.MaxSnapshotsPerPlayer
	}

	logx.Event("config file · %s (explicit flags take precedence)", path)
	return nil
}
