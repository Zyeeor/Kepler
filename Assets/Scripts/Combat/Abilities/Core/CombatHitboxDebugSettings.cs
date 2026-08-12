using UnityEngine;

/// <summary>Attach to any active debug GameObject to expose the global hitbox toggle in the Inspector.</summary>
public class CombatHitboxDebugSettings : MonoBehaviour
{
    public bool enableHitboxDebug;

    private void OnEnable()
    {
        CombatHitboxDebug.Enabled = enableHitboxDebug;
    }

    private void Update()
    {
        CombatHitboxDebug.Enabled = enableHitboxDebug;
    }

    private void OnDisable()
    {
        CombatHitboxDebug.Enabled = false;
    }
}
