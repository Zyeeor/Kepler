using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 词缀生成器自校验（EditMode / 开发期调用，非正式流程）。
///
/// 为什么不建 .asmdef 测试程序集：项目当前无任何测试程序集（仅第三方 LWGUI），
/// 为一个纯函数引入新程序集代价过高。改成本文件提供静态 Run()，
/// 由 Editor 菜单或 script-execute 调用，输出 PASS/FAIL 报告。
/// 后续若项目引入测试程序集，可直接把断言迁为 NUnit [Test]。
///
/// 覆盖设计文档 §6.2 的 5 项单测重点 + Wire 编解码（验收项 15/16）。
/// </summary>
public static class CardEpithetSelfTest
{
    /// <summary>运行全部断言，返回可读报告。</summary>
    public static string Run(CardEpithetCatalog catalog)
    {
        var sb = new System.Text.StringBuilder();
        int pass = 0, fail = 0;

        void Check(string name, bool ok, string detail = "")
        {
            if (ok) { pass++; sb.AppendLine($"  PASS  {name}"); }
            else { fail++; sb.AppendLine($"  FAIL  {name}   {detail}"); }
        }

        sb.AppendLine("═══ CardEpithetGenerator 自校验 ═══");
        sb.AppendLine();

        if (catalog == null)
        {
            sb.AppendLine("  SKIP  全部用例：未配置 CardEpithetCatalog");
            sb.AppendLine();
            sb.AppendLine("提示：先用 Kepler > Cards > Epithet 词缀表 创建目录并填充词表。");
            return sb.ToString();
        }

        var allIds = new List<string>();
        foreach (var e in catalog.entries)
            if (e != null && !string.IsNullOrEmpty(e.effectId)) allIds.Add(e.effectId);

        if (allIds.Count == 0)
        {
            sb.AppendLine("  SKIP  全部用例：词缀目录为空（请先「同步卡池」并填词）");
            return sb.ToString();
        }

        // ── 1. 确定性：同集合乱序输入 → 同输出（1000 次）──
        {
            var rng = new System.Random(20260827);
            string baseline = string.Join("|", CardEpithetGenerator.Generate(allIds, catalog));
            bool stable = true;
            for (int i = 0; i < 1000; i++)
            {
                var shuffled = new List<string>(allIds);
                for (int j = shuffled.Count - 1; j > 0; j--)
                {
                    int k = rng.Next(j + 1);
                    string t = shuffled[j]; shuffled[j] = shuffled[k]; shuffled[k] = t;
                }
                if (string.Join("|", CardEpithetGenerator.Generate(shuffled, catalog)) != baseline)
                { stable = false; break; }
            }
            Check("确定性：1000 次乱序输入结果恒定", stable, $"baseline=[{baseline}]");
        }

        // ── 2. 词数边界：1/2/3/4/8 张卡 → 1/2/3/4/4 词 ──
        {
            int max = catalog.maxEpithetCount > 0 ? catalog.maxEpithetCount : 4;
            var expected = new Dictionary<int, int> { { 1, 1 }, { 2, 2 }, { 3, 3 }, { 4, 4 }, { 8, max } };
            bool ok = true;
            string detail = "";
            foreach (var kv in expected)
            {
                var subset = new List<string>();
                for (int i = 0; i < kv.Key && i < allIds.Count; i++) subset.Add(allIds[i]);
                if (allIds.Count < kv.Key) continue;   // 词表不足时跳过该项
                int got = CardEpithetGenerator.Generate(subset, catalog).Length;
                if (got != kv.Value)
                {
                    ok = false;
                    detail += $"[输入{kv.Key}张→得{got}词,期望{kv.Value}] ";
                }
            }
            Check($"词数边界：1/2/3/4/8 卡 → 1/2/3/4/{max} 词", ok, detail);
        }

        // ── 3. 失效 ID 过滤 ──
        {
            var mixed = new List<string>(allIds) { "ZZ-NOT-EXIST-01", "", "XX-FAKE-99" };
            var words = CardEpithetGenerator.Generate(mixed, catalog);
            var baseline = CardEpithetGenerator.Generate(allIds, catalog);
            Check("失效 ID 过滤：混入不存在 ID 不影响结果",
                string.Join("|", words) == string.Join("|", baseline),
                $"got=[{string.Join("|", words)}] base=[{string.Join("|", baseline)}]");
        }

        // ── 4. Type Growth 强制入选 ──
        {
            var tg = new List<string>();
            foreach (var id in allIds)
            {
                var parts = id.Split('-');
                if (parts.Length >= 2 && parts[1].StartsWith("TG", StringComparison.OrdinalIgnoreCase))
                    tg.Add(id);
            }
            if (tg.Count > 0)
            {
                // 构造：TG + 多张满上限的其他卡（若 TG 权重非最高，仍须入选）
                var probe = new List<string>(tg);
                int others = 0;
                foreach (var id in allIds)
                {
                    if (probe.Contains(id)) continue;
                    probe.Add(id);
                    if (++others >= 8) break;
                }
                var words = CardEpithetGenerator.Generate(probe, catalog);
                var tgWords = new HashSet<string>();
                foreach (var id in tg)
                {
                    var we = catalog.Find(id);
                    if (we == null) continue;
                    var te = FindTextEntry(CardEpithetCatalog.ResolveTextKey(we));
                    if (te != null && !string.IsNullOrEmpty(te.text)) tgWords.Add(te.text);
                }
                bool contains = false;
                foreach (var w in words) if (tgWords.Contains(w)) { contains = true; break; }
                Check("Type Growth 强制入选（权重 1000）", contains,
                    $"TG词=[{string.Join(",", tgWords)}] 结果=[{string.Join("|", words)}]");
            }
            else
            {
                sb.AppendLine("  SKIP  Type Growth 强制入选：词表中无 TG 卡");
            }
        }

        // ── 5. tie-break 稳定（重复调用同集合结果一致）──
        {
            var a = CardEpithetGenerator.Generate(allIds, catalog);
            System.Threading.Thread.Sleep(1);
            var b = CardEpithetGenerator.Generate(allIds, catalog);
            Check("tie-break 稳定：重复调用结果一致（无时间/随机依赖）",
                string.Join("|", a) == string.Join("|", b));
        }

        // ── 6. Wire 编解码往返（验收项 13/14）──
        {
            var words = CardEpithetGenerator.Generate(allIds, catalog);
            string wire = CardEpithetGenerator.EncodeForWire("gluttony", words);
            bool decoded = CardEpithetGenerator.TryDecodeFromWire(wire, out string sin, out string[] back);
            Check("Wire 往返：编码→解码 词序列一致",
                decoded && sin == "gluttony" && string.Join("|", back) == string.Join("|", words),
                $"wire={wire}");
        }

        // ── 7. 旧格式快照降级（验收项 15）──
        {
            bool decoded = CardEpithetGenerator.TryDecodeFromWire("暴食-魔猫", out _, out _);
            Check("旧格式降级：无分隔符的 catalog 名返回 false（不报错、不乱码）", !decoded);
        }

        // ── 8. 空输入兜底（验收项 16 / §7.3）──
        {
            var empty = CardEpithetGenerator.Generate(new string[0], catalog);
            string name = CardEpithetGenerator.Format("pride", empty, 0, catalog);
            Check("空输入兜底：不抛异常且输出仅中心词（不虚构词缀）",
                empty.Length == 0 && !string.IsNullOrEmpty(name) && !name.Contains("之"),
                $"name=[{name}]");
        }

        sb.AppendLine();
        sb.AppendLine($"═══ 结果：{pass} 通过 / {fail} 失败 ═══");
        return sb.ToString();
    }

    static TextEntry FindTextEntry(string key)
    {
        var tc = TextCatalog.Instance;
        if (tc == null) return null;
        if (tc.entries != null)
            foreach (var e in tc.entries)
                if (e != null && e.key == key) return e;
        if (tc.sections != null)
            foreach (var s in tc.sections)
            {
                if (s == null || s.entries == null) continue;
                foreach (var e in s.entries)
                    if (e != null && e.key == key) return e;
            }
        return null;
    }
}
