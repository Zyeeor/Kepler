// 精英怪投放可调参数配置。
package elite

// EliteConfig 精英怪投放可调参数（策划案 §6 TUNABLE + §8.2/§8.4 容量治理）。
type EliteConfig struct {
	MinBD                 int     // MIN_BD：最低 BD 数量门槛（Step 1）
	WaveGap               int     // WAVE_GAP：投放序号差（Step 2；「波次」字段语义 = 第几次投放精英怪）
	TopBandMode           string  // TOP_BAND 模式："percent"（前 X%）| "topk"（前 K 条），双模式可切换（§8.4）
	TopBandPercent        float64 // percent 模式：高分档比例（如 0.2 = 前 20%）
	TopBandTopK           int     // topk 模式：高分档条数
	MaxSnapshots          int     // 候选库全局上限（FIFO 淘汰最早快照，§8.2）
	MaxSnapshotsPerPlayer int     // 每玩家快照上限（§8.4，与全局 FIFO 并行生效）
}

// DefaultEliteConfig 默认参数（首版 Baseline，需 Playable 验证后调整，不代表数值冻结）。
func DefaultEliteConfig() EliteConfig {
	return EliteConfig{
		MinBD:                 1,
		WaveGap:               0, // 服务端不叠加难度，waveGap 完全由客户端指定
		TopBandMode:           "percent",
		TopBandPercent:        0.2,
		TopBandTopK:           5,
		MaxSnapshots:          10000,
		MaxSnapshotsPerPlayer: 100,
	}
}

// normalize 钳制非法参数到安全范围。
func (c EliteConfig) normalize() EliteConfig {
	if c.MinBD < 1 {
		c.MinBD = 1 // Step 1「bdCount=0 直接丢弃」是核心承诺，下限为 1
	}
	if c.WaveGap < 0 {
		c.WaveGap = 0
	}
	if c.TopBandMode != "topk" {
		c.TopBandMode = "percent"
	}
	if c.TopBandPercent <= 0 || c.TopBandPercent > 1 {
		c.TopBandPercent = 0.2
	}
	if c.TopBandTopK < 1 {
		c.TopBandTopK = 1
	}
	if c.MaxSnapshots < 1 {
		c.MaxSnapshots = 10000
	}
	if c.MaxSnapshotsPerPlayer < 1 {
		c.MaxSnapshotsPerPlayer = 100
	}
	return c
}
