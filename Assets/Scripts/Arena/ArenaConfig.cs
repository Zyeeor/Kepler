using UnityEngine;

[CreateAssetMenu(fileName = "ArenaConfig", menuName = "Game/Arena Config")]
public class ArenaConfig : ScriptableObject
{
    [Header("Arena")]
    public Vector2 arenaSize = new Vector2(40, 40);

    [Header("Spawn")]
    public float spawnRadius = 8f;

    [Header("Camera")]
    public Vector3 cameraOffset = new Vector3(0, 10, -10);
    public float cameraSize = 10f;
}
