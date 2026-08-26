// BD 内容指纹缓存与去重工具：首次使用全库加载、常驻内存，入库点增量维护——
// userBD 在线上传与启动导入的重复检测从 O(全库扫描) 降为 O(1)。
package elite

import (
	"encoding/json"
	"sort"
	"strings"
)

// initFingerprints 首次调用时全库加载内容指纹缓存（幂等）。
func (s *EliteService) initFingerprints() error {
	s.fpMu.Lock()
	defer s.fpMu.Unlock()
	if s.fpReady {
		return nil
	}
	snaps, err := s.store.ListAllSnapshots()
	if err != nil {
		return err
	}
	s.fpSeen = make(map[string]struct{}, len(snaps))
	for _, snap := range snaps {
		s.fpSeen[contentFingerprint(snap.Sin, json.RawMessage(snap.BDData))] = struct{}{}
	}
	s.fpReady = true
	return nil
}

// claimFingerprint 原子检查并占用指纹：已存在返回 true（重复）。
func (s *EliteService) claimFingerprint(fp string) bool {
	s.fpMu.Lock()
	defer s.fpMu.Unlock()
	if _, ok := s.fpSeen[fp]; ok {
		return true
	}
	s.fpSeen[fp] = struct{}{}
	return false
}

// releaseFingerprints 入库失败时释放本次占用的指纹（避免后续误判重复）。
func (s *EliteService) releaseFingerprints(fps []string) {
	s.fpMu.Lock()
	defer s.fpMu.Unlock()
	for _, fp := range fps {
		delete(s.fpSeen, fp)
	}
}

// trackFingerprints 入库成功后并入指纹缓存（客户端 Upload 等非 userBD 入库点维护缓存一致性）。
func (s *EliteService) trackFingerprints(snaps []*BuildSnapshot) {
	if len(snaps) == 0 {
		return
	}
	s.fpMu.Lock()
	defer s.fpMu.Unlock()
	if s.fpSeen == nil {
		return // 缓存未初始化：首次 initFingerprints 全库加载时会补上
	}
	for _, snap := range snaps {
		s.fpSeen[contentFingerprint(snap.Sin, json.RawMessage(snap.BDData))] = struct{}{}
	}
}

// contentFingerprint BD 内容指纹：sin + 排序后的 cardId 集合（对装配顺序不敏感）。
func contentFingerprint(sin string, bdData json.RawMessage) string {
	return sin + "|" + cardIDList(bdData)
}

// cardIDList 提取排序后的 cardId 逗号列表（指纹与日志展示共用；解析失败返回截断原文）。
func cardIDList(bdData json.RawMessage) string {
	var cards []struct {
		CardID string `json:"cardId"`
	}
	if err := json.Unmarshal(bdData, &cards); err != nil || len(cards) == 0 {
		s := string(bdData)
		if len(s) > 60 {
			s = s[:60] + "..."
		}
		return s
	}
	ids := make([]string, 0, len(cards))
	for _, c := range cards {
		ids = append(ids, c.CardID)
	}
	sort.Strings(ids)
	return strings.Join(ids, ",")
}

// bdDataSummary bdData 概要（skip 日志用，截断防刷屏）。
func bdDataSummary(bdData json.RawMessage) string {
	s := string(bdData)
	if len(s) > 60 {
		s = s[:60] + "..."
	}
	if s == "" {
		s = "<empty>"
	}
	return s
}
