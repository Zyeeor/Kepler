using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cue 集合（ScriptableObject，策划编辑）：Resources.Load("Narrative/NarrativeCueSet") 懒加载单例。
/// 策划增删 Cue = 增删列表项；cueId 唯一（Editor OnValidate 校验，Find 取首个确定性行为）。
/// </summary>
[CreateAssetMenu(menuName = "Kepler/Narrative/Cue Set", fileName = "NarrativeCueSet")]
public class NarrativeCueSet : ScriptableObject
{
    public List<NarrativeCue> cues = new List<NarrativeCue>();

    static NarrativeCueSet _instance;
    /// <summary>懒加载单例（缺失仅告警，调度器空转零开销）。</summary>
    public static NarrativeCueSet Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<NarrativeCueSet>("Narrative/NarrativeCueSet");
                if (_instance == null)
                    Debug.LogWarning("[Narrative] Resources 加载 NarrativeCueSet 失败（Narrative/NarrativeCueSet）——旁白系统空转，属预期（资产未建时）。");
            }
            return _instance;
        }
    }

    public NarrativeCue Find(string cueId)
    {
        if (string.IsNullOrEmpty(cueId)) return null;
        for (int i = 0; i < cues.Count; i++)
            if (cues[i] != null && cues[i].cueId == cueId) return cues[i];
        return null;
    }

    public void InvalidateCache() => _instance = null;

#if UNITY_EDITOR
    void OnValidate()
    {
        var seen = new HashSet<string>();
        foreach (var c in cues)
        {
            if (c == null) continue;
            if (string.IsNullOrEmpty(c.cueId))
                Debug.LogWarning("[NarrativeCueSet] 存在空 cueId 的 Cue，请补 ID。", this);
            else if (!seen.Add(c.cueId))
                Debug.LogWarning($"[NarrativeCueSet] 重复 cueId '{c.cueId}'，运行时取首个。", this);
        }
    }
#endif
}
