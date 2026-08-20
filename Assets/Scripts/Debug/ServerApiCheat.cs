using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Play-mode debug panel for client-server integration testing (Go server under Server/).
/// - F9: full UGC flow (upload -> list -> search -> download -> subscribe -> rate).
/// - F10: elite snapshot flow (rolling upload with upsert overwrite -> pick for multiple players).
/// Results are printed to Console and to an OnGUI panel; raw responses are logged when
/// logRawResponses is enabled. Requires the server running first: cd Server; go run .
/// All request/response DTOs mirror Server/internal (camelCase wire format).
/// </summary>
public class ServerApiCheat : MonoBehaviour
{
    [Header("Server")]
    [Tooltip("Base URL of the content server (Server/README.md for launch options).")]
    public string serverUrl = "http://127.0.0.1:8080";
    [Tooltip("Per-request timeout in seconds. Keep short so a dead server fails fast.")]
    public int timeoutSeconds = 5;
    [Tooltip("Log raw JSON response bodies (truncated) to Console for inspection.")]
    public bool logRawResponses = true;

    [Header("Input")]
    public bool enableCheats = true;
    public bool showOnScreenHint = true;
    [Tooltip("Hotkey for the UGC flow. Change to a plain letter (e.g. U) if F-keys are hard to reach on your keyboard/remote session.")]
    public KeyCode ugcFlowKey = KeyCode.F9;
    [Tooltip("Hotkey for the elite flow.")]
    public KeyCode eliteFlowKey = KeyCode.F10;

    private const int MaxLogLines = 8;
    private const int MaxRawLength = 300;

    private readonly Queue<string> recentLogs = new Queue<string>();
    private string lastStatus = "ServerApiCheat ready. Start the server first (Server: go run .).";
    private bool running;

    // 测试用固定身份：UGC 侧匿名 ID / 精英怪侧设备特征码（api-guide 基础约定）。
    private const string UgcPlayerId = "unity-debug-tester";
    private const string ElitePlayerA = "device-unity-debug-a";
    private const string ElitePlayerB = "device-unity-debug-b";
    private const string ElitePlayerFresh = "device-unity-debug-fresh";

    void Update()
    {
        if (!enableCheats || GameManager.IsFormalFlow) return; // 正式流程屏蔽联调入口

        if (Input.GetKeyDown(ugcFlowKey)) RunUgcFlow();
        if (Input.GetKeyDown(eliteFlowKey)) RunEliteFlow();
    }

    void OnGUI()
    {
        if (!enableCheats || !showOnScreenHint || GameManager.IsFormalFlow) return;

        const float width = 640f;
        float height = 124f + recentLogs.Count * 16f;
        GUI.Box(new Rect(10f, 10f, width, height), "Server API Cheat");
        GUI.Label(new Rect(18f, 32f, width - 24f, 20f), $"{ugcFlowKey} UGC flow | {eliteFlowKey} elite flow | server: {serverUrl}");
        if (GUI.Button(new Rect(18f, 52f, 155f, 22f), "Run UGC flow")) RunUgcFlow();
        if (GUI.Button(new Rect(180f, 52f, 155f, 22f), "Run Elite flow")) RunEliteFlow();
        float y = 80f;
        foreach (string line in recentLogs)
        {
            GUI.Label(new Rect(18f, y, width - 24f, 16f), line);
            y += 16f;
        }
        GUI.Label(new Rect(18f, y + 4f, width - 24f, 20f), lastStatus);
    }

    // ========================================================================
    // F9：UGC 全链路
    // ========================================================================

    private async void RunUgcFlow()
    {
        if (!BeginFlow("UGC flow (F9)")) return;
        try
        {
            // 1/6 上传一张测试地图（fileData = base64(JSON)）。
            var upload = new UploadCreationReq
            {
                creatorId = UgcPlayerId,
                creatorName = "UnityDebug",
                type = "map",
                name = $"联调测试地图 {DateTime.Now:HH:mm:ss}",
                description = "auto upload by ServerApiCheat",
                tags = new[] { "roguelike", "debug" },
                fileName = "map.json",
                fileData = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"version\":\"1.0\",\"source\":\"ServerApiCheat\"}")),
            };
            var uploadResp = await PostJson<UploadCreationResp>("/api/creations", upload);
            string creationId = uploadResp.creationId;
            Log($"1/6 upload ok: creationId={creationId}");

            // 2/6 列表（热门排序，截前 5）。
            var list = await GetJson<ListResp>("/api/creations?type=map&page=1&pageSize=5&sortBy=downloads&descending=true");
            string first = list.creations != null && list.creations.Count > 0
                ? $"'{list.creations[0].name}' (downloads={list.creations[0].downloads})"
                : "(empty)";
            Log($"2/6 list ok: total={list.total}, first={first}");

            // 3/6 搜索（中文关键词，验证 URL 编码）。
            var search = await GetJson<ListResp>("/api/creations/search?keyword=" + UnityWebRequest.EscapeURL("联调"));
            Log($"3/6 search ok: hits={search.total}");

            // 4/6 下载并解码（下载数 +1）。
            var download = await GetJson<DownloadResp>($"/api/creations/{creationId}/download");
            string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(download.fileData));
            Log($"4/6 download ok: '{download.name}' v{download.version} file='{decoded}'");

            // 5/6 订阅。
            var subscribe = await PostJson<OkResp>($"/api/creations/{creationId}/subscribe",
                new SubscribeReq { playerId = UgcPlayerId, subscribe = true });
            Log($"5/6 subscribe ok: {subscribe.ok}");

            // 6/6 评分 5 星。
            var rate = await PostJson<OkResp>($"/api/creations/{creationId}/rate",
                new RateReq { playerId = UgcPlayerId, rating = 5, comment = "auto rated by ServerApiCheat" });
            Log($"6/6 rate ok: {rate.ok}");

            SetStatus("UGC flow finished OK.");
        }
        catch (Exception ex)
        {
            FailFlow(ex);
        }
        finally
        {
            running = false;
        }
    }

    // ========================================================================
    // F10：精英怪 BD 快照链路
    // ========================================================================

    private async void RunEliteFlow()
    {
        if (!BeginFlow("Elite flow (F10)")) return;
        try
        {
            string runId = "run-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");

            // 1/5 A 上传 wave3 浅构筑，再上传 wave6 深构筑 —— 验证滚动 upsert 覆盖（库中保留最深版本）。
            var upShallow = await PostJson<UploadSnapshotsResp>("/api/bd-snapshots",
                new UploadSnapshotsReq
                {
                    playerId = ElitePlayerA,
                    runId = runId,
                    snapshots = new List<SnapshotEntry> { Snap("lust", "色欲-灵念师", "LU", 1, 3, 120) },
                });
            var upDeep = await PostJson<UploadSnapshotsResp>("/api/bd-snapshots",
                new UploadSnapshotsReq
                {
                    playerId = ElitePlayerA,
                    runId = runId,
                    snapshots = new List<SnapshotEntry> { Snap("lust", "色欲-灵念师", "LU", 3, 6, 420) },
                });
            Log($"1/5 A upload ok: accepted={upShallow.accepted}->{upDeep.accepted} (wave3->6 upsert, bdCount 1->3)");

            // 2/5 B 上传 envy 快照（sourceWave=8，供 A 在低波次命中）。
            var upB = await PostJson<UploadSnapshotsResp>("/api/bd-snapshots",
                new UploadSnapshotsReq
                {
                    playerId = ElitePlayerB,
                    runId = runId,
                    snapshots = new List<SnapshotEntry> { Snap("envy", "嫉妒-雷暴术士", "EN", 2, 8, 600) },
                });
            Log($"2/5 B upload ok: accepted={upB.accepted}");

            // 3/5 A 在第 5 波请求投放：排除自己 -> 预期命中 B 的 envy（sourceWave 8 >= 5+1 主路径）。
            var pickA = await PostJson<ElitePickResp>("/api/elite/pick",
                new PickReq { playerId = ElitePlayerA, wave = 5 });
            LogPick("3/5 A pick wave=5", pickA);

            // 4/5 B 在第 5 波请求投放：预期命中 A 的 lust bdCount=3（upsert 后的最深版本）。
            var pickB = await PostJson<ElitePickResp>("/api/elite/pick",
                new PickReq { playerId = ElitePlayerB, wave = 5 });
            LogPick("4/5 B pick wave=5", pickB);

            // 5/5 全新玩家在超大波次请求：观察兜底路径（relaxed / snapshot=null 均为正常分支）。
            var pickFresh = await PostJson<ElitePickResp>("/api/elite/pick",
                new PickReq { playerId = ElitePlayerFresh, wave = 999 });
            LogPick("5/5 fresh pick wave=999", pickFresh);

            SetStatus("Elite flow finished OK.");
        }
        catch (Exception ex)
        {
            FailFlow(ex);
        }
        finally
        {
            running = false;
        }
    }

    /// <summary>构造一条测试快照（bdData 结构为前台约定的卡 ID + 层数，服务器只透传）。</summary>
    private static SnapshotEntry Snap(string sin, string monsterType, string cardPrefix, int bdCount, int wave, long gameTime)
    {
        return new SnapshotEntry
        {
            sin = sin,
            monsterType = monsterType,
            bdCount = bdCount,
            sourceWave = wave,
            gameTime = gameTime,
            bdData = new List<BdCardEntry>
            {
                new BdCardEntry { cardId = cardPrefix + "-S01", stack = 1 },
                new BdCardEntry { cardId = cardPrefix + "-M02", stack = bdCount },
            },
            stats = new SnapshotStats { killedCount = wave * 2 },
        };
    }

    private void LogPick(string label, ElitePickResp resp)
    {
        // JsonUtility 无法区分 "snapshot":null 与默认实例，双重判断（原始 null 标记 + 空字段）。
        bool isNull = resp.snapshot == null
            || (resp.snapshot.snapshotId == 0 && string.IsNullOrEmpty(resp.snapshot.sin));
        if (isNull)
        {
            Log($"{label} -> snapshot=null (no elite this wave) relaxed={resp.relaxed}");
            return;
        }

        EliteSnapshotItem s = resp.snapshot;
        string cards = s.bdData != null && s.bdData.Count > 0
            ? DescribeCards(s.bdData)
            : "(empty)";
        Log($"{label} -> sin={s.sin} monster='{s.monsterType}' bdCount={s.bdCount} " +
            $"sourceWave={s.sourceWave} by={s.sourcePlayerId} relaxed={resp.relaxed} cards=[{cards}]");
    }

    private static string DescribeCards(List<BdCardEntry> cards)
    {
        var parts = new List<string>(cards.Count);
        for (int i = 0; i < cards.Count; i++)
            parts.Add(cards[i].cardId + "x" + cards[i].stack);
        return string.Join(", ", parts.ToArray());
    }

    // ========================================================================
    // HTTP 基础设施
    // ========================================================================

    // Unity 2022.3 未内置 UnityWebRequestAsyncOperation.GetAwaiter（2023.1+ 才提供），
    // 用 TaskCompletionSource 桥接 completed 回调以支持 await；回调经 UnitySynchronizationContext 派发回主线程。
    private static Task SendAsync(UnityWebRequest req)
    {
        UnityWebRequestAsyncOperation op = req.SendWebRequest();
        var tcs = new TaskCompletionSource<bool>();
        op.completed += _ => tcs.SetResult(true);
        return tcs.Task;
    }

    private async Task<T> GetJson<T>(string pathAndQuery)
    {
        string label = "GET " + pathAndQuery;
        using (var req = UnityWebRequest.Get(serverUrl + pathAndQuery))
        {
            req.timeout = timeoutSeconds;
            await SendAsync(req);
            return CheckAndParse<T>(req, label);
        }
    }

    private async Task<T> PostJson<T>(string path, object body)
    {
        string label = "POST " + path;
        string json = JsonUtility.ToJson(body);
        using (var req = new UnityWebRequest(serverUrl + path, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = timeoutSeconds;
            await SendAsync(req);
            return CheckAndParse<T>(req, label);
        }
    }

    private T CheckAndParse<T>(UnityWebRequest req, string label)
    {
        string text = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
        if (req.result != UnityWebRequest.Result.Success)
        {
            // 网络失败（服务器未启动）或协议失败（4xx/5xx，body 为 {"code":..,"msg":".."}）。
            throw new Exception($"{label} failed: {req.error} (HTTP {req.responseCode}) body: {text}");
        }

        if (logRawResponses)
            Debug.Log($"[ServerApiCheat] <- {label}: {Truncate(text, MaxRawLength)}");
        return JsonUtility.FromJson<T>(text);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value;
        return value.Substring(0, maxLength) + $"...({value.Length} chars)";
    }

    // ========================================================================
    // 面板状态
    // ========================================================================

    private bool BeginFlow(string name)
    {
        if (running)
        {
            SetStatus("Another request flow is already running.");
            return false;
        }
        running = true;
        Log($"=== {name} ===");
        SetStatus($"{name} started...");
        return true;
    }

    private void FailFlow(Exception ex)
    {
        SetStatus($"FAILED: {ex.Message}");
        Debug.LogException(ex);
    }

    private void Log(string message)
    {
        Debug.Log("[ServerApiCheat] " + message);
        recentLogs.Enqueue(message);
        while (recentLogs.Count > MaxLogLines)
            recentLogs.Dequeue();
    }

    private void SetStatus(string message)
    {
        lastStatus = message;
        Debug.Log("[ServerApiCheat] " + message);
    }

    // ========================================================================
    // Wire DTO（与 Server/internal 的 JSON 标签一一对应；JsonUtility 字段即键名）
    // ========================================================================

    // --- UGC：上传 ---
    [Serializable]
    private class UploadCreationReq
    {
        public string creatorId;
        public string creatorName;
        public string type;
        public string name;
        public string description;
        public string[] tags;
        public string fileName;
        public string fileData; // base64
    }

    [Serializable]
    private class UploadCreationResp
    {
        public string creationId;
        public string fileUrl;
    }

    // --- UGC：列表 / 搜索 ---
    [Serializable]
    private class CreationItem
    {
        public string creationId;
        public string creatorName;
        public string type;
        public string name;
        public int downloads;
        public float rating;
    }

    [Serializable]
    private class ListResp
    {
        public List<CreationItem> creations;
        public int total;
    }

    // --- UGC：下载 ---
    [Serializable]
    private class DownloadResp
    {
        public string creationId;
        public string type;
        public string name;
        public string fileData; // base64
        public int version;
    }

    // --- UGC：订阅 / 评分 ---
    [Serializable]
    private class SubscribeReq
    {
        public string playerId;
        public bool subscribe;
    }

    [Serializable]
    private class RateReq
    {
        public string playerId;
        public int rating;
        public string comment;
    }

    [Serializable]
    private class OkResp
    {
        public bool ok;
    }

    // --- 精英怪：批量上传 ---
    [Serializable]
    private class UploadSnapshotsReq
    {
        public string playerId;
        public string runId;
        public List<SnapshotEntry> snapshots;
    }

    [Serializable]
    private class SnapshotEntry
    {
        public string sin;
        public string monsterType;
        public int bdCount;
        public List<BdCardEntry> bdData;
        public int sourceWave;
        public long gameTime;
        public SnapshotStats stats;
    }

    [Serializable]
    private class BdCardEntry
    {
        public string cardId;
        public int stack;
    }

    [Serializable]
    private class SnapshotStats
    {
        public int killedCount;
    }

    [Serializable]
    private class UploadSnapshotsResp
    {
        public bool ok;
        public int accepted;
    }

    // --- 精英怪：请求投放 ---
    [Serializable]
    private class PickReq
    {
        public string playerId;
        public int wave;
    }

    [Serializable]
    private class ElitePickResp
    {
        public EliteSnapshotItem snapshot; // JsonUtility 无法表达 null，用 LogPick 双重判断
        public bool relaxed;
    }

    [Serializable]
    private class EliteSnapshotItem
    {
        public long snapshotId;
        public string sourcePlayerId;
        public string runId;
        public string sin;
        public string monsterType;
        public List<BdCardEntry> bdData;
        public int bdCount;
        public int sourceWave;
        public long gameTime;
    }
}
