using System;
using UnityEngine;

/// <summary>
/// 设备特征码（策划案《精英怪筛选-他人BD怪物投放》§2 拍板：同设备 = 一个玩家）。
/// 首次访问生成 "device-"+GUID 并持久化到 PlayerPrefs；作为精英快照的 sourcePlayerId / pick 的 playerId。
/// 不做账号体系，玩家隔离按"设备级防自见"理解。
/// </summary>
public static class DeviceIdentity
{
    const string PrefsKey = "EliteDeviceId";
    static string cached;

    /// <summary>本设备唯一 ID（懒生成 + PlayerPrefs 持久化）。</summary>
    public static string Id
    {
        get
        {
            if (!string.IsNullOrEmpty(cached)) return cached;
            cached = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (string.IsNullOrEmpty(cached))
            {
                cached = "device-" + Guid.NewGuid().ToString("N").Substring(0, 16);
                PlayerPrefs.SetString(PrefsKey, cached);
                PlayerPrefs.Save();
                Debug.Log($"[DeviceIdentity] 生成设备特征码：{cached}");
            }
            return cached;
        }
    }
}
