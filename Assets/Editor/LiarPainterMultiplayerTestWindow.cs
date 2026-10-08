using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class LiarPainterMultiplayerTestWindow : EditorWindow
{
    private const string GameScenePath = "Assets/Scenes/GameScene.unity";
    private int playerCount = 4;
    private bool fastMode = true;
    private bool autoVote = true;
    private int randomSeed = 20261008;

    [MenuItem("Liar Painter/Multiplayer Test")]
    private static void Open()
    {
        GetWindow<LiarPainterMultiplayerTestWindow>("Liar Painter Test");
    }

    private void OnEnable()
    {
        playerCount = EditorPrefs.GetInt("LiarPainter.TestWindow.PlayerCount", 4);
        fastMode = EditorPrefs.GetBool("LiarPainter.TestWindow.FastMode", true);
        autoVote = EditorPrefs.GetBool("LiarPainter.TestWindow.AutoVote", true);
        randomSeed = EditorPrefs.GetInt("LiarPainter.TestWindow.Seed", 20261008);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("자동 플레이 테스트", EditorStyles.boldLabel);
        EditorGUILayout.Space(4f);
        playerCount = EditorGUILayout.IntSlider("전체 플레이어", playerCount, 4, 8);
        fastMode = EditorGUILayout.Toggle("빠른 진행", fastMode);
        autoVote = EditorGUILayout.Toggle("사짜 화가 자동 투표", autoVote);
        randomSeed = EditorGUILayout.IntField("랜덤 시드", randomSeed);

        int virtualPlayers = Mathf.Min(4, playerCount);
        int bots = Mathf.Max(0, playerCount - virtualPlayers);
        EditorGUILayout.HelpBox(
            $"규칙 자동 테스트: {playerCount}명 모두 자동 플레이\n" +
            $"실제 네트워크 권장 구성: 가상 플레이어 {virtualPlayers}명 + 보충 봇 {bots}명",
            MessageType.Info);

        if (GUILayout.Button($"{playerCount}명 규칙 자동 테스트 시작", GUILayout.Height(36f)))
            StartAutomatedRuleTest();

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("실제 네트워크 테스트", EditorStyles.boldLabel);
        bool packageInstalled = AssetDatabase.IsValidFolder("Packages/com.unity.multiplayer.playmode");
        EditorGUILayout.HelpBox(packageInstalled
            ? "Multiplayer Play Mode가 설치되어 있습니다. 최대 4개의 실제 에디터 플레이어를 동시에 실행할 수 있습니다."
            : "Multiplayer Play Mode 설치가 진행 중입니다. Package Manager 해석이 끝난 뒤 사용할 수 있습니다.",
            packageInstalled ? MessageType.Info : MessageType.Warning);

        using (new EditorGUI.DisabledScope(!packageInstalled))
        {
            if (GUILayout.Button("Play Mode Scenarios 창 열기", GUILayout.Height(30f)))
            {
                if (!EditorApplication.ExecuteMenuItem("Window/Play Mode/Scenarios"))
                    Debug.LogWarning("Window > Play Mode > Scenarios 메뉴를 직접 열어주세요.");
            }
        }
    }

    private void StartAutomatedRuleTest()
    {
        EditorPrefs.SetInt("LiarPainter.TestWindow.PlayerCount", playerCount);
        EditorPrefs.SetBool("LiarPainter.TestWindow.FastMode", fastMode);
        EditorPrefs.SetBool("LiarPainter.TestWindow.AutoVote", autoVote);
        EditorPrefs.SetInt("LiarPainter.TestWindow.Seed", randomSeed);

        PlayerPrefs.SetInt(LiarPainterAutomatedPlaytestRunner.EnabledKey, 1);
        PlayerPrefs.SetInt(LiarPainterAutomatedPlaytestRunner.PlayerCountKey, playerCount);
        PlayerPrefs.SetInt(LiarPainterAutomatedPlaytestRunner.FastModeKey, fastMode ? 1 : 0);
        PlayerPrefs.SetInt(LiarPainterAutomatedPlaytestRunner.AutoVoteKey, autoVote ? 1 : 0);
        PlayerPrefs.SetInt(LiarPainterAutomatedPlaytestRunner.SeedKey, randomSeed);
        PlayerPrefs.Save();

        if (SceneManager.GetActiveScene().path != GameScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(GameScenePath);
        }

        EditorApplication.isPlaying = true;
    }
}
