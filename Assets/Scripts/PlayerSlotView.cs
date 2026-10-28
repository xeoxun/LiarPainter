using TMPro;
using System.Linq;
using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerSlotView : MonoBehaviour
{
    [SerializeField] private CanvasGroup turnBorder;
    [SerializeField] private CanvasGroup turnDimmer;
    private bool dimmedForTurn;

    public void SetTurnDimmed(bool dimmed) => dimmedForTurn = dimmed && !IsLocalPlayer;
    [Header("Turn border breathing light")]
    [SerializeField, Min(0.2f)] private float turnPulseSeconds = 2.6f;
    [SerializeField, Range(0f, 1f)] private float turnMinimumAlpha = 0.3f;
    [SerializeField, Range(0f, 1f)] private float turnMaximumAlpha = 1f;
    [SerializeField, Min(0.01f)] private float turnFadeSeconds = 0.3f;
    private bool showingTurn;
    private float turnVisibility;
    private float turnPulseTime;

    public void SetLocalTurn(bool allowed)
    {
        bool next = allowed && IsOccupied && IsLocalPlayer;

        if (showingTurn == next) return;

        showingTurn = next;

        if (showingTurn && turnBorder != null)
        {
            turnPulseTime = 0f;
            turnBorder.alpha = 0f;
            turnBorder.gameObject.SetActive(true);
        }
    }

    private void Update()
    {
        if (turnDimmer != null)
            turnDimmer.alpha = Mathf.MoveTowards(turnDimmer.alpha, dimmedForTurn ? 1f : 0f,
                Time.unscaledDeltaTime / Mathf.Max(0.01f, turnFadeSeconds));
        if (turnBorder == null || (!showingTurn && turnVisibility <= 0f)) return;

        float delta = Time.unscaledDeltaTime;
        turnVisibility = Mathf.MoveTowards(turnVisibility, showingTurn ? 1f : 0f, delta / Mathf.Max(0.01f, turnFadeSeconds));
        turnPulseTime += delta;

        float phase = turnPulseTime * (Mathf.PI * 2f / Mathf.Max(0.2f, turnPulseSeconds));
        float breath = 0.5f - 0.5f * Mathf.Cos(phase);
        breath = Mathf.SmoothStep(0f, 1f, breath);
        turnBorder.alpha = Mathf.SmoothStep(0f, 1f, turnVisibility) * Mathf.Lerp(turnMinimumAlpha, turnMaximumAlpha, breath);
        
        if (!showingTurn && turnVisibility <= 0f) turnBorder.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        showingTurn = false;
        dimmedForTurn = false;
        if (turnDimmer != null) turnDimmer.alpha = 0f;
        turnVisibility = 0f;
        if (turnBorder != null) { turnBorder.alpha = 0f; turnBorder.gameObject.SetActive(false); }
    }
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Image background;
    [SerializeField] private Image stateLine;
    [SerializeField] private Button readyButton;
    [SerializeField] private TMP_Text readyButtonText;
    [SerializeField] private Button startGameButton;
    [SerializeField] private TMP_Text privateRoleText;
    [SerializeField] private TMP_Text privateKeywordText;
    [SerializeField] private TMP_Text warningText;

    public void SetMissedTurn(int turn)
    {
        if (warningText == null) warningText = GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.name == "WarningText");
        if (warningText != null) { warningText.text = turn > 0 ? $"{turn}번째 - 그림 미작성" : string.Empty; warningText.gameObject.SetActive(turn > 0); }
    }
    private bool hasPrivateAssignment;
    private bool gameInProgress;

    public void SetGameInProgress(bool active)
    {
        gameInProgress = active;
        RefreshStatusVisibility();
    }

    private void RefreshStatusVisibility()
    {
        if (statusText == null)
            statusText = GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(text => text.name == "StateText" || text.name == "StatusText");
        if (statusText != null)
            statusText.gameObject.SetActive(!gameInProgress && !hasPrivateAssignment);
    }

    public void ShowPrivateAssignment(LiarPainterPrivateAssignment assignment)
    {
        if (!IsOccupied || !IsLocalPlayer) { ClearPrivateAssignment(); return; }
        hasPrivateAssignment = true;
        if (privateRoleText == null) privateRoleText = transform.Find("PrivateRoleText")?.GetComponent<TMP_Text>();
        if (privateKeywordText == null) privateKeywordText = transform.Find("PrivateKeywordText")?.GetComponent<TMP_Text>();
        if (privateRoleText != null)
        {
            privateRoleText.text = "직업: " + (assignment.VisibleRole switch
            {
                LiarPainterRole.FakePainter => "사짜 화가",
                LiarPainterRole.Appraiser => "감정가",
                _ => "시민"
            });
            privateRoleText.gameObject.SetActive(true);
        }
        if (privateKeywordText != null)
        {
            privateKeywordText.text = "제시어: " + (assignment.VisibleRole == LiarPainterRole.FakePainter ? "없음" : assignment.Keyword);
            privateKeywordText.gameObject.SetActive(true);
        }
        RefreshReadyControls();
    }

    public void ClearPrivateAssignment()
    {
        hasPrivateAssignment = false;
        if (privateRoleText == null) privateRoleText = transform.Find("PrivateRoleText")?.GetComponent<TMP_Text>();
        if (privateKeywordText == null) privateKeywordText = transform.Find("PrivateKeywordText")?.GetComponent<TMP_Text>();
        foreach (var label in new[] { privateRoleText, privateKeywordText })
            if (label != null) { label.text = string.Empty; label.gameObject.SetActive(false); }
        RefreshReadyControls();
    }
    [SerializeField] private UnityEngine.UI.Button voteButton;
    [SerializeField] private TMP_Text voteCountText;
    private int receivedVotes;

    public void SetVoteCount(int count)
    {
        receivedVotes = Mathf.Max(0, count);
        if (voteCountText == null) voteCountText = transform.Find("VoteCountText")?.GetComponent<TMP_Text>();
        if (voteCountText == null) return;
        voteCountText.text = $"{receivedVotes}표";
        voteCountText.gameObject.SetActive(voting && IsOccupied);
    }
    private bool voting;
    public event Action<PlayerSlotView> VoteButtonClicked;

    public void SetVoteState(bool votingPhase, bool canVoteForSlot, bool selected)
    {
        voting = votingPhase;
        if (voteButton == null) voteButton = transform.Find("VoteButton")?.GetComponent<UnityEngine.UI.Button>();
        if (voteButton != null)
        {
            voteButton.gameObject.SetActive(votingPhase && canVoteForSlot && IsOccupied);
            var label = voteButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = selected ? "선택됨" : "투표";
        }
        RefreshReadyControls();
    }

    public void HandleVoteClicked() => VoteButtonClicked?.Invoke(this);


    public bool IsOccupied { get; private set; }
    public bool IsReady { get; private set; }
    public bool IsLocalPlayer { get; private set; }
    public bool IsHost { get; private set; }
    public ulong NetworkClientId { get; private set; } = ulong.MaxValue;

    public event Action<PlayerSlotView> ReadyButtonClicked;
    public event Action<PlayerSlotView> StartGameButtonClicked;

    public void Configure(TMP_Text playerName, TMP_Text status, Image slotBackground, Image line)
    {
        playerNameText = playerName;
        statusText = status;
        background = slotBackground;
        stateLine = line;
    }

    public void ConfigureReadyButtons(Button ready, TMP_Text readyLabel, Button start)
    {
        readyButton = ready;
        readyButtonText = readyLabel;
        startGameButton = start;
        RefreshReadyControls();
    }

    public void SetEmpty(int slotNumber)
    {
        SetLocalTurn(false);
        ClearPrivateAssignment();
        IsOccupied = false;
        IsReady = false;
        IsLocalPlayer = false;
        IsHost = false;
        NetworkClientId = ulong.MaxValue;

        if (playerNameText != null) playerNameText.text = "빈 자리";
        if (statusText != null) statusText.text = "플레이어 모집 중";
        RefreshReadyControls();
        gameObject.SetActive(true);
    }

    public void SetPlayer(int slotNumber, string playerName, string status = "대기 중", bool isLocalPlayer = false, bool isHost = false, bool isReady = false, ulong networkClientId = ulong.MaxValue)
    {
        IsOccupied = true;
        IsLocalPlayer = isLocalPlayer;
        IsHost = isHost;
        IsReady = isReady;
        NetworkClientId = networkClientId;
        if (!isLocalPlayer) ClearPrivateAssignment();

        if (playerNameText != null) playerNameText.text = playerName;
        if (statusText != null) statusText.text = status;
        // Keep the prefab's authored background and accent colors for every player.
        RefreshReadyControls();
        gameObject.SetActive(true);

        var localObject = Unity.Netcode.NetworkManager.Singleton?.LocalClient?.PlayerObject;
        if (isLocalPlayer && localObject != null && localObject.TryGetComponent(out LiarPainterNetworkPlayer localPlayer) && localPlayer.HasLocalAssignment)
            ShowPrivateAssignment(localPlayer.LocalAssignment);
    }

    public void SetReadyState(bool isReady)
    {
        IsReady = isReady;
        if (statusText != null) statusText.text = isReady ? "<color=#00DB3B>준비완료</color>" : "대기 중";
        RefreshReadyControls();
    }

    public void SetStartButtonInteractable(bool interactable)
    {
        if (startGameButton != null) startGameButton.interactable = interactable;
    }

    public void HandleReadyClicked()
    {
        ReadyButtonClicked?.Invoke(this);
    }

    public void HandleStartClicked()
    {
        StartGameButtonClicked?.Invoke(this);
    }

    private void RefreshReadyControls()
    {
        RefreshStatusVisibility();
        if (readyButton != null) readyButton.gameObject.SetActive(!hasPrivateAssignment && !voting && IsOccupied && IsLocalPlayer);
        if (readyButtonText != null) readyButtonText.text = IsReady ? "준비취소" : "준비하기";
        if (startGameButton != null) startGameButton.gameObject.SetActive(!hasPrivateAssignment && !voting && IsOccupied && IsLocalPlayer && IsHost);
    }
}
