using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public sealed class LiarPainterGameCoordinator : MonoBehaviour
{
    [Header("게임 규칙")]
    [SerializeField] private LiarPainterRuleSettings rules = new();

    [Header("이번 게임 콘텐츠")]
    [SerializeField] private LiarPainterRoundContent roundContent = new();
    [SerializeField] private LiarPainterContentCatalog contentCatalog;
    [SerializeField] private bool useContentCatalog = true;

    [Header("결과 UI")]
    [SerializeField] private GameResultPopupController resultPopup;

    private readonly List<LiarPainterPlayerState> players = new();
    private readonly List<string> drawingOrder = new();
    private readonly VoteService voteService = new();
    private Coroutine gameRoutine;
    private System.Random random;

    public LiarPainterGamePhase CurrentPhase { get; private set; } = LiarPainterGamePhase.Lobby;
    public string CurrentDrawingPlayerId { get; private set; }
    public IReadOnlyList<LiarPainterPlayerState> Players => players;
    public LiarPainterRoundContent RoundContent => roundContent;

    public event Action<LiarPainterGamePhase> PhaseChanged;
    public event Action<float> PhaseTimeChanged;
    public event Action<LiarPainterPrivateAssignment> PrivateAssignmentCreated;
    public event Action<string> QuestionRevealed;
    public event Action<int, int> AnonymousDrawingTurnStarted;
    public event Action<string, bool> DrawingPermissionChanged;
    public event Action<LiarPainterGameOutcome> GameFinished;

    private void Awake()
    {
        if (resultPopup == null) resultPopup = GetComponent<GameResultPopupController>();
    }

    public void SetRoundContent(string citizenKeyword, string drunkPainterKeyword, string question)
    {
        useContentCatalog = false;
        roundContent.citizenKeyword = citizenKeyword ?? string.Empty;
        roundContent.drunkPainterKeyword = drunkPainterKeyword ?? string.Empty;
        roundContent.question = question ?? string.Empty;
    }

    public void ConfigurePlaytestTimings()
    {
        rules.roleRevealSeconds = 1f;
        rules.questionRevealSeconds = 1f;
        rules.drawingSecondsPerPlayer = 0.5f;
        rules.discussionSeconds = 1f;
        rules.votingSeconds = 1f;
        rules.resultSeconds = 3f;
    }

    public bool StartSingleRound(IReadOnlyList<LiarPainterPlayerInfo> playerInfos, int? randomSeed = null)
    {
        if (gameRoutine != null || CurrentPhase != LiarPainterGamePhase.Lobby) return false;
        random = randomSeed.HasValue ? new System.Random(randomSeed.Value) : new System.Random();
        if (useContentCatalog && !TryGenerateRoundContent(random)) return false;
        if (!ValidateStart(playerInfos, out string error))
        {
            Debug.LogWarning(error);
            return false;
        }

        CreatePlayerStates(playerInfos);
        gameRoutine = StartCoroutine(RunSingleRound());
        return true;
    }

    private bool TryGenerateRoundContent(System.Random sourceRandom)
    {
        if (contentCatalog == null) contentCatalog = LiarPainterContentProvider.LoadDefault();
        if (contentCatalog != null && contentCatalog.TryCreateRound(sourceRandom, out LiarPainterRoundContent generated))
        {
            roundContent = generated;
            return true;
        }

        Debug.LogWarning("LiarPainterContentCatalog에 질문과 같은 카테고리의 키워드가 2개 이상 필요합니다.");
        return false;
    }

    public bool SubmitVote(string voterPlayerId, string targetPlayerId)
    {
        if (CurrentPhase != LiarPainterGamePhase.Voting) return false;
        string[] validIds = players.Select(player => player.PlayerId).ToArray();
        return voteService.SubmitVote(voterPlayerId, targetPlayerId, validIds);
    }

    public bool TryAppraise(string appraiserPlayerId, DrawingStrokeOwner selectedStroke, out string ownerPlayerId)
    {
        ownerPlayerId = null;
        if (CurrentPhase != LiarPainterGamePhase.Discussion || selectedStroke == null) return false;

        LiarPainterPlayerState appraiser = players.FirstOrDefault(player => player.PlayerId == appraiserPlayerId);
        if (appraiser == null) return false;

        RoleAbilityResult result = appraiser.RoleAbility.TryUse(new RoleAbilityContext(
            CurrentPhase,
            selectedStroke.StrokeId,
            selectedStroke.OwnerPlayerId));
        ownerPlayerId = result.RevealedPlayerId;
        return result.Succeeded;
    }

    public LiarPainterPrivateAssignment GetPrivateAssignment(string playerId)
    {
        LiarPainterPlayerState player = players.FirstOrDefault(value => value.PlayerId == playerId);
        if (player == null) throw new ArgumentException("플레이어를 찾을 수 없습니다.", nameof(playerId));

        return new LiarPainterPrivateAssignment(
            player.PlayerId,
            KeywordAssignmentService.GetVisibleRole(player.Role),
            player.PrivateKeyword);
    }

    private IEnumerator RunSingleRound()
    {
        SetPhase(LiarPainterGamePhase.RoleReveal);
        foreach (LiarPainterPlayerState player in players)
            PrivateAssignmentCreated?.Invoke(GetPrivateAssignment(player.PlayerId));
        yield return RunTimer(rules.roleRevealSeconds);

        SetPhase(LiarPainterGamePhase.QuestionReveal);
        QuestionRevealed?.Invoke(roundContent.question);
        yield return RunTimer(rules.questionRevealSeconds);

        SetPhase(LiarPainterGamePhase.Drawing);
        for (int i = 0; i < drawingOrder.Count; i++)
        {
            CurrentDrawingPlayerId = drawingOrder[i];
            AnonymousDrawingTurnStarted?.Invoke(i + 1, drawingOrder.Count);
            DrawingPermissionChanged?.Invoke(CurrentDrawingPlayerId, true);
            yield return RunTimer(rules.drawingSecondsPerPlayer);
            DrawingPermissionChanged?.Invoke(CurrentDrawingPlayerId, false);
        }
        CurrentDrawingPlayerId = null;

        SetPhase(LiarPainterGamePhase.Discussion);
        yield return RunTimer(rules.discussionSeconds);

        voteService.Clear();
        SetPhase(LiarPainterGamePhase.Voting);
        yield return RunTimer(rules.votingSeconds);

        VoteResult voteResult = voteService.CalculateResult();
        LiarPainterGameOutcome outcome = WinConditionService.Evaluate(
            voteResult,
            players,
            rules.fakePainterWinsOnVoteTie);

        SetPhase(LiarPainterGamePhase.Result);
        GameFinished?.Invoke(outcome);
        ShowResult(outcome);
        gameRoutine = null;
    }

    private void CreatePlayerStates(IReadOnlyList<LiarPainterPlayerInfo> playerInfos)
    {
        players.Clear();
        drawingOrder.Clear();
        voteService.Clear();

        IReadOnlyList<LiarPainterRole> roles = RoleAssignmentService.CreateRoles(playerInfos.Count, random);
        for (int i = 0; i < playerInfos.Count; i++)
        {
            LiarPainterPlayerState state = new(playerInfos[i])
            {
                Role = roles[i],
                RoleAbility = RoleAbilityFactory.Create(roles[i])
            };
            state.PrivateKeyword = KeywordAssignmentService.GetPrivateKeyword(state.Role, roundContent);
            players.Add(state);
            drawingOrder.Add(state.PlayerId);
        }

        RoleAssignmentService.Shuffle(drawingOrder, random);
    }

    private bool ValidateStart(IReadOnlyList<LiarPainterPlayerInfo> playerInfos, out string error)
    {
        if (playerInfos == null || playerInfos.Count < rules.minimumPlayers || playerInfos.Count > rules.maximumPlayers)
        {
            error = $"플레이어 수는 {rules.minimumPlayers}~{rules.maximumPlayers}명이어야 합니다.";
            return false;
        }

        if (playerInfos.Any(player => player == null || string.IsNullOrWhiteSpace(player.playerId)) ||
            playerInfos.Select(player => player.playerId).Distinct().Count() != playerInfos.Count)
        {
            error = "플레이어 ID가 비어 있거나 중복되었습니다.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(roundContent.citizenKeyword) ||
            string.IsNullOrWhiteSpace(roundContent.drunkPainterKeyword) ||
            string.IsNullOrWhiteSpace(roundContent.question))
        {
            error = "시민 키워드, 알코올 중독 화가 키워드, 질문을 먼저 설정해야 합니다.";
            return false;
        }

        error = null;
        return true;
    }

    private IEnumerator RunTimer(float seconds)
    {
        float remaining = Mathf.Max(0f, seconds);
        while (remaining > 0f)
        {
            PhaseTimeChanged?.Invoke(remaining);
            remaining -= Time.unscaledDeltaTime;
            yield return null;
        }
        PhaseTimeChanged?.Invoke(0f);
    }

    private void SetPhase(LiarPainterGamePhase phase)
    {
        CurrentPhase = phase;
        PhaseChanged?.Invoke(phase);
    }

    private void ShowResult(LiarPainterGameOutcome outcome)
    {
        if (resultPopup == null) return;

        string title = outcome.WinningTeam == LiarPainterTeam.Citizen
            ? "시민 팀 승리!"
            : "사짜 화가 승리!";

        StringBuilder names = new();
        StringBuilder roles = new();
        StringBuilder keywords = new();
        StringBuilder results = new();
        foreach (LiarPainterPlayerState player in outcome.Players)
        {
            names.AppendLine(player.DisplayName);
            roles.AppendLine(GetRoleDisplayName(player.Role));
            keywords.AppendLine(string.IsNullOrWhiteSpace(player.PrivateKeyword) ? "미부여" : player.PrivateKeyword);
            results.AppendLine(player.Team == outcome.WinningTeam ? "승리" : "패배");
        }

        resultPopup.ShowResult(
            title,
            names.ToString().TrimEnd(),
            roles.ToString().TrimEnd(),
            keywords.ToString().TrimEnd(),
            results.ToString().TrimEnd());
    }

    private static string GetRoleDisplayName(LiarPainterRole role)
    {
        return role switch
        {
            LiarPainterRole.Appraiser => "감정가",
            LiarPainterRole.FakePainter => "사짜 화가",
            LiarPainterRole.DrunkPainter => "알코올 중독 화가",
            _ => "시민"
        };
    }
}
