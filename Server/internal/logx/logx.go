// Package logx 统一服务端日志版式：
//
//	2026/08/24 15:13:40 pick wave=5 player=device-xxx gap=1 · minBD=1 band=percent/0.20 topK=5
//	2026/08/24 15:13:40 query · bdCount>=1 AND sourceWave>=6 AND player!=self → candidates=3
//	2026/08/24 15:13:40 pick result wave=5 player=device-xxx → sin=lust bdCount=3 sourceWave=7 (path=main, 3 candidates)
//	2026/08/24 15:13:40 handleElitePick → 200 · 3ms · 127.0.0.1:51303
//
// Event 输出顶层事件，Detail 输出隶属事件的明细行（仅代码语义区分，版式相同）。
// 输出仍走标准 log 包，日志路由（控制台 + 文件双写）由 main 统一设置。
package logx

import (
	"fmt"
	"log"
)

// Event 输出一条顶层事件日志。
func Event(format string, args ...any) {
	log.Printf("%s", fmt.Sprintf(format, args...))
}

// detailEnabled Detail 级日志开关（P3 级别过滤）：默认开启保持既有观测行为；
// main 可用 -detail=false 关闭（明细行量大：stored/skip/cand/容量检查等，长跑减噪用）。
var detailEnabled = true

// EnableDetail 开关 Detail 级日志。
func EnableDetail(on bool) { detailEnabled = on }

// Detail 输出一条隶属事件的明细行（与 Event 版式相同，仅作代码语义区分）。
// 可通过 EnableDetail(false) / -detail=false 整级关闭。
func Detail(format string, args ...any) {
	if !detailEnabled {
		return
	}
	log.Printf("%s", fmt.Sprintf(format, args...))
}
