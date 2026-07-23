using UnityEngine;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Singleton on the player that tracks permanent passive buffs from possessed enemies.
/// Buffs stack and persist across all forms (soul & possession).
/// </summary>
public class PlayerPassiveManager : MonoBehaviour
{
    public static PlayerPassiveManager Instance { get; private set; }

    [Header("Accumulated Buffs (read-only)")]
    public float totalMoveSpeedBonus = 0f;
    public float totalLifestealBonus = 0f;
    public float totalBurnBonus = 0f;
    [Tooltip("Burn VFX prefab to use when applying burn from any player attack.")]
    public GameObject burnVfxPrefab;

    [Header("Base Stats")]
    public float baseMoveSpeed = 5f;

    [Header("Passive UI")]
    public Transform passiveIconParent;
    public GameObject prideIconPrefab;
    public GameObject wrathIconPrefab;
    public GameObject envyIconPrefab;

    private HashSet<string> obtainedPassives = new HashSet<string>();
    private Dictionary<string, PassiveIconUI> activeIcons = new Dictionary<string, PassiveIconUI>();
    private PlayerInputController input;

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
        if (input != null) baseMoveSpeed = input.moveSpeed;
    }

    void Update()
    {
        if (input != null)
            input.moveSpeed = baseMoveSpeed * (1f + totalMoveSpeedBonus);

        foreach (var kvp in activeIcons)
        {
            var icon = kvp.Value;
            if (icon.stackText == null) continue;

            if (kvp.Key == "Pride")
                icon.stackText.text = Mathf.RoundToInt(totalMoveSpeedBonus * 20f).ToString();
            else if (kvp.Key == "Wrath")
                icon.stackText.text = Mathf.RoundToInt(totalLifestealBonus * 100f).ToString();
            else if (kvp.Key == "Envy")
                icon.stackText.text = Mathf.RoundToInt(totalBurnBonus * 100f).ToString();
        }
    }

    public void OnEnemyPossessed(Enemy enemy)
    {
        if (enemy == null) return;

        var passives = enemy.GetComponentsInChildren<EnemyPassiveBuff>(true);
        foreach (var p in passives)
        {
            if (p == null || !p.enabled) continue;

            totalMoveSpeedBonus += p.moveSpeedBonusPercent / 100f;
            totalLifestealBonus += p.lifestealBonusPercent / 100f;
            totalBurnBonus += p.burnBonusPercent / 100f;

            string passiveKey = null;
            GameObject prefab = null;

            if (p.moveSpeedBonusPercent > 0f) { passiveKey = "Pride"; prefab = prideIconPrefab; }
            if (p.lifestealBonusPercent > 0f) { passiveKey = "Wrath"; prefab = wrathIconPrefab; }
            if (p.burnBonusPercent > 0f) { passiveKey = "Envy"; prefab = envyIconPrefab; }

            if (passiveKey != null && !obtainedPassives.Contains(passiveKey))
            {
                obtainedPassives.Add(passiveKey);
                ShowPassiveIcon(passiveKey, prefab);
            }

            if (passiveKey != null && activeIcons.ContainsKey(passiveKey) && activeIcons[passiveKey].stackText != null)
            {
                if (passiveKey == "Pride")
                    activeIcons[passiveKey].stackText.text = Mathf.RoundToInt(totalMoveSpeedBonus * 20f).ToString();
                else if (passiveKey == "Wrath")
                    activeIcons[passiveKey].stackText.text = Mathf.RoundToInt(totalLifestealBonus * 100f).ToString();
                else if (passiveKey == "Envy")
                    activeIcons[passiveKey].stackText.text = Mathf.RoundToInt(totalBurnBonus * 100f).ToString();
            }
        }
    }

    void ShowPassiveIcon(string key, GameObject prefab)
    {
        if (passiveIconParent == null) return;

        GameObject iconObj;
        if (prefab != null)
            iconObj = Instantiate(prefab, passiveIconParent);
        else
        {
            iconObj = new GameObject(key + "Icon");
            iconObj.transform.SetParent(passiveIconParent, false);
        }

        var icon = new PassiveIconUI();
        icon.root = iconObj;
        icon.stackText = iconObj.GetComponentInChildren<TMP_Text>();
        activeIcons[key] = icon;
    }

    public float GetLifestealMultiplier() { return totalLifestealBonus; }
    public float GetBurnPercent() { return totalBurnBonus; }
    public GameObject GetBurnVfxPrefab() { return burnVfxPrefab; }
}
