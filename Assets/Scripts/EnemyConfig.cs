using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyConfig", menuName = "Game/Enemy Config")]
public class EnemyConfig : ScriptableObject
{
    [Header("Identity")]
    public string enemyName = "Enemy";
    
    [Header("Stats")]
    public float maxHealth = 100f;
    public float damage = 10f;
    public float moveSpeed = 1.5f;
    
    [Header("Combat")]
    public float attackRange = 0.8f;
    public bool isRanged = false;
    
    [Header("Visual")]
    public Color enemyColor = Color.red;
    public Color weakenedColor = new Color(1f, 0.5f, 0f);
}
