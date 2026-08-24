using UnityEngine;

[System.Serializable]
public class BossAbilityProfile
{
    public EnemyAbility ability;
    public string family;
    public float minRange;
    public float maxRange = 30f;
    public float preferredRange = 10f;
    public bool requiresLineOfSight;
    public bool requiresEnvyMark;
    public bool requiresLustAnchor;
    public float baseWeight = 1f;
    public float minimumRepeatGap = 1.5f;
    public int dangerClass;
}
