// 战果事件幂等去重：按 (reporter, eventId) 内存窗口查重，防客户端重试/重放重复累加计数。
// eventId 为空 = 旧客户端，跳过查重（行为与改造前一致）；进程重启清零（聚合计数器为
// 最终一致语义，重启窗口内的重复可接受）。
package elite

import (
	"sync"
	"time"
)

// eventDedup 事件去重表：容量上限 + TTL 惰性清扫。
type eventDedup struct {
	mu        sync.Mutex
	seen      map[string]time.Time // key → 首见时间
	cap       int                  // 容量上限（超出触发清扫）
	ttl       time.Duration        // 窗口：首见后这么久内视为重复
	lastSweep time.Time            // 上次清扫时间（无论容量，周期性清扫）
}

// newEventDedup 创建去重表。
func newEventDedup(capacity int, ttl time.Duration) *eventDedup {
	return &eventDedup{
		seen:      make(map[string]time.Time, capacity/4),
		cap:       capacity,
		ttl:       ttl,
		lastSweep: time.Now(),
	}
}

// Seen 原子检查并记录：key 未见过 → 记录并返回 false（放行）；
// 已在窗口内 → 返回 true（重复，跳过）。
func (d *eventDedup) Seen(key string) bool {
	now := time.Now()
	d.mu.Lock()
	defer d.mu.Unlock()

	if t, ok := d.seen[key]; ok && now.Sub(t) < d.ttl {
		return true // 窗口内重复
	}

	// 放行前确保容量：达到上限或距上次清扫超过 TTL/2 → 清扫过期项
	if len(d.seen) >= d.cap || now.Sub(d.lastSweep) > d.ttl/2 {
		d.sweepLocked(now)
	}
	d.seen[key] = now
	return false
}

// sweepLocked 清扫过期项（调用方须持有锁）。
func (d *eventDedup) sweepLocked(now time.Time) {
	for k, t := range d.seen {
		if now.Sub(t) >= d.ttl {
			delete(d.seen, k)
		}
	}
	d.lastSweep = now
}
