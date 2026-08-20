using UnityEngine;

/// <summary>
/// Marks a GameObject as owned by <see cref="VfxPool"/>. Stores the source prefab and original local scale
/// so reuse never compounds transforms or loses the pool mapping.
/// </summary>
public class PooledObject : MonoBehaviour
{
    public GameObject SourcePrefab;
    public Vector3 OriginalLocalScale = Vector3.one;
}
