using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Named combat SFX router, parallel to <see cref="CombatEffectManager"/>.
/// Abilities reference clips by string name (cast / hit settle); this manager owns playback.
/// </summary>
public class CombatAudioManager : MonoBehaviour
{
    public static CombatAudioManager Instance { get; private set; }

    [Serializable]
    public class NamedClip
    {
        [Tooltip("Stable id referenced by Ability castAudioName / hitAudioName.")]
        public string name;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0.5f, 1.5f)] public float pitch = 1f;
    }

    [Header("Library")]
    [SerializeField] private NamedClip[] clips = Array.Empty<NamedClip>();

    [Header("Playback")]
    [SerializeField] private AudioSource dedicatedSource;
    [SerializeField] private float spatialBlend3D = 0.85f;
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 28f;

    private readonly Dictionary<string, NamedClip> _lookup =
        new Dictionary<string, NamedClip>(StringComparer.OrdinalIgnoreCase);

    void Awake()
    {
        Instance = this;
        RebuildLookup();
        if (dedicatedSource == null)
        {
            dedicatedSource = gameObject.GetComponent<AudioSource>();
            if (dedicatedSource == null)
                dedicatedSource = gameObject.AddComponent<AudioSource>();
        }

        dedicatedSource.playOnAwake = false;
        dedicatedSource.spatialBlend = 0f;
        dedicatedSource.loop = false;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void OnValidate()
    {
        RebuildLookup();
    }

    private void RebuildLookup()
    {
        _lookup.Clear();
        if (clips == null) return;
        for (int i = 0; i < clips.Length; i++)
        {
            NamedClip entry = clips[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.name) || entry.clip == null)
                continue;
            _lookup[entry.name.Trim()] = entry;
        }
    }

    /// <summary>Play a named clip. Safe when Instance is missing or the name is empty.</summary>
    public static void Play(string clipName, Vector3? worldPosition = null)
    {
        if (string.IsNullOrWhiteSpace(clipName))
            return;
        if (Instance == null)
            return;
        Instance.PlayInternal(clipName, worldPosition);
    }

    public void PlayInternal(string clipName, Vector3? worldPosition)
    {
        if (!_lookup.TryGetValue(clipName.Trim(), out NamedClip entry) || entry.clip == null)
        {
            Debug.LogWarning($"[CombatAudioManager] Clip '{clipName}' not found.", this);
            return;
        }

        float volume = Mathf.Clamp01(entry.volume);
        float pitch = Mathf.Clamp(entry.pitch, 0.5f, 1.5f);

        if (worldPosition.HasValue)
        {
            GameObject go = new GameObject($"SFX_{entry.name}");
            go.transform.position = worldPosition.Value;
            AudioSource src = go.AddComponent<AudioSource>();
            src.clip = entry.clip;
            src.volume = volume;
            src.pitch = pitch;
            src.spatialBlend = spatialBlend3D;
            src.minDistance = minDistance;
            src.maxDistance = maxDistance;
            src.Play();
            Destroy(go, entry.clip.length / Mathf.Max(0.01f, pitch) + 0.1f);
            return;
        }

        if (dedicatedSource == null)
            return;

        dedicatedSource.pitch = pitch;
        dedicatedSource.PlayOneShot(entry.clip, volume);
    }
}
