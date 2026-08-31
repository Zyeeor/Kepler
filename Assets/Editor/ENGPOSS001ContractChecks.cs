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
        stacks[(int)SinType.Greed] = 10;
        float progress = 0f;
        PossessionImprintMath.ApplyTransaction(stacks, ref progress, SinType.Wrath);
        Require(stacks[(int)SinType.Wrath] == 2, "Greed bonus did not reward the possessed sin.");
        Require(stacks[(int)SinType.Greed] == 10, "Greed bonus recursively modified Greed.");
        Require(Math.Abs(progress) < 0.0001f, "Greed progress remainder is wrong.");
        Require(Approximately(PossessionImprintMath.GreedProgressPerPossession(15), 1.5f),
            "Greed fractional progress is wrong.");
    }

    static void MultipliersRespectCaps()
    {
        Require(PossessionImprintMath.MaxStacks == 100, "Seven-sin stack cap must default to 100.");
        Require(Approximately(PossessionImprintMath.PrideCooldownMultiplier(20), 0.3f), "Pride 20-stack CDR is wrong.");
        Require(Approximately(PossessionImprintMath.WrathDamageMultiplier(20), 3f), "Wrath 20-stack damage is wrong.");
        Require(Approximately(PossessionImprintMath.GluttonyHealthMultiplier(20), 3f), "Gluttony 20-stack health is wrong.");
        Require(Approximately(PossessionImprintMath.GluttonyScaleMultiplier(20), 2f), "Gluttony 20-stack scale cap is wrong.");
        Require(Approximately(PossessionImprintMath.SlothDrainMultiplier(20), 0.3f), "Sloth 20-stack drain reduction is wrong.");
        Require(Approximately(PossessionImprintMath.SlothDrainMultiplier(1000), PossessionImprintMath.SlothDrainMultiplier(100)), "Sloth stack cap is not enforced.");
        Require(Approximately(PossessionImprintMath.EnvyMoveSpeedBonus(20), 0.20f), "Envy 20-stack move speed bonus is wrong.");
        Require(Approximately(PossessionImprintMath.EnvyMoveSpeedBonus(100), 0.50f), "Envy move speed cap is not enforced.");
        Require(Approximately(PossessionImprintMath.LustLifestealMultiplier(20), 0.2f), "Lust formula is wrong.");
        Require(Approximately(PossessionImprintMath.LustLifestealMultiplier(100), 1f), "Lust lifesteal formula is wrong.");
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
        PossessionImprintHUD sceneHud = UnityEngine.Object.FindObjectOfType<PossessionImprintHUD>();
        Require(installer != null && installer.bossPrefab != null && sceneHud != null,
            "CombatTest scene bindings are incomplete.");

        EditorSceneManager.OpenScene("Assets/Scenes/EnemyAiTest.unity", OpenSceneMode.Single);
        installer = UnityEngine.Object.FindObjectOfType<ENGPOSS001SceneInstaller>();
        sceneHud = UnityEngine.Object.FindObjectOfType<PossessionImprintHUD>();
        Require(installer != null && installer.bossPrefab != null && sceneHud != null,
            "EnemyAiTest scene bindings are incomplete.");

    }

    static bool Approximately(float a, float b) => Math.Abs(a - b) < 0.0001f;

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
