using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class LiarPainterAutomatedPlaytestRunner : MonoBehaviour
{
    public const string EnabledKey = "LiarPainter.Playtest.Enabled";
    public const string PlayerCountKey = "LiarPainter.Playtest.PlayerCount";
    public const string FastModeKey = "LiarPainter.Playtest.FastMode";
    public const string AutoVoteKey = "LiarPainter.Playtest.AutoVote";
    public const string SeedKey = "LiarPainter.Playtest.Seed";

    private LiarPainterGameCoordinator coordinator;
    private bool autoVote;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void TryCreate()
    {
#if UNITY_EDITOR
        if (SceneManager.GetActiveScene().name != "GameScene" || PlayerPrefs.GetInt(EnabledKey, 0) != 1) return;
        PlayerPrefs.SetInt(EnabledKey, 0);
        new GameObject("AutomatedPlaytestRunner").AddComponent<LiarPainterAutomatedPlaytestRunner>();
#endif
    }

    private void Start()
    {
        coordinator = FindFirstObjectByType<LiarPainterGameCoordinator>();
        if (coordinator == null)
        {
            Debug.LogError("[Liar Painter Test] LiarPainterGameCoordinator를 찾지 못했습니다.");
            Destroy(gameObject);
            return;
        }

        int playerCount = Mathf.Clamp(PlayerPrefs.GetInt(PlayerCountKey, 4), 4, 8);
        bool fastMode = PlayerPrefs.GetInt(FastModeKey, 1) == 1;
        autoVote = PlayerPrefs.GetInt(AutoVoteKey, 1) == 1;
        int seed = PlayerPrefs.GetInt(SeedKey, 20261008);

        if (fastMode)
        {
            coordinator.ConfigurePlaytestTimings();
            FindFirstObjectByType<GameResultPopupController>()?.SetDisplaySeconds(3f);
        }
        coordinator.SetRoundContent("고양이", "호랑이", "이것이 어울리는 장소는?");
        coordinator.PhaseChanged += HandlePhaseChanged;

        List<LiarPainterPlayerInfo> players = new(playerCount);
        for (int i = 0; i < playerCount; i++)
            players.Add(new LiarPainterPlayerInfo($"test-player-{i + 1}", i == 0 ? "HOST" : $"BOT {i:00}"));

        PopulatePlayerSlots(players);
        bool started = coordinator.StartSingleRound(players, seed);
        if (started)
        {
            string assignments = string.Join(", ", coordinator.Players.Select(player =>
                $"{player.DisplayName}={player.Role}/{(string.IsNullOrWhiteSpace(player.PrivateKeyword) ? "키워드 없음" : player.PrivateKeyword)}"));
            Debug.Log($"[Liar Painter Test] 직업 배정: {assignments}");
        }
        Debug.Log(started
            ? $"[Liar Painter Test] {playerCount}명 자동 테스트를 시작했습니다."
            : "[Liar Painter Test] 자동 테스트를 시작하지 못했습니다.");
    }

    private void OnDestroy()
    {
        if (coordinator != null) coordinator.PhaseChanged -= HandlePhaseChanged;
    }

    private void HandlePhaseChanged(LiarPainterGamePhase phase)
    {
        if (!autoVote || phase != LiarPainterGamePhase.Voting) return;
        LiarPainterPlayerState fakePainter = coordinator.Players.FirstOrDefault(player => player.Role == LiarPainterRole.FakePainter);
        if (fakePainter == null) return;

        foreach (LiarPainterPlayerState voter in coordinator.Players)
        {
            string target = voter.PlayerId == fakePainter.PlayerId
                ? coordinator.Players.First(player => player.PlayerId != voter.PlayerId).PlayerId
                : fakePainter.PlayerId;
            coordinator.SubmitVote(voter.PlayerId, target);
        }
    }

    private static void PopulatePlayerSlots(IReadOnlyList<LiarPainterPlayerInfo> players)
    {
        PlayerSlotView[] slots = FindObjectsByType<PlayerSlotView>(FindObjectsSortMode.None)
            .OrderBy(slot => slot.transform.GetSiblingIndex())
            .ToArray();
        for (int i = 0; i < slots.Length; i++)
        {
            if (i < players.Count)
                slots[i].SetPlayer(i + 1, players[i].displayName, "자동 준비 완료", i == 0, i == 0, true);
            else
                slots[i].SetEmpty(i + 1);
        }
    }
}
