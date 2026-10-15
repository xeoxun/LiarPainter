using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

[InitializeOnLoad]
public static class LiarPainterMultiplayerPlayModeInstaller
{
    private const string PackageId = "com.unity.multiplayer.playmode";
    private const string AttemptKey = "LiarPainter.MppmInstallAttempted";
    private static AddRequest request;

    static LiarPainterMultiplayerPlayModeInstaller()
    {
        if (AssetDatabase.IsValidFolder("Packages/" + PackageId))
        {
            SessionState.EraseBool(AttemptKey);
            return;
        }

        if (SessionState.GetBool(AttemptKey, false)) return;
        SessionState.SetBool(AttemptKey, true);
        EditorApplication.delayCall += Install;
    }

    private static void Install()
    {
        Debug.Log("[Liar Painter] Multiplayer Play Mode 호환 버전을 설치합니다.");
        request = Client.Add(PackageId);
        EditorApplication.update += Poll;
    }

    private static void Poll()
    {
        if (request == null || !request.IsCompleted) return;
        EditorApplication.update -= Poll;

        if (request.Status == StatusCode.Success)
            Debug.Log($"[Liar Painter] Multiplayer Play Mode 설치 완료: {request.Result.name}@{request.Result.version}");
        else
        {
            SessionState.EraseBool(AttemptKey);
            Debug.LogError($"[Liar Painter] Multiplayer Play Mode 설치 실패: {request.Error?.message}");
        }

        request = null;
    }
}
