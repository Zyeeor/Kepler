// 荣誉殿堂排行榜（策划案 §5.4/§5.8 异步战绩的 Top N 视图）：击杀玩家次数最多的 BD 怪物。
package elite

import (
	"time"

	"demo/server/tools/logx"
)

// LeaderboardDefaultLimit / LeaderboardMaxLimit limit 参数默认值与上限保护。
const (
	LeaderboardDefaultLimit = 20
	LeaderboardMaxLimit     = 100
)

// leaderboardCacheTTL 缓存 TTL 兜底（写路径失效为主失效手段；异步战绩语义容忍秒级延迟）。
const leaderboardCacheTTL = 30 * time.Second

// Leaderboard 荣誉殿堂排行榜：按击杀玩家次数（body_fatal）降序取 Top limit 个 BD 怪物。
//
// 排序主键 = body_fatal（击杀玩家 Body Fatal 次数；run_fail 不并入主键——同一次玩家死亡
// 前台会同时上报 bodyFatal 与 runFail，相加会双计数）。tie-break：run_fail → deployed →
// updated_at（全部降序，保证顺序稳定）。
// 排行主体 = BD 快照（INNER JOIN：被容量治理淘汰的悬空聚合行不上榜——怪物与构筑信息
// 已不可考，无展示意义）。快照按 (player, run, sin) upsert 滚动覆盖，榜单展示的 BD 为
// 该键当前最新版本，战绩为跨版本累积（现有数据模型固有的归因粒度）。
//
// 进程内缓存：一次查库回填全库 Top MaxLimit 条，任意 limit ≤ MaxLimit 的请求切片命中；
// 写路径（战果聚合 / 快照 upsert / 容量淘汰）即调 invalidateLeaderboard 失效，
// TTL 兜底防漏。返回切片与缓存共享底层数组，调用方只读（handler 仅序列化）。
func (s *EliteService) Leaderboard(limit int) ([]*LeaderboardEntry, error) {
	if limit < 1 {
		limit = LeaderboardDefaultLimit
	}
	if limit > LeaderboardMaxLimit {
		limit = LeaderboardMaxLimit
	}

	// 命中：缓存有效期内直接切片（缓存即当前全量 Top；库中条数不足 limit 时截断）。
	s.lbMu.RLock()
	if s.lbCache != nil && time.Now().Before(s.lbExpireAt) {
		entries := s.lbCache
		s.lbMu.RUnlock()
		if limit > len(entries) {
			limit = len(entries)
		}
		logx.Event("leaderboard · cached entries=%d limit=%d", limit, limit)
		return entries[:limit], nil
	}
	s.lbMu.RUnlock()

	// 未命中：查库取满额（Top MaxLimit）回填缓存。
	entries, err := s.store.Leaderboard(LeaderboardMaxLimit)
	if err != nil {
		return nil, err
	}
	s.lbMu.Lock()
	s.lbCache = entries
	s.lbExpireAt = time.Now().Add(leaderboardCacheTTL)
	s.lbMu.Unlock()

	if limit > len(entries) {
		limit = len(entries)
	}
	logx.Event("leaderboard · entries=%d limit=%d", limit, limit)
	return entries[:limit], nil
}

// invalidateLeaderboard 排行榜缓存失效（写路径成功后调用）：
// 战果聚合（RecordEvents）、快照 upsert（Upload / UploadUserBD，含容量治理淘汰）。
// 种子注入与启动导入发生在缓存建立前，无需失效。
func (s *EliteService) invalidateLeaderboard() {
	s.lbMu.Lock()
	s.lbCache = nil
	s.lbMu.Unlock()
}
