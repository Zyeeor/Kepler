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

// Detail 输出一条隶属事件的明细行（与 Event 版式相同，仅作代码语义区分）。
func Detail(format string, args ...any) {
	log.Printf("%s", fmt.Sprintf(format, args...))
}
