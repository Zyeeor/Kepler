// 荣誉殿堂排行榜（策划案 §5.4/§5.8 异步战绩的 Top N 视图）：击杀玩家次数最多的 BD 怪物。
package elite

import (
	"time"

	"possession/server/internal/logx"
)

// LeaderboardDefaultLimit / LeaderboardMaxLimit limit 参数默认值与上限保护。
const (
	LeaderboardDefaultLimit = 20
	LeaderboardMaxLimit     = 100
)

// leaderboardCacheTTL 缓存 TTL 兜底（写路径失效为主失效手段；异步战绩语义容忍秒级延迟）。
const leaderboardCacheTTL = 30 * time.Second

// Leaderboard 荣誉殿堂排行榜：按击杀玩家次数（body_fatal）降序取 Top limit 个 BD 怪物。
// run_fail 不并入主键——同一次死亡前台双报 bodyFatal/runFail，相加会双计数；
// tie-break：run_fail → deployed → updated_at。INNER JOIN 快照表，悬空聚合行不上榜
//（被容量治理淘汰，构筑信息不可考）。进程内缓存：查库回填 Top MaxLimit 条，任意
// limit 切片命中；写路径失效 + TTL 兜底。返回切片与缓存共享底层数组，调用方只读。
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
