// Package elite — 精英怪 BD 快照投放域：上传（滚动 upsert）、筛选投放、战果回传聚合。
//
// 设计依据：《精英怪筛选 — 他人 BD 怪物投放 · 策划案》§3–§6/§8。
// 筛选只依赖 bdCount 与 sourceWave；透传原则见 BuildSnapshot。
//
// 文件划分：
//   - config.go：EliteConfig 与默认值
//   - service.go：服务本体与共享白名单
//   - store.go：EliteStore 存储抽象与数据模型
//   - seed.go：种子快照注入
//   - userbd.go：用户 BD（工具在线上传 + 启动目录导入 + 内容指纹去重）
//   - upload.go：客户端滚动上传 + 容量治理
//   - pick.go：四步筛选 + 三级兜底 + TOP_BAND 加权随机
//   - events.go：战果回传聚合 + 战绩查询
package elite

import (
	"sync"
	"time"
)

// EliteService 精英怪 BD 快照服务。
type EliteService struct {
	store EliteStore
	cfg   EliteConfig

	// 荣誉殿堂排行榜进程内缓存（leaderboard.go）：写路径失效 + TTL 兜底。
	// 零外部依赖的轻量读优化——读多写少（异步战绩语义容忍秒级延迟）。
	lbMu       sync.RWMutex
	lbCache    []*LeaderboardEntry // 全库 Top LeaderboardMaxLimit 条（不足则全量）
	lbExpireAt time.Time

	// 战果事件幂等去重（dedup.go）：eventId 窗口内重复上报跳过，防重试重放刷计数。
	dedup *eventDedup

	// BD 内容指纹缓存：首次使用全库加载，常驻内存，入库成功后增量维护——
	// userBD 在线上传的重复检测从 O(全库扫描) 降为 O(1)（userbd.go）。
	fpMu    sync.Mutex
	fpSeen  map[string]struct{}
	fpReady bool
}

// NewEliteService 创建服务（配置在构造时归一化）。
func NewEliteService(st EliteStore, cfg EliteConfig) *EliteService {
	return &EliteService{
		store: st,
		cfg:   cfg.normalize(),
		dedup: newEventDedup(10000, 10*time.Minute),
	}
}

// validSins 合法七宗罪 wire 名（客户端 EliteMonsterCatalog.WireName 同源）。
var validSins = map[string]bool{
	"pride": true, "sloth": true, "gluttony": true, "envy": true,
	"wrath": true, "greed": true, "lust": true,
}
