using UnityEngine;

/// <summary>Combat-scene binding for the ENG-POSS-001 runtime-owned systems.</summary>
public sealed class ENGPOSS001SceneInstaller : MonoBehaviour
{
    public GameObject bossPrefab;
    public PossessionImprintHUD imprintHudPrefab;

    void Awake()
    {
        RunSpawnDirector.EnsureInstance().ConfigureBoss(bossPrefab);
        PossessionImprintManager.EnsureInstance();
        if (imprintHudPrefab != null && FindObjectOfType<PossessionImprintHUD>() == null)
            Instantiate(imprintHudPrefab);
    }
}
