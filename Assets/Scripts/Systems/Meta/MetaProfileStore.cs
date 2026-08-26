using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 长期 Profile 存档（与 Run 存档 possess_run_save.json 物理分离）。
/// 聚合各局外系统数据段（当前：cardArchive）。Run 结束 / 新游戏（SaveCoordinator.DeleteSave）均不触碰本文件。
/// 版本不符时仅丢弃不可恢复的段、保留可恢复段（前向兼容），不整体清库。
/// </summary>
public static class MetaProfileStore
{
    public const int SchemaVersion = 1;
    static readonly string SavePath = Path.Combine(Application.persistentDataPath, "possess_meta_save.json");

    [Serializable]
    public class Container
    {
        public int schemaVersion = SchemaVersion;
        public List<CardArchiveEntry> cardArchive = new List<CardArchiveEntry>();
        public int validCardTotal = 0;   // 当前有效卡总数（Run 内刷新并固化，供主菜单无场景读取）
        // 未来段（T2 可将荣誉殿堂 hallOfFame 接入此处，统一版本管理）
    }

    static Container _cache;

    static Container Data
    {
        get { if (_cache == null) _cache = Load(); return _cache; }
    }

    /// <summary>读盘：损坏 / 版本不符 → 保留可恢复段，不整体清库。</summary>
    public static Container Load()
    {
        if (_cache != null) return _cache;
        try
        {
            if (File.Exists(SavePath))
            {
                var c = JsonUtility.FromJson<Container>(File.ReadAllText(SavePath));
                if (c != null && c.schemaVersion == SchemaVersion)
                {
                    _cache = c;
                    return c;
                }
                _cache = Recover(c);   // 版本不符：保留同结构段
                return _cache;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[MetaProfileStore] 读取失败：{e.Message}");
        }
        _cache = new Container();
        return _cache;
    }

    /// <summary>版本不符时保留可恢复的数据段（当前 cardArchive 段同结构直接保留）。</summary>
    static Container Recover(Container old)
    {
        var c = new Container();
        if (old != null && old.cardArchive != null) c.cardArchive = old.cardArchive;
        return c;
    }

    public static void Save()
    {
        try
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(Data, true));
        }
        catch (Exception e)
        {
            Debug.LogError($"[MetaProfileStore] 写入失败：{e.Message}");
        }
    }

    /// <summary>cardArchive 段访问（由 CardArchiveStore 调用）。</summary>
    public static List<CardArchiveEntry> CardArchive => Data.cardArchive;

    /// <summary>当前有效卡总数（进度分母），Run 内刷新并持久化，主菜单无场景时读取。</summary>
    public static int ValidCardTotal
    {
        get => Data.validCardTotal;
        set { Data.validCardTotal = value; Save(); }
    }
}
