using UnityEngine;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Singleton on the player that tracks permanent passive buffs from possessed enemies.
/// Buffs stack and persist across all forms (soul & possession).
/// Each passive type has its own prefab (you create them), instantiated under a HorizontalLayoutGroup parent.
/// </summary>
public class PlayerPassiveManager : MonoBehaviour
{
    public static PlayerPassiveManager Instance { get; private set; }

    [Header("Accumulated Buffs (read-only)")]
    public float totalMoveSpeedBonus = 0f;     // e.g. 0.05 = +5%
    public float totalLifestealBonus = 0f;     // e.g. 0.01 = +1%

    [Header("Base Stats (set from player components on Start)")]
    public float baseMoveSpeed = 5f;
    public float baseLifestealPercent = 0f;

    [Header("Passive UI")]
    public Transform passiveIconParent;             // HorizontalLayoutGroup parent
    public GameObject prideIconPrefab;              // your prefab for Pride passive
    public GameObject wrathIconPrefab;              // your prefab for Wrath passive

    // Track which passives have been obtained
    private HashSet<string> obtainedPassives = new HashSet<string>();

    // Runtime icon instances
    private Dictionary<string, PassiveIconUI> activeIcons = new Dictionary<string, PassiveIconUI>();

    private PlayerInputController input;
    private PlayerHealth health;

    class PassiveIconUI
    {
        public GameObject root;
        public TMP_Text stackText;
    }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        input = GetComponent<PlayerInputController>();
        health = GetComponent<PlayerHealth>();
        if (input != null) baseMoveSpeed = input.moveSpeed;
    }

    void Update()
    {
        // Apply move speed bonus every frame
        if (input != null)
        {
            input.moveSpeed = baseMoveSpeed * (1f + totalMoveSpeedBonus);
        }

        // Update stack count texts every frame
        foreach (var kvp in activeIcons)
        {
            var icon = kvp.Value;
            if (icon.stackText == null) continue;

            if (kvp.Key == "Pride")
                icon.stackText.text = Mathf.RoundToInt(totalMoveSpeedBonus * 20f).ToString();  // /0.05 = *20
            else if (kvp.Key == "Wrath")
                icon.stackText.text = Mathf.RoundToInt(totalLifestealBonus * 100f).ToString(); // /0.01 = *100
        }
    }

    /// <summary>
    /// Called when the player possesses an enemy.
    /// Scans the enemy for EnemyPassiveBuff components and accumulates their values.
    /// Updates UI immediately (doesn't wait for next frame).
    /// </summary>
    public void OnEnemyPossessed(Enemy enemy)
    {
        if (enemy == null) return;

        var passives = enemy.GetComponentsInChildren<EnemyPassiveBuff>(true);
        foreach (var p in passives)
        {
            if (p == null || !p.enabled) continue;

            totalMoveSpeedBonus += p.moveSpeedBonusPercent / 100f;
            totalLifestealBonus += p.lifestealBonusPercent / 100f;

            // Determine the passive key and which prefab to use
            string passiveKey = null;
            GameObject prefab = null;
            if (p.moveSpeedBonusPercent > 0f)
            {
                passiveKey = "Pride";
                prefab = prideIconPrefab;
            }
            else if (p.lifestealBonusPercent > 0f)
            {
                passiveKey = "Wrath";
                prefab = wrathIconPrefab;
            }

            // Show icon if first time obtaining this passive type
            if (passiveKey != null && !obtainedPassives.Contains(passiveKey))
            {
                obtainedPassives.Add(passiveKey);
                ShowPassiveIcon(passiveKey, prefab);
            }

            // Update icon text immediately (not waiting for Update)
            if (passiveKey != null && activeIcons.ContainsKey(passiveKey) && activeIcons[passiveKey].stackText != null)
            {
                if (passiveKey == "Pride")
                    activeIcons[passiveKey].stackText.text = Mathf.RoundToInt(totalMoveSpeedBonus * 20f).ToString();
                else if (passiveKey == "Wrath")
                    activeIcons[passiveKey].stackText.text = Mathf.RoundToInt(totalLifestealBonus * 100f).ToString();
            }
        }
    }

    void ShowPassiveIcon(string key, GameObject prefab)
    {
        if (passiveIconParent == null)
        {
            Debug.LogWarning("[PlayerPassive] passiveIconParent not assigned");
            return;
        }

        GameObject iconObj;
        if (prefab != null)
        {
            iconObj = Instantiate(prefab, passiveIconParent);
        }
        else
        {
            Debug.LogWarning("[PlayerPassive] No prefab for " + key + " — create an empty placeholder");
            iconObj = new GameObject(key + "Icon");
            iconObj.transform.SetParent(passiveIconParent, false);
        }

        var icon = new PassiveIconUI();
        icon.root = iconObj;
        icon.stackText = iconObj.GetComponentInChildren<TMP_Text>();

        activeIcons[key] = icon;
    }

    public float GetLifestealMultiplier()
    {
        return totalLifestealBonus;
    }
}
