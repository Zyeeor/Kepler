using System;
using System.Collections.Generic;

/// <summary>
/// Lightweight hierarchical Gameplay Tag helpers.
/// Tags use dot-separated paths: "State.Control.Stunned" matches the parent query "State.Control".
/// </summary>
public static class GameplayTagUtility
{
    public static string Normalize(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return string.Empty;

        string value = tag.Trim().Trim('.');
        while (value.Contains("..")) value = value.Replace("..", ".");
        return value;
    }

    /// <summary>Returns true when an owned tag equals, or is a child of, the query tag.</summary>
    public static bool Matches(string ownedTag, string queryTag)
    {
        ownedTag = Normalize(ownedTag);
        queryTag = Normalize(queryTag);
        if (string.IsNullOrEmpty(ownedTag) || string.IsNullOrEmpty(queryTag)) return false;

        return string.Equals(ownedTag, queryTag, StringComparison.OrdinalIgnoreCase)
            || ownedTag.StartsWith(queryTag + ".", StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasAny(IEnumerable<string> ownedTags, IEnumerable<string> queryTags)
    {
        if (ownedTags == null || queryTags == null) return false;
        foreach (string query in queryTags)
        {
            foreach (string owned in ownedTags)
            {
                if (Matches(owned, query)) return true;
            }
        }
        return false;
    }

    public static bool HasAll(IEnumerable<string> ownedTags, IEnumerable<string> queryTags)
    {
        if (queryTags == null) return true;
        foreach (string query in queryTags)
        {
            if (string.IsNullOrWhiteSpace(query)) continue;

            bool found = false;
            foreach (string owned in ownedTags)
            {
                if (Matches(owned, query))
                {
                    found = true;
                    break;
                }
            }
            if (!found) return false;
        }
        return true;
    }
}
