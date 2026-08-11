using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Optional designer-facing catalog for the project's valid Gameplay Tag tree.
/// This asset validates spelling only; runtime matching remains string-based to keep the minigame workflow lightweight.
/// </summary>
[CreateAssetMenu(fileName = "GameplayTagCatalog", menuName = "Possession/Combat/Gameplay Tag Catalog")]
public class GameplayTagCatalog : ScriptableObject
{
    [Tooltip("One dot-separated tag per item. Parent tags may be listed independently for documentation and filtering.")]
    public List<string> declaredTags = new List<string>();

    public bool Contains(string tag)
    {
        string normalized = GameplayTagUtility.Normalize(tag);
        return declaredTags.Exists(value => string.Equals(GameplayTagUtility.Normalize(value), normalized, System.StringComparison.OrdinalIgnoreCase));
    }

    private void OnValidate()
    {
        var uniqueTags = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        for (int i = declaredTags.Count - 1; i >= 0; i--)
        {
            string normalized = GameplayTagUtility.Normalize(declaredTags[i]);
            if (string.IsNullOrEmpty(normalized) || !uniqueTags.Add(normalized))
            {
                declaredTags.RemoveAt(i);
                continue;
            }
            declaredTags[i] = normalized;
        }
        declaredTags.Sort(System.StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>Optional prefab authoring component for persistent actor tags.</summary>
public class GameplayTagAuthoring : MonoBehaviour
{
    [Tooltip("Tags granted while this actor is enabled, for example Actor.Monster or Faction.Enemy.")]
    public List<string> initialTags = new List<string>();

    private CombatAbilityComponent combat;

    private void Awake()
    {
        combat = GetComponent<CombatAbilityComponent>();
        if (combat == null) combat = gameObject.AddComponent<CombatAbilityComponent>();
        combat.AddLooseTags(this, initialTags);
    }

    private void OnDisable()
    {
        if (combat != null) combat.RemoveLooseTags(this);
    }
}
