// 精英怪筛选投放（§3 四步 + §5 三级兜底 + TOP_BAND 加权随机）。
package elite

import (
	"math"
	"math/rand"

	"demo/server/tools/logx"
)

// Pick 第 N 次投放请求精英怪：返回一条「确为他人在更高投放序号时点 BD 过」的快照。
//
// 投放模型（前台现状）：精英投放为本地定时模型——每 60s 周期第 40s 投 1 只，
// Boss 前共约 7 次投放；wave / sourceWave 的「波次」字段语义 = 第几次投放精英怪
// （投放序号，1-based，前台 cycleIndex + 1）。
//
// 返回值：snap == nil 表示本次不投放（兜底 3，正常业务分支）；
// relaxed 表示命中了放宽投放序号条件的兜底路径（仅观测用，前台无需处理）。
// waveGap 由客户端传入（投放序号差，难度设置），<0 时回退到服务端配置默认值。
func (s *EliteService) Pick(playerID string, wave int, waveGap int) (snap *BuildSnapshot, relaxed bool, err error) {
	if waveGap < 0 {
		waveGap = s.cfg.WaveGap
	}
	logx.Event("pick wave=%d player=%s gap=%d · minBD=%d band=%s/%.2f topK=%d",
		wave, playerID, waveGap, s.cfg.MinBD, s.cfg.TopBandMode, s.cfg.TopBandPercent, s.cfg.TopBandTopK)

	// 主路径（Step 1–4）：bdCount >= MIN_BD 且 sourceWave >= N + waveGap 且他人。
	minWave := wave + waveGap
	cands, err := s.store.PickCandidates(s.cfg.MinBD, minWave, playerID)
	if err != nil {
		return nil, false, err
	}
	logx.Detail("query · bdCount>=%d AND sourceWave>=%d AND player!=self → candidates=%d",
		s.cfg.MinBD, minWave, len(cands))
	if len(cands) > 0 {
		s.logCandidates(cands)
		snap := s.pickInBand(cands)
		s.logPickResult("main", wave, playerID, snap, len(cands))
		return snap, false, nil
	}

	// 兜底 1：放宽 WAVE_GAP 到 0（允许同投放序号的 BD 怪）。Step 1/Step 3 保持。
	logx.Detail("fallback1 · main path empty, relax waveGap → sourceWave>=%d (was >=%d)", wave, minWave)
	cands, err = s.store.PickCandidates(s.cfg.MinBD, wave, playerID)
	if err != nil {
		return nil, false, err
	}
	logx.Detail("fallback1 query · bdCount>=%d AND sourceWave>=%d AND player!=self → candidates=%d",
		s.cfg.MinBD, wave, len(cands))
	if len(cands) > 0 {
		s.logCandidates(cands)
		snap := s.pickInBand(cands)
		s.logPickResult("relaxed:wave-gap=0", wave, playerID, snap, len(cands))
		return snap, true, nil
	}

	// 兜底 2：全库 sourceWave 最高档中 bdCount 最大的一条。Step 1/Step 3 保持。
	logx.Detail("fallback2 · fallback1 empty, trying top-wave tier (global max sourceWave, bdCount>=%d, player!=self)", s.cfg.MinBD)
	cands, err = s.store.TopWaveCandidates(s.cfg.MinBD, playerID)
	if err != nil {
		return nil, false, err
	}
	logx.Detail("fallback2 query → top-wave candidates=%d", len(cands))
	if len(cands) > 0 {
		s.logCandidates(cands)
		snap := cands[0]
		s.logPickResult("relaxed:top-wave", wave, playerID, snap, len(cands))
		return snap, true, nil
	}

	// 兜底 3：本次不投放精英怪。
	logx.Event("pick result wave=%d player=%s → none (all paths exhausted, pool empty)", wave, playerID)
	return nil, false, nil
}

// logPickResult 记录筛选最终结果。
func (s *EliteService) logPickResult(path string, wave int, playerID string, snap *BuildSnapshot, candidates int) {
	logx.Event("pick result wave=%d player=%s → sin=%s bdCount=%d sourceWave=%d by=%s (path=%s, %d candidates)",
		wave, playerID, snap.Sin, snap.BDCount, snap.SourceWave, snap.PlayerID, path, candidates)
}

// logCandidates 打印候选列表摘要（最多前 5 条 + 尾部 1 条）。
func (s *EliteService) logCandidates(cands []*BuildSnapshot) {
	show := len(cands)
	if show > 5 {
		show = 5
	}
	for i := 0; i < show; i++ {
		c := cands[i]
		logx.Detail("cand #%-2d · id=%-4d sin=%-8s bd=%-2d wave=%-3d by=%s run=%s",
			i+1, c.ID, c.Sin, c.BDCount, c.SourceWave, c.PlayerID, c.RunID)
	}
	if len(cands) > 5 {
		c := cands[len(cands)-1]
		logx.Detail("cand #%-2d · id=%-4d sin=%-8s bd=%-2d wave=%-3d by=%s run=%s (+%d more)",
			len(cands), c.ID, c.Sin, c.BDCount, c.SourceWave, c.PlayerID, c.RunID, len(cands)-show-1)
	}
}

// pickInBand TOP_BAND 内加权随机取 1（§3 Step 4）。
//
// cands 已按 bdCount 降序；高档内以 bdCount 为权重随机，
// 避免每局精英怪永远是同一只 BD 最满的怪；候选很少（band <= 1）时直接取最高。
func (s *EliteService) pickInBand(cands []*BuildSnapshot) *BuildSnapshot {
	n := s.topBandSize(len(cands))
	logx.Detail("band · mode=%s size=%d/%d (percent=%.2f, topK=%d)",
		s.cfg.TopBandMode, n, len(cands), s.cfg.TopBandPercent, s.cfg.TopBandTopK)
	if n <= 1 {
		logx.Detail("band · size<=1 → pick top id=%d sin=%s bdCount=%d", cands[0].ID, cands[0].Sin, cands[0].BDCount)
		return cands[0]
	}
	total := 0
	for i := 0; i < n; i++ {
		total += cands[i].BDCount
	}
	if total <= 0 {
		return cands[0]
	}
	r := rand.Intn(total)
	logx.Detail("band · weighted random total=%d roll=%d", total, r)
	for i := 0; i < n; i++ {
		logx.Detail("band[%d] · id=%d sin=%s bdCount=%d (weight=%d, remaining=%d)",
			i, cands[i].ID, cands[i].Sin, cands[i].BDCount, cands[i].BDCount, r)
		r -= cands[i].BDCount
		if r < 0 {
			logx.Detail("band · selected band[%d] → id=%d sin=%s bdCount=%d", i, cands[i].ID, cands[i].Sin, cands[i].BDCount)
			return cands[i]
		}
	}
	return cands[n-1]
}

// topBandSize 计算参与加权随机的高分档条数（双模式，§8.4）。
func (s *EliteService) topBandSize(n int) int {
	if n <= 1 {
		return n
	}
	if s.cfg.TopBandMode == "topk" {
		if n < s.cfg.TopBandTopK {
			return n
		}
		return s.cfg.TopBandTopK
	}
	size := int(math.Ceil(float64(n) * s.cfg.TopBandPercent))
	if size < 1 {
		size = 1
	}
	if size > n {
		size = n
	}
	return size
}
