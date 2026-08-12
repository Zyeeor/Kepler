using UnityEngine;

/// <summary>
/// Destroy this GameObject when the referenced owner GameObject is destroyed.
/// Attach to any VFX instantiated by an ability.
/// </summary>
public class DestroyOnOwnerDeath : MonoBehaviour
{
    public GameObject owner;

    void Update()
    {
        if (owner == null)
        {
            Destroy(gameObject);
        }
    }
}
