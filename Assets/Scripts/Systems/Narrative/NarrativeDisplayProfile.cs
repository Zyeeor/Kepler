using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Display Profile（SO）：全局默认 + 载体级覆盖 + Access→文本线映射。</summary>
[CreateAssetMenu(menuName = "Kepler/Narrative/Display Profile", fileName = "NarrativeDisplayProfile")]
public class NarrativeDisplayProfile : ScriptableObject
{
    [Tooltip("全局默认（载体无覆盖时）")]
    public TextLinePreference globalDefault = TextLinePreference.FollowAccess;

    [Tooltip("载体级覆盖（各自选择是否 Follow Access，契约 §3）")]
    public List<CarrierOverride> carrierOverrides = new List<CarrierOverride>();

    [Tooltip("FollowAccess 时的 Access→文本线映射（Dual-Line §4 节奏，策划可调）")]
    public List<AccessLineRule> accessLineMap = new List<AccessLineRule>();

    [Tooltip("已获得 Card 是否随 Access 切换包装文本（契约 §4 配置项）")]
    public bool syncOwnedCardsOnAccessChange = true;

    [Serializable] public class CarrierOverride { public NarrativeCarrier carrier; public TextLinePreference preference; }
    [Serializable] public class AccessLineRule { public NarrativeAccess access; public TextLinePreference line; }
}
