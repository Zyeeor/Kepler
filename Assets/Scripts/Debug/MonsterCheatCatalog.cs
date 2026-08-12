using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ordered monster list for debug hotkeys. Index 0 maps to keyboard 1, index 1 to 2, etc.
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
