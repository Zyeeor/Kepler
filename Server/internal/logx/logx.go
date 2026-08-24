// Package logx 统一服务端日志版式：
//
//	2026/08/24 15:13:40 pick wave=5 player=device-xxx gap=1 · minBD=1 band=percent/0.20 topK=5
//	2026/08/24 15:13:40   ├ query · bdCount>=1 AND sourceWave>=6 AND player!=self → candidates=3
//	2026/08/24 15:13:40   ├ cand #1  · id=1    lust      bd=3  wave=7   by=device-seed-default-a run=run-seed-001
//	2026/08/24 15:13:40 pick result wave=5 player=device-xxx → sin=lust bdCount=3 sourceWave=7 (path=main, 3 candidates)
//	2026/08/24 15:13:40 POST /api/elite/pick → 200 · 3ms · 127.0.0.1:51303
//
// Event 输出顶层事件，Detail 输出隶属事件的明细行（`├` 树形前缀缩进），
// 替代旧版 "[elite]" / "  [stored]" 前缀与手写空格缩进。
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

// Detail 输出一条明细行：缩进一级并带树形前缀。
func Detail(format string, args ...any) {
	log.Printf("  ├ %s", fmt.Sprintf(format, args...))
}
