using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Designer-facing catalog for the project's valid Gameplay Tag tree and Effect Tag directory.
/// Runtime Tag matching remains string-based; Effect lookup is explicit through this assigned asset.
/// </summary>
[CreateAssetMenu(fileName = "GameplayTagCatalog", menuName = "Possession/Combat/Gameplay Tag Catalog")]
public class GameplayTagCatalog : ScriptableObject
{
    [Tooltip("One dot-separated tag per item. Parent tags may be listed independently for documentation and filtering.")]
    public List<string> declaredTags = new List<string>();

    [Header("Effect Tag Directory")]
    [Tooltip("Gameplay Effect assets indexed by their unique effectTag. Assign this catalog to CardManager or GameplayEffectApplier to resolve Effects from Tags.")]
    public List<GameplayEffectDefinition> effectDefinitions = new List<GameplayEffectDefinition>();

    public bool Contains(string tag)
    {
        string normalized = GameplayTagUtility.Normalize(tag);
        return declaredTags.Exists(value => string.Equals(GameplayTagUtility.Normalize(value), normalized, System.StringComparison.OrdinalIgnoreCase));
    }

    public bool TryGetEffect(string effectTag, out GameplayEffectDefinition definition)
    {
        string normalized = GameplayTagUtility.Normalize(effectTag);
        foreach (GameplayEffectDefinition candidate in effectDefinitions)
        {
            if (candidate == null) continue;
            if (string.Equals(candidate.effectTag, normalized, System.StringComparison.OrdinalIgnoreCase))
            {
                definition = candidate;
                return true;
            }
        }

        definition = null;
        return false;
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

        var uniqueEffectTags = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        for (int i = effectDefinitions.Count - 1; i >= 0; i--)
        {
            GameplayEffectDefinition definition = effectDefinitions[i];
            if (definition == null)
            {
                effectDefinitions.RemoveAt(i);
                continue;
            }

            string effectTag = GameplayTagUtility.Normalize(definition.effectTag);
            if (string.IsNullOrEmpty(effectTag) || !uniqueEffectTags.Add(effectTag))
            {
                effectDefinitions.RemoveAt(i);
                continue;
            }
        }
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
