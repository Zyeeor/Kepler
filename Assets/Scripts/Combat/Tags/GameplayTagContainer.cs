using System;
using System.Collections.Generic;

/// <summary>
/// Runtime tag set with source ownership and reference counting.
/// Multiple abilities/effects may grant the same tag; removing one source never removes another source's tag.
/// </summary>
public sealed class GameplayTagContainer
{
    private readonly Dictionary<string, int> tagCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<object, List<string>> tagsBySource = new Dictionary<object, List<string>>();

    public event Action OnChanged;

    public IEnumerable<string> ActiveTags
    {
        get { return tagCounts.Keys; }
    }

    public bool HasTag(string queryTag)
    {
        foreach (string ownedTag in tagCounts.Keys)
        {
            if (GameplayTagUtility.Matches(ownedTag, queryTag)) return true;
        }
        return false;
    }

    public bool HasAny(IEnumerable<string> queryTags)
    {
        return GameplayTagUtility.HasAny(tagCounts.Keys, queryTags);
    }

    public bool HasAll(IEnumerable<string> queryTags)
    {
        return GameplayTagUtility.HasAll(tagCounts.Keys, queryTags);
    }

    public void AddTags(object source, IEnumerable<string> tags)
    {
        if (source == null || tags == null) return;

        List<string> sourceTags;
        if (!tagsBySource.TryGetValue(source, out sourceTags))
        {
            sourceTags = new List<string>();
            tagsBySource.Add(source, sourceTags);
        }

        bool changed = false;
        foreach (string rawTag in tags)
        {
            string tag = GameplayTagUtility.Normalize(rawTag);
            if (string.IsNullOrEmpty(tag) || sourceTags.Contains(tag)) continue;

            sourceTags.Add(tag);
            int count;
            tagCounts.TryGetValue(tag, out count);
            tagCounts[tag] = count + 1;
            changed = true;
        }

        if (changed) OnChanged?.Invoke();
    }

    public void RemoveTags(object source)
    {
        if (source == null) return;

        List<string> sourceTags;
        if (!tagsBySource.TryGetValue(source, out sourceTags)) return;

        foreach (string tag in sourceTags)
        {
            int count;
            if (!tagCounts.TryGetValue(tag, out count)) continue;

            if (count <= 1) tagCounts.Remove(tag);
            else tagCounts[tag] = count - 1;
        }

        tagsBySource.Remove(source);
        OnChanged?.Invoke();
    }

    public void Clear()
    {
        if (tagCounts.Count == 0 && tagsBySource.Count == 0) return;
        tagCounts.Clear();
        tagsBySource.Clear();
        OnChanged?.Invoke();
    }
}
