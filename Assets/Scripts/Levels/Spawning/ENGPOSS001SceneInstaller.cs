using UnityEngine;

/// <summary>Combat-scene binding for the ENG-POSS-001 runtime-owned systems.</summary>
public sealed class ENGPOSS001SceneInstaller : MonoBehaviour
{
    public GameObject bossPrefab;
    public PossessionImprintHUD imprintHudPrefab;


    void Awake()
    {
        MonsterSpawner.EnsureInstance();
        RunSpawnDirector.EnsureInstance().ConfigureBoss(bossPrefab);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (FindObjectOfType<MonsterPossessionCheat>() == null)
        {
            MonsterPossessionCheat bossCheat = gameObject.AddComponent<MonsterPossessionCheat>();
            bossCheat.bossSummonOnly = true;
        }
#endif
        PossessionImprintManager.EnsureInstance();
        if (imprintHudPrefab != null && FindObjectOfType<PossessionImprintHUD>() == null)
            Instantiate(imprintHudPrefab);
    }
}
