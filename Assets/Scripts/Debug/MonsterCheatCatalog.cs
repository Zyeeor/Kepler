using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ordered monster list for debug hotkeys. Index 0 maps to keyboard 1, index 1 to 2, etc.
/// 【调试隔离豁免】本资产被生产功能 PossessionBodyProvider（雕像供躯体-随机模式）引用，
/// 已从"纯调试资产"转为内容配置资产，故不加 #if UNITY_EDITOR || DEVELOPMENT_BUILD。
/// </summary>
[CreateAssetMenu(fileName = "MonsterCheatCatalog", menuName = "Possession/Debug/Monster Cheat Catalog")]
public class MonsterCheatCatalog : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("Display name used in cheat logs / on-screen hint.")]
        public string displayName;
        [Tooltip("Monster prefab root (must contain MonsterActor / Enemy).")]
        public GameObject prefab;
    }

    [Tooltip("Hotkey order: entry[0]=1, entry[1]=2, ... up to 9.")]
    public List<Entry> monsters = new List<Entry>();
}
