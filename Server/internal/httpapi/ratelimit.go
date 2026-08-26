// 按客户端 IP 的内存令牌桶限流：防异常/恶意客户端高频刷接口（无外部依赖，单实例部署）。
//
// 每条规则一个 limiter（rate 个/秒持续补充，容量 burst 突发）；默认阈值远宽于正常
// 游戏行为（投放模型为 60s 周期），只拦截脚本级滥用，均为 TUNABLE 常量。
// 超限返回 429 + Retry-After（等待秒数）。
//
// 局限（已知，接受）：按 RemoteAddr（含反代场景下的代理 IP）限流；不做 X-Forwarded-For
// 解析——公网经 Nginx 反代部署时全站共享同一桶，阈值需按整体流量评估放宽。
package httpapi

import (
	"math"
	"net"
	"net/http"
	"strconv"
	"sync"
	"time"

	"possession/server/internal/logx"
)

// ============================================================================
// 规则（TUNABLE：正常玩家无感的宽松默认值）
// ============================================================================

// rateRule 单条限流规则：rate = 持续速率（个/秒），burst = 突发容量。
type rateRule struct {
	rate  float64
	burst int
}

// rateRules 路由组 → 规则：每 IP 每组独立计桶。
var rateRules = map[string]rateRule{
	"ugc_upload": {rate: 0.5, burst: 10}, // UGC 上传（重请求）：~30/min 持续，突发 10
	"ugc_action": {rate: 1, burst: 30},   // 订阅 / 评分：~60/min，突发 30
	"snapshot":   {rate: 2, burst: 60},   // BD 快照批量上传：~120/min，突发 60（每轮选卡后上传）
	"pick":       {rate: 4, burst: 120},  // 精英投放请求：~240/min（正常 60s 一次，超宽）
	"events":     {rate: 2, burst: 60},   // 战果回传批量上报：~120/min
	"userbd":     {rate: 1, burst: 20},   // 工具构筑上传：~60/min
	"read":       {rate: 10, burst: 300}, // GET 类（列表/搜索/下载/stats/leaderboard/health）
}

// ============================================================================
// 令牌桶
// ============================================================================

// bucket 单个客户端的令牌桶状态。
type bucket struct {
	tokens float64   // 当前令牌数（≤ burst）
	last   time.Time // 上次补充时间
}

// limiter 单条规则的限流器：按客户端 IP 分桶。
type limiter struct {
	mu        sync.Mutex
	rate      float64
	burst     float64
	buckets   map[string]*bucket
	lastSweep time.Time // 上次清扫时间（防桶无限增长）
}

// newLimiter 创建限流器。
func newLimiter(rule rateRule) *limiter {
	return &limiter{
		rate:      rule.rate,
		burst:     float64(rule.burst),
		buckets:   make(map[string]*bucket),
		lastSweep: time.Now(),
	}
}

// sweepInterval 清扫间隔；bucketIdleTTL 不活跃桶的回收时长。
const (
	sweepInterval = 10 * time.Minute
	bucketIdleTTL = 30 * time.Minute
)

// allow 取一个令牌：允许返回 (true, 0)；超限返回 (false, 需等待时长)。
func (l *limiter) allow(key string) (bool, time.Duration) {
	now := time.Now()
	l.mu.Lock()
	defer l.mu.Unlock()

	if now.Sub(l.lastSweep) >= sweepInterval {
		l.sweepLocked(now)
	}

	b, ok := l.buckets[key]
	if !ok {
		b = &bucket{tokens: l.burst, last: now}
		l.buckets[key] = b
	} else {
		// 按流逝时间补充令牌（上限 burst）
		b.tokens = math.Min(l.burst, b.tokens+now.Sub(b.last).Seconds()*l.rate)
		b.last = now
	}

	if b.tokens >= 1 {
		b.tokens--
		return true, 0
	}
	// 不足 1 个：等待补满 1 个令牌的时长
	wait := time.Duration((1 - b.tokens) / l.rate * float64(time.Second))
	return false, wait
}

// sweepLocked 回收不活跃桶（调用方须持有锁）。
func (l *limiter) sweepLocked(now time.Time) {
	for k, b := range l.buckets {
		if now.Sub(b.last) >= bucketIdleTTL {
			delete(l.buckets, k)
		}
	}
	l.lastSweep = now
}

// ============================================================================
// 中间件
// ============================================================================

// clientIP 提取客户端 IP（RemoteAddr 去端口；不做代理头解析，见文件头说明）。
func clientIP(r *http.Request) string {
	if host, _, err := net.SplitHostPort(r.RemoteAddr); err == nil {
		return host
	}
	return r.RemoteAddr
}

// limited 给 handler 挂限流：超限返回 429 + Retry-After。s.limiters 无该规则（限流禁用）时透传。
func (s *Server) limited(rule string, h http.HandlerFunc) http.HandlerFunc {
	l := s.limiters[rule]
	if l == nil {
		return h
	}
	return func(w http.ResponseWriter, r *http.Request) {
		if ok, wait := l.allow(clientIP(r)); !ok {
			retryAfter := int(math.Ceil(wait.Seconds()))
			if retryAfter < 1 {
				retryAfter = 1
			}
			w.Header().Set("Retry-After", strconv.Itoa(retryAfter))
			logx.Event("rate limited · rule=%s ip=%s retry_after=%ds",
				rule, clientIP(r), retryAfter)
			writeErr(w, http.StatusTooManyRequests, "too many requests")
			return
		}
		h(w, r)
	}
}
