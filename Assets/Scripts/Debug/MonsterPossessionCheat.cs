#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Play-mode cheat panel for monster skill / build testing.
/// - Number key alone (1-7): spawn catalog monster and instantly possess it.
/// - Number key 9: spawn a random Elite enemy from the EliteMonsterCatalog.
/// - Number key 0: spawn a random catalog monster as a normal enemy (no possess).
/// - While possessed, hold a skill key + number: unlock the Nth CardLibrary build entry
///   that targets that skill's abilities (1-based index in filtered CardLibrary order).
/// Skill keys: LMB = BasicAttack, RMB = Skill, Space = Mobility.

/// Cheat-possessed bodies receive permanent Effect.Defense.DamageImmune.
/// </summary>
public class MonsterPossessionCheat : MonoBehaviour
{
    [Header("Catalog")]
    public MonsterCheatCatalog catalog;

    [Header("Spawn")]
    [Tooltip("Spawn offset from the current player body / soul.")]
    public Vector3 spawnOffset = new Vector3(2f, 0f, 0f);
    [Tooltip("刷怪统一高度（世界 y）：灵魂玩家悬浮，怪跟随会刷在空中；此值强制刷怪高度（默认 0，按地面实际高度手动调整）。")]
    public float spawnHeightY = 0f;
    [Tooltip("If true, previously cheat-spawned possessed bodies are despawned when spawning a new one.")]
    public bool despawnPreviousCheatBody = true;
    [Tooltip("Apply permanent damage-immune Effect to cheat-possessed bodies.")]
    public bool immortalCheatBodies = true;
    [Tooltip("Permanent damage immunity Effect (duration <= 0). Auto-loaded from Resources/Assets if empty.")]
    public GameplayEffectDefinition damageImmuneEffect;

    [Header("Input")]
    public bool enableCheats = true;
    public bool showOnScreenHint = true;
    [Tooltip("When enabled, this instance only handles the Boss summon hotkey [8].")]
    public bool bossSummonOnly;

    private MonsterActor lastCheatBody;
    private string lastStatus = "MonsterPossessionCheat ready.";

    void Awake()
    {
        EnsureDamageImmuneEffect();
    }

    void Update()
    {
        if (!enableCheats || GameManager.IsFormalFlow) return; // 正式流程屏蔽作弊
        if (PlayerController.IsGameplayInputBlocked) return;
        if (GameManager.Instance != null && GameManager.Instance.currentState == GameManager.GameState.GameOver) return;

        if (!TryReadNumberKeyDown(out int number)) return;

        if (number == 0)
        {
            TrySpawnRandomEnemy();
            return;
        }

        if (number == 8)
        {
            TrySummonBoss();
            return;
        }

        if (number == 9)
        {
            if (bossSummonOnly) return;
            if (TryGetHeldSkillType(out EnemyAbility.AbilityType skillTypeForNine))
            {
                TryUnlockBuildEntry(skillTypeForNine, number);
                return;
            }
            TrySpawnRandomElite();
            return;
        }

        if (bossSummonOnly) return;

        if (TryGetHeldSkillType(out EnemyAbility.AbilityType skillType))
        {
            TryUnlockBuildEntry(skillType, number);
            return;
        }

        TrySpawnAndPossess(number);
    }

    void OnGUI()
    {
        if (!enableCheats || !showOnScreenHint || bossSummonOnly || GameManager.IsFormalFlow) return; // 正式流程屏蔽屏幕提示

        const float width = 560f;
        List<string> buildLines = BuildHintLines(out string skillLabel);
        float height = 70f + (catalog != null && catalog.monsters != null ? Mathf.Min(catalog.monsters.Count, 9) * 16f : 0f) + buildLines.Count * 16f + 28f;
        GUI.Box(new Rect(10f, 10f, width, height), "Monster Possession Cheat");
        GUI.Label(new Rect(18f, 32f, width - 24f, 20f), "0 random enemy | 1-7 spawn+possess | 8 summon Boss | 9 random Elite | hold LMB/RMB/Space + 1-9 unlock");

        float y = 52f;
        if (catalog != null && catalog.monsters != null)
        {
            for (int i = 0; i < catalog.monsters.Count && i < 9; i++)
            {
                MonsterCheatCatalog.Entry entry = catalog.monsters[i];
                string name = entry != null && !string.IsNullOrEmpty(entry.displayName)
                    ? entry.displayName
                    : (entry != null && entry.prefab != null ? entry.prefab.name : "(empty)");
                GUI.Label(new Rect(18f, y, width - 24f, 18f), $"{i + 1}: {name}");
                y += 16f;
            }
        }

        if (!string.IsNullOrEmpty(skillLabel))
        {
            GUI.Label(new Rect(18f, y + 2f, width - 24f, 18f), skillLabel);
            y += 18f;
            for (int i = 0; i < buildLines.Count; i++)
            {
                GUI.Label(new Rect(18f, y, width - 24f, 18f), buildLines[i]);
                y += 16f;
            }
        }

        GUI.Label(new Rect(18f, y + 4f, width - 24f, 20f), lastStatus);
    }

    private List<string> BuildHintLines(out string skillLabel)
    {
        skillLabel = null;
        var lines = new List<string>();
        if (!TryGetHeldSkillType(out EnemyAbility.AbilityType skillType)) return lines;
        if (PossessionManager.Instance == null || PossessionManager.Instance.CurrentBody == null) return lines;
        if (CardManager.Instance == null) return lines;

        skillLabel = $"Builds for {skillType}:";
        List<EnemyAbility> abilities = CollectAbilitiesOfType(PossessionManager.Instance.CurrentBody, skillType);
        List<CardData> cards = CardManager.Instance.GetCardsTargetingAbilities(abilities);
        if (cards.Count == 0)
        {
            lines.Add("(none in CardLibrary)");
            return lines;
        }

        for (int i = 0; i < cards.Count && i < 9; i++)
        {
            CardData card = cards[i];
            bool unlocked = CardManager.Instance.IsEffectUnlocked(card.effectId);
            lines.Add($"{i + 1}: {card.cardName} [{card.effectId}]{(unlocked ? " ✓" : "")}");
        }
        return lines;
    }

    private void TrySpawnRandomEnemy()
    {
        if (!TryPickRandomCatalogEntry(out MonsterCheatCatalog.Entry entry, out string label))
        {
            SetStatus("No monster available to spawn with 0.");
            return;
        }

        Vector3 spawnPos = ResolveSpawnPosition();
        GameObject go = MonsterPool.Instance.Spawn(entry.prefab, spawnPos, Quaternion.identity);
        if (go == null)
        {
            SetStatus($"Random spawn failed for '{entry.prefab.name}'.");
            return;
        }

        go.tag = "Enemy";
        MonsterActor monster = go.GetComponentInChildren<MonsterActor>(true);
        if (monster == null)
        {
            SetStatus($"Spawned '{go.name}' has no MonsterActor.");
            return;
        }

        if (CardManager.Instance != null) CardManager.Instance.ApplyAllUnlocksTo(go);
        SetStatus($"Spawned enemy [0] {label}");
    }

    private void TrySpawnAndPossess(int number)
    {
        if (catalog == null || catalog.monsters == null || catalog.monsters.Count == 0)
        {
            SetStatus("No MonsterCheatCatalog assigned.");
            return;
        }

        int index = number - 1;
        if (index < 0 || index >= catalog.monsters.Count)
        {
            SetStatus($"No monster mapped to key {number}.");
            return;
        }

        MonsterCheatCatalog.Entry entry = catalog.monsters[index];
        if (entry == null || entry.prefab == null)
        {
            SetStatus($"Monster slot {number} has no prefab.");
            return;
        }

        if (PossessionManager.Instance == null)
        {
            SetStatus("PossessionManager missing.");
            return;
        }

        MonsterActor previousCheatBody = lastCheatBody;

        Vector3 spawnPos = ResolveSpawnPosition();
        GameObject go = MonsterPool.Instance.Spawn(entry.prefab, spawnPos, Quaternion.identity);
        if (go == null)
        {
            SetStatus($"Spawn failed for '{entry.prefab.name}'.");
            return;
        }

        go.tag = "Enemy";
        MonsterActor monster = go.GetComponentInChildren<MonsterActor>(true);
        if (monster == null)
        {
            SetStatus($"Spawned '{go.name}' has no MonsterActor.");
            return;
        }

        monster.ResolveSinIdentityFromHint(entry.prefab.name + " " + entry.displayName);
        if (immortalCheatBodies) monster.suppressPossessionDrain = true;
        if (CardManager.Instance != null) CardManager.Instance.ApplyAllUnlocksTo(go);

        // Cheat 1-7 is the test path for the real possession/imprint feature, so it must
        // publish PlayerPossession rather than the manager's non-progression Debug reason.
        if (!PossessionManager.Instance.DebugForcePossess(monster, PossessionGrantReason.PlayerPossession))
        {
            SetStatus($"Spawned '{monster.displayName}' but force-possess failed.");
            return;
        }

        if (immortalCheatBodies) ApplyDamageImmune(monster);
        else ClearDamageImmune(monster);

        // Always strip immortality from the previous cheat body, then despawn if requested.
        if (previousCheatBody != null && previousCheatBody != monster)
        {
            ClearDamageImmune(previousCheatBody);
            previousCheatBody.suppressPossessionDrain = false;
            if (despawnPreviousCheatBody && !previousCheatBody.IsElite && previousCheatBody.gameObject.activeInHierarchy)
                previousCheatBody.BeginDisappearing();
        }

        lastCheatBody = monster;
        string label = !string.IsNullOrEmpty(entry.displayName) ? entry.displayName : monster.displayName;
        SetStatus($"Possessed [{number}] {label}{(immortalCheatBodies ? " (damage immune)" : "")}");
    }

    private void TrySpawnRandomElite()
    {
        EliteBuildDirector director = EliteBuildDirector.EnsureInstance();
        EliteMonsterCatalog eliteCatalog = director.catalog;
        if (eliteCatalog == null)
        {
            SetStatus("EliteMonsterCatalog missing.");
            return;
        }

        EliteSnapshotItem snapshot = eliteCatalog.PickPresetSnapshot();
        if (snapshot == null)
        {
            SetStatus("Elite catalog has no valid preset snapshot.");
            return;
        }

        EliteMonsterCatalog.Entry entry = eliteCatalog.FindByWireName(snapshot.sin);
        if (entry == null || entry.prefab == null)
        {
            SetStatus($"Elite preset '{snapshot.sin}' has no prefab entry.");
            return;
        }

        MonsterSpawner spawner = MonsterSpawner.EnsureInstance();
        MonsterActor monster = spawner.SpawnEliteMonster(entry.prefab, ResolveSpawnPosition());
        if (monster == null)
        {
            SetStatus($"Elite spawn failed for '{entry.prefab.name}'.");
            return;
        }

        EliteBuildCarrier carrier = monster.gameObject.AddComponent<EliteBuildCarrier>();
        carrier.Init(snapshot, entry.displayName);
        director.ApplyEliteRuntimeSettings(monster);
        director.AnnounceEliteSpawn(monster, entry.displayName);
        string label = !string.IsNullOrEmpty(entry.displayName) ? entry.displayName : monster.displayName;
        SetStatus($"Spawned random Elite '{label}' (HP x{director.eliteHealthMultiplier:0.##}, ATK x{director.eliteAttackDamageMultiplier:0.##}, Scale x{director.eliteVisualScaleMultiplier:0.##})");
    }

    private void TrySummonBoss()
    {
        RunSpawnDirector director = RunSpawnDirector.EnsureInstance();
        if (director.bossPrefab == null)
        {
            SetStatus("Boss prefab is not bound in the active combat scene.");
            return;
        }
        if (director.DebugSpawnBossNow())
            SetStatus("Boss summoned with key [8].");
        else
            SetStatus("Boss summon failed (already spawned, no legal spawn point, or quota full).");
    }

    private void ApplyDamageImmune(MonsterActor monster)
    {
        EnsureDamageImmuneEffect();
        if (monster == null || damageImmuneEffect == null) return;

        CombatAbilityComponent combat = monster.Combat;
        if (combat == null) combat = monster.GetComponentInChildren<CombatAbilityComponent>(true);
        if (combat == null)
        {
            Debug.LogWarning("[MonsterPossessionCheat] Cannot apply damage immune: CombatAbilityComponent missing.");
            return;
        }

        if (!combat.ApplyEffect(damageImmuneEffect))
            Debug.LogWarning("[MonsterPossessionCheat] Failed to apply DamageImmune effect.");
    }

    private void ClearDamageImmune(MonsterActor monster)
    {
        if (monster == null) return;
        monster.suppressPossessionDrain = false;
        CombatAbilityComponent combat = monster.Combat;
        if (combat == null) combat = monster.GetComponentInChildren<CombatAbilityComponent>(true);
        if (combat == null) return;
        if (damageImmuneEffect != null) combat.RemoveEffect(damageImmuneEffect);
        combat.RemoveEffectsWithTag("Effect.Defense.DamageImmune");
    }

    private void EnsureDamageImmuneEffect()
    {
        if (damageImmuneEffect != null) return;

        if (CardManager.Instance != null &&
            CardManager.Instance.TryGetGameplayEffect("Effect.Defense.DamageImmune", out GameplayEffectDefinition fromCatalog))
        {
            damageImmuneEffect = fromCatalog;
            return;
        }

#if UNITY_EDITOR
        damageImmuneEffect = UnityEditor.AssetDatabase.LoadAssetAtPath<GameplayEffectDefinition>(
            "Assets/Combat/Effects/Effect_Defense_DamageImmune.asset");
#endif
    }

    private bool TryPickRandomCatalogEntry(out MonsterCheatCatalog.Entry entry, out string label)
    {
        entry = null;
        label = null;
        if (catalog == null || catalog.monsters == null || catalog.monsters.Count == 0) return false;

        var valid = new List<MonsterCheatCatalog.Entry>();
        for (int i = 0; i < catalog.monsters.Count; i++)
        {
            MonsterCheatCatalog.Entry candidate = catalog.monsters[i];
            if (candidate != null && candidate.prefab != null) valid.Add(candidate);
        }
        if (valid.Count == 0) return false;

        entry = valid[Random.Range(0, valid.Count)];
        label = !string.IsNullOrEmpty(entry.displayName) ? entry.displayName : entry.prefab.name;
        return true;
    }

    private void TryUnlockBuildEntry(EnemyAbility.AbilityType skillType, int number)
    {
        PossessionManager possession = PossessionManager.Instance;
        if (possession == null || possession.State != PossessionManager.SwitchState.Possessing || possession.CurrentBody == null)
        {
            SetStatus("Hold skill+number only works while possessed.");
            return;
        }

        if (CardManager.Instance == null)
        {
            SetStatus("CardManager missing.");
            return;
        }

        List<EnemyAbility> abilities = CollectAbilitiesOfType(possession.CurrentBody, skillType);
        if (abilities.Count == 0)
        {
            SetStatus($"Current body has no {skillType} ability.");
            return;
        }

        List<CardData> cards = CardManager.Instance.GetCardsTargetingAbilities(abilities);
        int index = number - 1;
        if (cards.Count == 0)
        {
            SetStatus($"{skillType}: CardLibrary has no matching build cards.");
            return;
        }
        if (index < 0 || index >= cards.Count)
        {
            SetStatus($"{skillType} has {cards.Count} build entries; no slot {number}.");
            return;
        }

        CardData card = cards[index];
        if (CardManager.Instance.IsEffectUnlocked(card.effectId))
        {
            SetStatus($"Already unlocked: {card.cardName} ({card.effectId})");
            return;
        }

        CardManager.Instance.UnlockEffect(card.effectId);
        SetStatus($"Unlocked [{number}] {card.cardName} ({card.effectId}) for {skillType}");
    }

    private static List<EnemyAbility> CollectAbilitiesOfType(MonsterActor body, EnemyAbility.AbilityType skillType)
    {
        var result = new List<EnemyAbility>();
        if (body == null) return result;
        EnemyAbility[] all = body.GetComponentsInChildren<EnemyAbility>(true);
        for (int i = 0; i < all.Length; i++)
        {
            EnemyAbility ability = all[i];
            if (ability != null && ability.type == skillType) result.Add(ability);
        }
        return result;
    }

    private Vector3 ResolveSpawnPosition()
    {
        Transform anchor = null;
        if (PossessionManager.Instance != null && PossessionManager.Instance.CurrentBody != null)
            anchor = PossessionManager.Instance.CurrentBody.transform;
        else
        {
            SoulActor soul = FindObjectOfType<SoulActor>();
            if (soul != null) anchor = soul.transform;
            else if (PlayerController.Instance != null) anchor = PlayerController.Instance.transform;
        }

        Vector3 origin = anchor != null ? anchor.position : transform.position;
        Vector3 pos = origin + spawnOffset;
        pos.y = spawnHeightY; // 统一高度（灵魂悬浮，怪不跟随悬浮）
        return pos;
    }

    private static bool TryReadNumberKeyDown(out int number)
    {
        number = -1;
        if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0))
        {
            number = 0;
            return true;
        }

        for (int i = 1; i <= 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0 + i) || Input.GetKeyDown(KeyCode.Keypad0 + i))
            {
                number = i;
                return true;
            }
        }
        return false;
    }

    private static bool TryGetHeldSkillType(out EnemyAbility.AbilityType skillType)
    {
        // Prefer RMB / Space over LMB so accidental left mouse hold does not steal build hotkeys.
        if (Input.GetMouseButton(1) || Input.GetMouseButtonDown(1))

        {
            skillType = EnemyAbility.AbilityType.Skill;
            return true;
        }
        if (Input.GetKey(KeyCode.Space) || Input.GetKeyDown(KeyCode.Space))
        {
            skillType = EnemyAbility.AbilityType.Mobility;
            return true;
        }
        if (Input.GetMouseButton(0) || Input.GetMouseButtonDown(0))
        {
            skillType = EnemyAbility.AbilityType.BasicAttack;
            return true;
        }

        skillType = EnemyAbility.AbilityType.Passive;
        return false;
    }

    private void SetStatus(string message)
    {
        lastStatus = message;
        Debug.Log("[MonsterPossessionCheat] " + message);
    }
}
#endif
