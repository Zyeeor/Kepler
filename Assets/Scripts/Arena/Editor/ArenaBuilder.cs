using UnityEngine;
using UnityEditor;

public class ArenaBuilder
{
    [MenuItem("Tools/Arena/Build Combat Arena")]
    public static void BuildCombatArena()
    {
        var mainCam = Camera.main;
        if (mainCam == null) { Debug.LogError("[ArenaBuilder] No Main Camera found."); return; }

        // 1. 移除旧的 CameraFollow（帧率相关 Lerp、跟随 Y、无边界）
        CameraFollow oldFollow = mainCam.GetComponent<CameraFollow>();
        if (oldFollow != null) Undo.DestroyObjectImmediate(oldFollow);

        // 2. 移除旧的 CameraTarget
        var oldTarget = GameObject.Find("CameraTarget");
        if (oldTarget != null) Undo.DestroyObjectImmediate(oldTarget);

        // 3. 创建 / 查找 CameraTarget（独立对象，不作为 Player 或 Camera 的子物体）
        GameObject targetGO = GameObject.Find("CameraTarget");
        if (targetGO == null)
        {
            targetGO = new GameObject("CameraTarget");
            Undo.RegisterCreatedObjectUndo(targetGO, "Create CameraTarget");
        }
        CameraTarget camTarget = targetGO.GetComponent<CameraTarget>();
        if (camTarget == null) camTarget = Undo.AddComponent<CameraTarget>(targetGO);

        // 4. 加 / 配置 BattleCameraRig
        BattleCameraRig rig = mainCam.GetComponent<BattleCameraRig>();
        if (rig == null) rig = Undo.AddComponent<BattleCameraRig>(mainCam.gameObject);
        Undo.RecordObject(rig, "Configure BattleCameraRig");

        rig.projection = BattleCameraRig.ProjectionMode.Orthographic;
        rig.orthographicSize = 8f;
        rig.offset = new Vector3(0, 8, -6);
        rig.smoothSpeed = 6f;
        rig.lookSmoothSpeed = 8f;
        rig.target = targetGO.transform;

        Debug.Log("[ArenaBuilder] Combat arena camera set up.");
    }
}
