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
    [SerializeField] private TMP_Text gameStatusText;
    [SerializeField] private DrawingCanvasLineRenderer drawingCanvas;
    [SerializeField, Min(0f)] private float statusVisibleSeconds = 4f;
    [SerializeField, Min(0.01f)] private float statusFadeSeconds = 1f;
    [Header("개인 직업과 제시어 팝업")]
    [SerializeField] private GameObject assignmentPopup;
    [SerializeField] private TMP_Text assignmentRoleText;
    [SerializeField] private TMP_Text assignmentKeywordText;
    [SerializeField] private TMP_Text assignmentGuideText;

    private LiarPainterGameCoordinator localCoordinator;
    private LiarPainterNetworkGameCoordinator networkCoordinator;
    private bool localCanDraw;
    private Coroutine statusFadeRoutine;

    public void Configure(
        TMP_Text topic,
        TMP_Text round,
        TMP_Text status,
        DrawingCanvasLineRenderer canvas)
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
        if (gameStatusText == null) gameStatusText = texts.FirstOrDefault(text => text.name == "GameStatusText");
        if (drawingCanvas == null) drawingCanvas = GetComponentInChildren<DrawingCanvasLineRenderer>(true);

        localCoordinator = GetComponent<LiarPainterGameCoordinator>();
        networkCoordinator = GetComponent<LiarPainterNetworkGameCoordinator>();
        EnsureAssignmentPopup();
        ResetHud();
    }

    private void OnEnable()
    {
        BindLocalCoordinator();
        BindNetworkCoordinator();
    }

    private void Start()
    {
        RefreshFromNetworkState();
    }

    private void OnDisable()
    {
        if (statusFadeRoutine != null)
        {
            StopCoroutine(statusFadeRoutine);
            statusFadeRoutine = null;
        }

        UnbindLocalCoordinator();
        UnbindNetworkCoordinator();
    }

    private void ResetHud()
    {
        HideAssignmentPopup();
        SetTopic("질문 공개 대기 중");
        SetRound(0, 0);
        SetStatus(LiarPainterGamePhase.Lobby);
        drawingCanvas?.SetDrawingPermission(false, string.Empty);
    }

    private void BindLocalCoordinator()
    {
        if (localCoordinator == null) return;
        localCoordinator.QuestionRevealed += HandleLocalQuestionRevealed;
        localCoordinator.AnonymousDrawingTurnStarted += HandleLocalDrawingTurnStarted;
        localCoordinator.DrawingPermissionChanged += HandleLocalDrawingPermissionChanged;
        localCoordinator.PhaseChanged += HandleLocalPhaseChanged;
        localCoordinator.PrivateAssignmentCreated += HandleLocalAssignment;
    }

    private void UnbindLocalCoordinator()
    {
        if (localCoordinator == null) return;
        localCoordinator.QuestionRevealed -= HandleLocalQuestionRevealed;
        localCoordinator.AnonymousDrawingTurnStarted -= HandleLocalDrawingTurnStarted;
        localCoordinator.DrawingPermissionChanged -= HandleLocalDrawingPermissionChanged;
        localCoordinator.PhaseChanged -= HandleLocalPhaseChanged;
        localCoordinator.PrivateAssignmentCreated -= HandleLocalAssignment;
    }

    private void BindNetworkCoordinator()
    {
        if (networkCoordinator == null) return;
        networkCoordinator.Question.OnValueChanged += HandleNetworkQuestionChanged;
        networkCoordinator.AnonymousTurn.OnValueChanged += HandleNetworkTurnChanged;
        networkCoordinator.DrawingPlayerCount.OnValueChanged += HandleNetworkPlayerCountChanged;
        networkCoordinator.Phase.OnValueChanged += HandleNetworkPhaseChanged;
        networkCoordinator.LocalDrawingPermissionChanged += HandleNetworkDrawingPermissionChanged;
        networkCoordinator.ReturnedToWaitingRoom += HandleReturnedToWaitingRoom;
        networkCoordinator.PrivateAssignmentReceived += ShowAssignment;
    }

    private void UnbindNetworkCoordinator()
    {
        if (networkCoordinator == null) return;
        networkCoordinator.Question.OnValueChanged -= HandleNetworkQuestionChanged;
        networkCoordinator.AnonymousTurn.OnValueChanged -= HandleNetworkTurnChanged;
        networkCoordinator.DrawingPlayerCount.OnValueChanged -= HandleNetworkPlayerCountChanged;
        networkCoordinator.Phase.OnValueChanged -= HandleNetworkPhaseChanged;
        networkCoordinator.LocalDrawingPermissionChanged -= HandleNetworkDrawingPermissionChanged;
        networkCoordinator.ReturnedToWaitingRoom -= HandleReturnedToWaitingRoom;
        networkCoordinator.PrivateAssignmentReceived -= ShowAssignment;
    }

    private void HandleLocalQuestionRevealed(string question)
    {
        SetTopic(question);
    }

    private void HandleLocalDrawingTurnStarted(int current, int total)
    {
        SetRound(current, total);
        SetStatus(LiarPainterGamePhase.Drawing);
    }

    private void HandleLocalDrawingPermissionChanged(string playerId, bool allowed)
    {
        string localPlayerId = localCoordinator.Players.Count > 0
            ? localCoordinator.Players[0].PlayerId
            : string.Empty;

        if (!string.Equals(playerId, localPlayerId, StringComparison.Ordinal)) return;
        localCanDraw = allowed;
        drawingCanvas?.SetDrawingPermission(allowed, localPlayerId);
        SetStatus(localCoordinator.CurrentPhase);
    }

    private void HandleLocalPhaseChanged(LiarPainterGamePhase phase)
    {
        if (phase != LiarPainterGamePhase.RoleReveal) HideAssignmentPopup();
        if (phase != LiarPainterGamePhase.Drawing)
        {
            localCanDraw = false;
            drawingCanvas?.SetDrawingPermission(false, string.Empty);
        }

        if (phase == LiarPainterGamePhase.Lobby)
        {
            SetTopic("질문 공개 대기 중");
            SetRound(0, 0);
        }

        SetStatus(phase);
    }

    private void HandleNetworkQuestionChanged(FixedString128Bytes previous, FixedString128Bytes current)
    {
        SetTopic(current.ToString());
    }

    private void HandleNetworkTurnChanged(int previous, int current)
    {
        SetRound(current, networkCoordinator.DrawingPlayerCount.Value);
    }

    private void HandleNetworkPlayerCountChanged(int previous, int current)
    {
        SetRound(networkCoordinator.AnonymousTurn.Value, current);
    }

    private void HandleNetworkPhaseChanged(LiarPainterGamePhase previous, LiarPainterGamePhase current)
    {
        if (current != LiarPainterGamePhase.RoleReveal) HideAssignmentPopup();
        if (current != LiarPainterGamePhase.Drawing)
        {
            localCanDraw = false;
            drawingCanvas?.SetDrawingPermission(false, string.Empty);
        }

        SetStatus(current);
    }

    private void HandleNetworkDrawingPermissionChanged(bool allowed)
    {
        localCanDraw = allowed;
        string playerId = NetworkManager.Singleton != null
            ? $"client-{NetworkManager.Singleton.LocalClientId}"
            : string.Empty;
        drawingCanvas?.SetDrawingPermission(allowed, playerId);
        SetStatus(networkCoordinator.Phase.Value);
    }

    private void HandleReturnedToWaitingRoom()
    {
        ResetHud();
    }

    private void RefreshFromNetworkState()
    {
        if (networkCoordinator == null || !networkCoordinator.IsSpawned) return;
        string question = networkCoordinator.Question.Value.ToString();
        if (!string.IsNullOrWhiteSpace(question)) SetTopic(question);
        SetRound(networkCoordinator.AnonymousTurn.Value, networkCoordinator.DrawingPlayerCount.Value);
        localCanDraw = networkCoordinator.LocalCanDraw;
        SetStatus(networkCoordinator.Phase.Value);
    }

    private void SetTopic(string question)
    {
        if (topicText == null) return;
        topicText.text = string.IsNullOrWhiteSpace(question)
            ? "질문 공개 대기 중"
            : $"질문: {question}";
    }

    private void SetRound(int current, int total)
    {
        if (roundText == null) return;
        roundText.text = current > 0 && total > 0
            ? $"차례: {current} / {total}"
            : "차례: - / -";
    }

    private void SetStatus(LiarPainterGamePhase phase)
    {
        if (gameStatusText == null) return;
        gameStatusText.text = phase switch
        {
            LiarPainterGamePhase.RoleReveal => "직업과 키워드를 확인하세요.",
            LiarPainterGamePhase.QuestionReveal => "이번 질문을 확인하세요.",
            LiarPainterGamePhase.Drawing when localCanDraw => "당신의 턴입니다. 그림을 그려주세요!",
            LiarPainterGamePhase.Drawing => "다른 플레이어가 그림을 그리고 있습니다.",
            LiarPainterGamePhase.Discussion => "토론 시간입니다. 완성된 그림을 확인하세요.",
            LiarPainterGamePhase.Voting => "사짜 화가라고 생각하는 플레이어에게 투표하세요.",
            LiarPainterGamePhase.Result => "게임 결과를 확인하세요.",
            _ => "플레이어 준비를 기다리고 있습니다."
        };

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
        Color color = gameStatusText.color;
        color.a = alpha;
        gameStatusText.color = color;
    }

    private void HandleLocalAssignment(LiarPainterPrivateAssignment assignment)
    {
        if (localCoordinator.Players.Count > 0 &&
            assignment.PlayerId == localCoordinator.Players[0].PlayerId)
            ShowAssignment(assignment);
    }

    private void ShowAssignment(LiarPainterPrivateAssignment assignment)
    {
        EnsureAssignmentPopup();
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
            LiarPainterRole.FakePainter => "다른 사람의 그림을 보고 제시어를 유추하세요.",
            LiarPainterRole.Appraiser => "토론 중 한 번, 선택한 선을 누가 그렸는지 감정할 수 있습니다.",
            _ => "질문과 제시어를 연결해서 그림을 그려주세요."
        };
        assignmentPopup.transform.SetAsLastSibling();
        assignmentPopup.SetActive(true);
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
        if (assignmentPopup == null)
        {
            assignmentPopup = new GameObject("PrivateAssignmentPopup", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            assignmentPopup.transform.SetParent(transform, false);
            RectTransform overlay = assignmentPopup.GetComponent<RectTransform>();
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            assignmentPopup.GetComponent<UnityEngine.UI.Image>().color = new Color(0.02f, 0.03f, 0.05f, 0.86f);

            GameObject window = new GameObject("PopupWindow", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            window.transform.SetParent(overlay, false);
            RectTransform rect = window.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(760f, 480f);
            window.GetComponent<UnityEngine.UI.Image>().color = new Color32(27, 35, 46, 255);
            CreateAssignmentText(window.transform, "TitleText", "당신의 직업과 제시어", 34f, 185f, 70f);
            assignmentRoleText = CreateAssignmentText(window.transform, "RoleText", "", 46f, 100f, 72f);
            CreateAssignmentText(window.transform, "KeywordLabelText", "제시어", 24f, 20f, 45f);
            assignmentKeywordText = CreateAssignmentText(window.transform, "KeywordText", "", 36f, -45f, 75f);
            assignmentGuideText = CreateAssignmentText(window.transform, "GuideText", "", 23f, -135f, 65f);
            CreateAssignmentText(window.transform, "NoticeText", "잠시 후 질문이 공개됩니다", 20f, -205f, 35f);
            assignmentPopup.SetActive(false);
        }
        Transform popupWindow = assignmentPopup.transform.Find("PopupWindow");
        if (assignmentRoleText == null) assignmentRoleText = popupWindow?.Find("RoleText")?.GetComponent<TMP_Text>();
        if (assignmentKeywordText == null) assignmentKeywordText = popupWindow?.Find("KeywordText")?.GetComponent<TMP_Text>();
        if (assignmentGuideText == null) assignmentGuideText = popupWindow?.Find("GuideText")?.GetComponent<TMP_Text>();
    }

    private TMP_Text CreateAssignmentText(Transform parent, string objectName, string value, float size, float y, float height)
    {
        GameObject label = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        label.transform.SetParent(parent, false);
        TMP_Text text = label.GetComponent<TMP_Text>();
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        text.rectTransform.sizeDelta = new Vector2(680f, height);
        text.rectTransform.anchoredPosition = new Vector2(0f, y);
        TMP_Text fontSource = topicText != null ? topicText : GetComponentsInChildren<TMP_Text>(true)
            .FirstOrDefault(candidate => candidate != text && candidate.font != null);
        if (fontSource != null) text.font = fontSource.font;
        text.fontSize = size;
        text.color = new Color32(242, 229, 202, 255);
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.richText = false;
        text.text = value;
        return text;
    }
}
