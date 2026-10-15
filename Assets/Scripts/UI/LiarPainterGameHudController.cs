using System;
using System.Collections;
using System.Linq;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public sealed class LiarPainterGameHudController : MonoBehaviour
{
    [Header("게임 진행 HUD")]
    [SerializeField] private TMP_Text topicText;
    [SerializeField] private TMP_Text roundText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text gameStatusText;
    [SerializeField] private CanvasGroup personalProgressGroup;
    [SerializeField] private DrawingCanvasLineRenderer drawingCanvas;
    [SerializeField, Min(0f)] private float statusVisibleSeconds = 4f;
    [SerializeField, Min(0.01f)] private float statusFadeSeconds = 1f;

    [Header("개인 직업과 제시어 팝업")]
    [SerializeField] private GameObject assignmentPopup;
    [SerializeField] private TMP_Text assignmentRoleText;
    [SerializeField] private TMP_Text assignmentKeywordText;
    [SerializeField] private TMP_Text assignmentGuideText;
    [Header("질문 공개 팝업")]
    [SerializeField] private GameObject questionPopup;
    [SerializeField] private TMP_Text questionPopupText;
    [SerializeField] private TMP_Text roundGuideText;
    [SerializeField] private GameObject gameInterruptedPopup;

    public void CloseGameInterruptedPopup()
    {
        if (gameInterruptedPopup != null) gameInterruptedPopup.SetActive(false);
    }

    private void ShowGameInterruptedPopup()
    {
        GetComponent<GameResultPopupController>()?.HideResult();
        if (gameInterruptedPopup == null) return;
        gameInterruptedPopup.transform.SetAsLastSibling();
        gameInterruptedPopup.SetActive(true);
    }

    private LiarPainterNetworkGameCoordinator networkCoordinator;
    private bool localCanDraw;
    private LiarPainterGamePhase displayedPhase = LiarPainterGamePhase.Lobby;
    private Coroutine statusFadeRoutine;
    private PlayerSlotView[] votingSlots;
    private ulong selectedVote = ulong.MaxValue;

    public void LeaveRoom()
    {
        if (LiarPainterSessionManager.Instance != null)
        {
            _ = LiarPainterSessionManager.Instance.LeaveRoomAsync();
            return;
        }
        if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainScene");
    }

    public void Configure(TMP_Text topic, TMP_Text round, TMP_Text status, DrawingCanvasLineRenderer canvas)
    {
        topicText = topic;
        roundText = round;
        gameStatusText = status;
        drawingCanvas = canvas;
    }

    private void Awake()
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

        if (topicText == null) topicText = texts.FirstOrDefault(text => text.name == "TopicText");
        if (roundText == null) roundText = texts.FirstOrDefault(text => text.name == "RoundText");
        if (timerText == null) timerText = texts.FirstOrDefault(text => string.Equals(text.name, "TimerText", StringComparison.OrdinalIgnoreCase));
        if (gameStatusText == null) gameStatusText = texts.FirstOrDefault(text => text.name == "GameStatusText");
        if (drawingCanvas == null) drawingCanvas = GetComponentInChildren<DrawingCanvasLineRenderer>(true);

        networkCoordinator = GetComponent<LiarPainterNetworkGameCoordinator>();

        EnsureAssignmentPopup();
        EnsurePersonalProgressPopup();
        votingSlots = GetComponentsInChildren<PlayerSlotView>(true);
        foreach (var slot in votingSlots) slot.VoteButtonClicked += HandleVoteClicked;
        ResetHud();
    }

    private void OnEnable()
    {
        LiarPainterNetworkPlayer.LocalAssignmentReceived += ShowAssignment;
        BindNetworkCoordinator();
    }

    private void Start()
    {
        RefreshFromNetworkState();
    }

    private void Update()
    {
        if (networkCoordinator == null || !networkCoordinator.IsSpawned) return;
        double remaining = networkCoordinator.Phase.Value == LiarPainterGamePhase.Lobby
            ? 0d : Math.Max(0d, networkCoordinator.PhaseEndsAt.Value - networkCoordinator.NetworkManager.ServerTime.Time);

        SetTimer((float)remaining);
    }

    private void SetTimer(float remaining)
    {
        if (timerText == null) return;
        int seconds = Mathf.CeilToInt(Mathf.Max(0f, remaining));

        timerText.text = $"{seconds / 60:00}:{seconds % 60:00}";
    }

    public void EnsurePersonalProgressPopup()
    {
        // Keep the authored parent, size, alignment and colors.
        if (gameStatusText != null) gameStatusText.raycastTarget = false;
        if (personalProgressGroup != null) return;
        Transform popup = GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(item => item.name == "PersonalProgressPopup");
        if (popup == null) return;
        personalProgressGroup = popup.GetComponent<CanvasGroup>();
        if (personalProgressGroup == null)
            personalProgressGroup = popup.gameObject.AddComponent<CanvasGroup>();
        personalProgressGroup.interactable = false;
        personalProgressGroup.blocksRaycasts = false;
    }

    private void OnDisable()
    {
        if (questionPopup != null) questionPopup.SetActive(false);
        if (votingSlots != null) foreach (var slot in votingSlots) slot.SetLocalTurn(false);
        LiarPainterNetworkPlayer.LocalAssignmentReceived -= ShowAssignment;
        if (statusFadeRoutine != null)
        {
            StopCoroutine(statusFadeRoutine);
            statusFadeRoutine = null;
        }

        UnbindNetworkCoordinator();
    }

    private void ResetHud()
    {
        localCanDraw = false;
        GetComponent<DrawingTurnFocusController>()?.SetTurnPresentation(false, false);
        if (questionPopup != null) questionPopup.SetActive(false);
        if (votingSlots != null) foreach (var slot in votingSlots) slot.SetLocalTurn(false);
        foreach (var slot in GetComponentsInChildren<PlayerSlotView>(true)) slot.ClearPrivateAssignment();
        HideAssignmentPopup();
        SetTopic("질문 공개 대기 중");
        SetRound(0, 0);
        SetTimer(0f);
        SetStatus(LiarPainterGamePhase.Lobby);
        drawingCanvas?.SetDrawingPermission(false, string.Empty);
    }



    private void BindNetworkCoordinator()
    {
        if (networkCoordinator == null) return;
        networkCoordinator.Question.OnValueChanged += HandleNetworkQuestionChanged;
        networkCoordinator.DefenseName.OnValueChanged += HandleDefenseNameChanged;
        networkCoordinator.RoundNumber.OnValueChanged += HandleRoundNumberChanged;
        networkCoordinator.AnonymousTurn.OnValueChanged += HandleNetworkTurnChanged;
        networkCoordinator.DrawingPlayerCount.OnValueChanged += HandleNetworkPlayerCountChanged;
        networkCoordinator.Phase.OnValueChanged += HandleNetworkPhaseChanged;
        networkCoordinator.LocalDrawingPermissionChanged += HandleNetworkDrawingPermissionChanged;
        networkCoordinator.DrawingTurnStarted += HandleDrawingTurnStarted;
        networkCoordinator.ReturnedToWaitingRoom += HandleReturnedToWaitingRoom;
        networkCoordinator.GameInterrupted += ShowGameInterruptedPopup;
        networkCoordinator.ResultReceived += HandleResultReceived;
        networkCoordinator.VoteCountsChanged += RefreshVoteButtons;
        networkCoordinator.RoundStateChanged += HandleRoundStateChanged;
    }

    private void UnbindNetworkCoordinator()
    {
        if (networkCoordinator == null) return;
        networkCoordinator.Question.OnValueChanged -= HandleNetworkQuestionChanged;
        networkCoordinator.DefenseName.OnValueChanged -= HandleDefenseNameChanged;
        networkCoordinator.RoundNumber.OnValueChanged -= HandleRoundNumberChanged;
        networkCoordinator.AnonymousTurn.OnValueChanged -= HandleNetworkTurnChanged;
        networkCoordinator.DrawingPlayerCount.OnValueChanged -= HandleNetworkPlayerCountChanged;
        networkCoordinator.Phase.OnValueChanged -= HandleNetworkPhaseChanged;
        networkCoordinator.LocalDrawingPermissionChanged -= HandleNetworkDrawingPermissionChanged;
        networkCoordinator.DrawingTurnStarted -= HandleDrawingTurnStarted;
        networkCoordinator.ReturnedToWaitingRoom -= HandleReturnedToWaitingRoom;
        networkCoordinator.GameInterrupted -= ShowGameInterruptedPopup;
        networkCoordinator.ResultReceived -= HandleResultReceived;
        networkCoordinator.VoteCountsChanged -= RefreshVoteButtons;
        networkCoordinator.RoundStateChanged -= HandleRoundStateChanged;
    }





    private void HandleRoundNumberChanged(int previous, int current) => RefreshRoundGuide();

    private void RefreshRoundGuide()
    {
        if (roundGuideText == null && questionPopup != null)
            roundGuideText = questionPopup.GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(text => text.name == "RoundGuideText");
        if (roundGuideText == null || networkCoordinator == null) return;
        roundGuideText.text = networkCoordinator.RoundNumber.Value switch
        {
            1 => "첫 번째 질문",
            2 => "마지막 질문",
            _ => string.Empty
        };
    }

    private void HandleDefenseNameChanged(FixedString64Bytes previous, FixedString64Bytes current)
    {
        // Network variables can reach clients after the phase change callback.
        if (networkCoordinator != null &&
            networkCoordinator.Phase.Value == LiarPainterGamePhase.FinalDefense)
            SetStatus(LiarPainterGamePhase.FinalDefense);
    }

    private void HandleNetworkQuestionChanged(FixedString128Bytes previous, FixedString128Bytes current)
    {
        SetTopic(current.ToString());
        if (questionPopupText != null) questionPopupText.text = current.ToString();
    }

    private void HandleNetworkTurnChanged(int previous, int current)
    {
        SetRound(current, networkCoordinator.DrawingPlayerCount.Value);
        if (networkCoordinator.Phase.Value == LiarPainterGamePhase.Drawing)
            SetStatus(LiarPainterGamePhase.Drawing);
    }

    private void HandleNetworkPlayerCountChanged(int previous, int current)
    {
        SetRound(networkCoordinator.AnonymousTurn.Value, current);
    }

    private void HandleNetworkPhaseChanged(LiarPainterGamePhase previous, LiarPainterGamePhase current)
    {
        if (previous != current && (current == LiarPainterGamePhase.QuestionReveal ||
            current == LiarPainterGamePhase.Discussion || current == LiarPainterGamePhase.FinalDefense))
            UIAudioManager.Instance?.PlayRoundStart();
        if (current == LiarPainterGamePhase.RoleReveal) CloseGameInterruptedPopup();
        if (current == LiarPainterGamePhase.RoleReveal) GetComponent<GameResultPopupController>()?.HideResult();
        if (current != LiarPainterGamePhase.RoleReveal) HideAssignmentPopup();
        if (current != LiarPainterGamePhase.Drawing && current != LiarPainterGamePhase.FinalDefense)
        {
            localCanDraw = false;
            drawingCanvas?.SetDrawingPermission(false, string.Empty);
        }

        SetStatus(current);
        RefreshTurnPresentation();
        selectedVote = ulong.MaxValue;
        RefreshVoteButtons();
    }

    private void HandleDrawingTurnStarted(bool isMyTurn)
    {
        if (UIAudioManager.Instance != null) UIAudioManager.Instance.PlayDrawingTurn(isMyTurn);
    }

    private void HandleNetworkDrawingPermissionChanged(bool allowed)
    {
        localCanDraw = allowed;
        string playerId = NetworkManager.Singleton != null ? $"client-{NetworkManager.Singleton.LocalClientId}" : string.Empty;
        drawingCanvas?.SetDrawingPermission(allowed, playerId);
        RefreshTurnPresentation();
        SetStatus(networkCoordinator.Phase.Value);
    }

    private void HandleReturnedToWaitingRoom()
    {
        NetworkObject playerObject = NetworkManager.Singleton?.LocalClient?.PlayerObject;

        if (playerObject != null && playerObject.TryGetComponent(out LiarPainterNetworkPlayer player))
            player.ClearLocalAssignment();
        ResetHud();
    }

    private void HandleVoteClicked(PlayerSlotView slot)
    {
        if (networkCoordinator == null || !networkCoordinator.IsSpawned ||
            networkCoordinator.Phase.Value != LiarPainterGamePhase.Voting || !slot.IsOccupied || !networkCoordinator.IsVoteCandidate(slot.NetworkClientId)) return;
        networkCoordinator.SubmitVote(slot.NetworkClientId);
        selectedVote = slot.NetworkClientId;
        RefreshVoteButtons();
    }

    private void HandleRoundStateChanged()
    {
        selectedVote = ulong.MaxValue;
        RefreshVoteButtons();
    }

    private void RefreshVoteButtons()
    {
        if (votingSlots == null) return;
        bool voting = networkCoordinator != null && networkCoordinator.IsSpawned && networkCoordinator.Phase.Value == LiarPainterGamePhase.Voting;

        foreach (var slot in votingSlots)
        {
            slot.SetMissedTurn(networkCoordinator != null ? networkCoordinator.GetMissedTurn(slot.NetworkClientId) : 0);
            slot.SetVoteState(voting, voting && networkCoordinator.IsVoteCandidate(slot.NetworkClientId), slot.NetworkClientId == selectedVote);
            slot.SetVoteCount(voting ? networkCoordinator.GetReceivedVotes(slot.NetworkClientId) : 0);
        }
    }

    private void HandleResultReceived(string json)
    {
        var result = JsonUtility.FromJson<NetworkResultPayload>(json);
        if (result?.players == null) return;
        var popup = GetComponent<GameResultPopupController>();
        if (popup == null) return;
        UIAudioManager.Instance?.PlayRoundStart();
        popup.ShowResult(result.winningTeam == LiarPainterTeam.Draw ? "무승부" : result.winningTeam == LiarPainterTeam.Citizen ? "시민 팀 승리" : "사짜 화가 승리",
            string.Join("\n", result.players.Select(p => p.displayName)),
            string.Join("\n", result.players.Select(p => p.role switch {
                LiarPainterRole.FakePainter => "사짜 화가", LiarPainterRole.Appraiser => "감정가",
                LiarPainterRole.DrunkPainter => "알코올 중독 화가", _ => "시민" })),
            string.Join("\n", result.players.Select(p => string.IsNullOrEmpty(p.keyword) ? "없음" : p.keyword)),
            string.Join("\n", result.players.Select(p => result.winningTeam == LiarPainterTeam.Draw ? "무승부" : p.won ? "승리" : "패배")), true);
    }

    private void OnDestroy()
    {
        if (votingSlots != null)
            foreach (var slot in votingSlots) slot.VoteButtonClicked -= HandleVoteClicked;
    }

    private void RefreshFromNetworkState()
    {
        if (networkCoordinator == null || !networkCoordinator.IsSpawned) return;
        string question = networkCoordinator.Question.Value.ToString();
        if (!string.IsNullOrWhiteSpace(question)) SetTopic(question);
        SetRound(networkCoordinator.AnonymousTurn.Value, networkCoordinator.DrawingPlayerCount.Value);
        localCanDraw = networkCoordinator.LocalCanDraw;
        NetworkObject playerObject = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        if (networkCoordinator.Phase.Value == LiarPainterGamePhase.RoleReveal &&
            playerObject != null && playerObject.TryGetComponent(out LiarPainterNetworkPlayer player) &&
            player.HasLocalAssignment)
            ShowAssignment(player.LocalAssignment);
        SetStatus(networkCoordinator.Phase.Value);
        RefreshTurnPresentation();
        RefreshVoteButtons();
    }

    private void SetTopic(string question)
    {
        if (topicText == null) return;
        topicText.text = string.IsNullOrWhiteSpace(question) ? "질문 공개 대기 중.." : $"{question}";
    }

    private void RefreshTurnPresentation()
    {
        var focus = GetComponent<DrawingTurnFocusController>();
        if (focus != null)
        {
            bool drawingPhase = networkCoordinator != null &&
                (networkCoordinator.Phase.Value == LiarPainterGamePhase.Drawing ||
                 networkCoordinator.Phase.Value == LiarPainterGamePhase.FinalDefense);
            focus.SetTurnPresentation(drawingPhase, localCanDraw);
        }
        if (votingSlots != null)
            foreach (var slot in votingSlots) slot.SetLocalTurn(localCanDraw);
        if (questionPopup == null || networkCoordinator == null) return;
        bool visible = networkCoordinator.Phase.Value == LiarPainterGamePhase.QuestionReveal;
        if (visible)
        {
            RefreshRoundGuide();
            if (questionPopupText != null) questionPopupText.text = networkCoordinator.Question.Value.ToString();
            questionPopup.transform.SetAsLastSibling();
        }
        questionPopup.SetActive(visible);
    }

    private void SetRound(int current, int total)
    {
        if (roundText == null) return;
        roundText.text = displayedPhase switch
        {
            LiarPainterGamePhase.RoleReveal or LiarPainterGamePhase.QuestionReveal => "키워드 및 직업 확인",
            LiarPainterGamePhase.Drawing when current > 0 && total > 0 => $"그림 그리기: {current}/{total}",
            LiarPainterGamePhase.Drawing => "그림 그리기",
            LiarPainterGamePhase.Discussion => "검증 시간",
            LiarPainterGamePhase.Voting => "투표 시간",
            LiarPainterGamePhase.FinalDefense => "최후 변론",
            LiarPainterGamePhase.Result => "게임 결과",
            _ => "게임 대기"
        };
    }

    private void SetStatus(LiarPainterGamePhase phase)
    {
        displayedPhase = phase;

        SetRound(networkCoordinator != null ? networkCoordinator.AnonymousTurn.Value : 0,
            networkCoordinator != null ? networkCoordinator.DrawingPlayerCount.Value : 0);

        if (gameStatusText == null) return;

        if (phase == LiarPainterGamePhase.Result)
        {
            if (statusFadeRoutine != null) StopCoroutine(statusFadeRoutine);
            statusFadeRoutine = null;
            gameStatusText.text = string.Empty;
            SetStatusAlpha(0f);
            return;
        }

        gameStatusText.text = phase switch
        {
            LiarPainterGamePhase.RoleReveal => "직업과 키워드를 확인하세요.",
            LiarPainterGamePhase.QuestionReveal => "이번 질문을 확인하세요.",
            LiarPainterGamePhase.Drawing when localCanDraw => "당신의 턴입니다. 그림을 그려주세요!",
            LiarPainterGamePhase.Drawing => "다른 플레이어가 그림을 그리고 있습니다.",
            LiarPainterGamePhase.Discussion => "검증 시간입니다. 완성된 그림을 확인하세요.",
            LiarPainterGamePhase.Voting => "사짜 화가라고 생각하는 플레이어에게 투표하세요.",
            LiarPainterGamePhase.FinalDefense =>
                $"<color=#FF4444>{networkCoordinator.DefenseName.Value.ToString().Replace("<", "＜").Replace(">", "＞")}</color>님의 최후 변론입니다.",
            _ => "플레이어 준비를 기다리고 있습니다."
        };

        if (phase == LiarPainterGamePhase.FinalDefense) gameStatusText.richText = true;
        RestartStatusFade();
    }

    private void RestartStatusFade()
    {
        if (statusFadeRoutine != null) StopCoroutine(statusFadeRoutine);
        SetStatusAlpha(1f);
        statusFadeRoutine = StartCoroutine(FadeStatusAfterDelay());
    }

    private IEnumerator FadeStatusAfterDelay()
    {
        yield return new WaitForSecondsRealtime(statusVisibleSeconds);

        float elapsed = 0f;
        while (elapsed < statusFadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            SetStatusAlpha(1f - Mathf.Clamp01(elapsed / statusFadeSeconds));
            yield return null;
        }

        SetStatusAlpha(0f);
        statusFadeRoutine = null;
    }

    private void SetStatusAlpha(float alpha)
    {
        if (personalProgressGroup != null) personalProgressGroup.alpha = alpha;
    }


    private void ShowAssignment(LiarPainterPrivateAssignment assignment)
    {
        foreach (var slot in GetComponentsInChildren<PlayerSlotView>(true))
            if (slot.IsLocalPlayer) slot.ShowPrivateAssignment(assignment);
        EnsureAssignmentPopup();
        if (assignmentPopup == null || assignmentRoleText == null || assignmentKeywordText == null || assignmentGuideText == null) return;
        assignmentRoleText.text = assignment.VisibleRole switch
        {
            LiarPainterRole.FakePainter => "사짜 화가",
            LiarPainterRole.Appraiser => "감정가",
            _ => "시민"
        };

        bool isFake = assignment.VisibleRole == LiarPainterRole.FakePainter;
        assignmentKeywordText.text = isFake ? "제시어가 제공되지 않습니다" : assignment.Keyword;
        assignmentGuideText.text = assignment.VisibleRole switch
        {
            LiarPainterRole.FakePainter => "다른 사람의 그림을 보고 제시어를 유추하세요. 검증 시간에 제시어를 맞히면 즉시 승리합니다.",
            LiarPainterRole.Appraiser => "라운드마다 한 번, 검증 시간에 원하는 선을 클릭해 작성자를 확인할 수 있습니다.",
            _ => "질문과 제시어를 연결해 그림을 그리세요. 다른 플레이어의 그림을 보고 사짜 화가를 찾아내세요!"
        };

        assignmentPopup.transform.SetAsLastSibling();
        bool opening = !assignmentPopup.activeSelf;
        assignmentPopup.SetActive(true);
        if (opening) UIAudioManager.Instance?.PlayRoundStart();
    }

    private void HideAssignmentPopup()
    {
        if (assignmentPopup == null) return;
        assignmentPopup.SetActive(false);
        // Clear private information when the reveal phase ends.
        if (assignmentRoleText != null) assignmentRoleText.text = string.Empty;
        if (assignmentKeywordText != null) assignmentKeywordText.text = string.Empty;
        if (assignmentGuideText != null) assignmentGuideText.text = string.Empty;
    }

    public void EnsureAssignmentPopup()
    {
        if (assignmentPopup == null)
            assignmentPopup = transform.Find("PrivateAssignmentPopup")?.gameObject;

        if (assignmentPopup == null) return;

        Transform popupWindow = assignmentPopup.transform.Find("PopupWindow");

        if (assignmentRoleText == null) assignmentRoleText = popupWindow?.Find("RoleText")?.GetComponent<TMP_Text>();
        if (assignmentKeywordText == null) assignmentKeywordText = popupWindow?.Find("KeywordText")?.GetComponent<TMP_Text>();
        if (assignmentGuideText == null) assignmentGuideText = popupWindow?.Find("GuideText")?.GetComponent<TMP_Text>();
    }
}
