using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyConfig", menuName = "Game/Enemy Config")]
public class EnemyConfig : ScriptableObject
{
    [Header("Identity")]
    public string enemyName = "Enemy";
    
    [Header("Stats")]
    public float maxHealth = 100f;
    public float maxTenacity = 100f;
    public float moveSpeed = 1.5f;
    
    [Header("Combat")]
    public float detectionRadius = 8f;
    public float aiAttackRange = 3f;
    public float collisionDamage = 10f;
    public float attackSpeed = 1.0f;
    
    [Header("Visual")]
    public Color enemyColor = Color.red;
    public Color weakenedColor = new Color(1f, 0.5f, 0f);
    public Color downedColor = new Color(0.3f, 0.3f, 0.3f);
    public Color flashColor = Color.red;
    public float flashDuration = 0.1f;
}