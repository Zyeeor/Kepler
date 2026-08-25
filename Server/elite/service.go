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

// EliteService 精英怪 BD 快照服务。
type EliteService struct {
	store EliteStore
	cfg   EliteConfig
}

// NewEliteService 创建服务（配置在构造时归一化）。
func NewEliteService(st EliteStore, cfg EliteConfig) *EliteService {
	return &EliteService{store: st, cfg: cfg.normalize()}
}

// validSins 合法七宗罪 wire 名（客户端 EliteMonsterCatalog.WireName 同源）。
var validSins = map[string]bool{
	"pride": true, "sloth": true, "gluttony": true, "envy": true,
	"wrath": true, "greed": true, "lust": true,
}
