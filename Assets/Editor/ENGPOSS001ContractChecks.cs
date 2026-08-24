using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Batch-mode contract checks for ENG-POSS-001 in this asmdef-free project.</summary>
public static class ENGPOSS001ContractChecks
{
    public static void Run()
    {
        TransactionAlwaysGrantsTargetStack();
        GreedUsesPreviousStacksAndRewardsTarget();
        MultipliersRespectCaps();
        DifficultyTierBoundariesAreStable();
        AssetsAndSceneBindingsAreValid();
        Console.WriteLine("ENG-POSS-001 contract checks passed.");
    }

    static void TransactionAlwaysGrantsTargetStack()
    {
        int[] stacks = new int[8];
        float progress = 0f;
        PossessionImprintMath.ApplyTransaction(stacks, ref progress, SinType.Pride);
        Require(stacks[(int)SinType.Pride] == 1, "Pride base stack was not granted.");
        Require(stacks[(int)SinType.Greed] == 0, "Greed changed without progress.");
    }

    static void GreedUsesPreviousStacksAndRewardsTarget()
    {
        int[] stacks = new int[8];
        stacks[(int)SinType.Greed] = 20;
        float progress = 0f;
        PossessionImprintMath.ApplyTransaction(stacks, ref progress, SinType.Wrath);
        Require(stacks[(int)SinType.Wrath] == 2, "Greed bonus did not reward the possessed sin.");
        Require(stacks[(int)SinType.Greed] == 20, "Greed bonus recursively modified Greed.");
        Require(Math.Abs(progress) < 0.0001f, "Greed progress remainder is wrong.");
    }

    static void MultipliersRespectCaps()
    {
        Require(Approximately(MonsterSpawnDifficulty.DamageMultiplier(100), 2.2f), "Damage cap is wrong.");
        Require(Approximately(MonsterSpawnDifficulty.HealthMultiplier(100), 3f), "Health cap is wrong.");
        Require(PossessionImprintMath.MaxStacks == 100, "Seven-sin stack cap must default to 100.");
        Require(Approximately(PossessionImprintMath.SlothDrainMultiplier(100), 0.4f), "Sloth drain cap is wrong.");
        Require(Approximately(PossessionImprintMath.SlothDrainMultiplier(1000), 0.4f), "Sloth stack cap is not enforced.");
        Require(Approximately(PossessionImprintMath.LustControlChance(100), 0.3f), "Lust cap is wrong.");
    }

    static void DifficultyTierBoundariesAreStable()
    {
        Require(MonsterSpawnDifficulty.TierAt(29.999f) == 0, "Tier before 30 seconds is wrong.");
        Require(MonsterSpawnDifficulty.TierAt(30f) == 1, "Tier at 30 seconds is wrong.");
        Require(MonsterSpawnDifficulty.TierAt(479.999f) == 15, "Tier before boss time is wrong.");
        Require(MonsterSpawnDifficulty.TierAt(480f) == 16, "Tier at boss time is wrong.");
    }

    static void AssetsAndSceneBindingsAreValid()
    {
        GameObject boss = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Monster/Boss_Sevenfold_Convergence_new.prefab");
        Require(boss != null, "Boss prefab is missing.");
        Require(boss.GetComponent<BossSevenfoldActor>() != null, "Boss actor component is missing.");
        EnemyAbility[] abilities = boss.GetComponentsInChildren<EnemyAbility>(true);
        int nonMovement = 0;
        for (int i = 0; i < abilities.Length; i++)
            if (abilities[i] != null && abilities[i].type != EnemyAbility.AbilityType.Mobility) nonMovement++;
        Require(nonMovement == BossSevenfoldActor.AbilityCount, "Boss must contain fourteen non-movement abilities.");

        GameObject hud = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/HUD/PossessionImprintHUD.prefab");
        Require(hud != null, "Imprint HUD prefab is missing.");
        PossessionImprintHUD hudComponent = hud.GetComponent<PossessionImprintHUD>();
        Require(hudComponent != null && hudComponent.icons != null && hudComponent.icons.Length == 7,
            "Imprint HUD must bind seven icons.");

        EditorSceneManager.OpenScene("Assets/Scenes/CombatTest.unity", OpenSceneMode.Single);
        ENGPOSS001SceneInstaller installer = UnityEngine.Object.FindObjectOfType<ENGPOSS001SceneInstaller>();
        Require(installer != null && installer.bossPrefab != null && installer.imprintHudPrefab != null,
            "CombatTest scene installer references are incomplete.");

        EditorSceneManager.OpenScene("Assets/Scenes/EnemyAiTest.unity", OpenSceneMode.Single);
        installer = UnityEngine.Object.FindObjectOfType<ENGPOSS001SceneInstaller>();
        Require(installer != null && installer.bossPrefab != null && installer.imprintHudPrefab != null,
            "EnemyAiTest scene installer references are incomplete.");
    }

    static bool Approximately(float a, float b) => Math.Abs(a - b) < 0.0001f;

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
