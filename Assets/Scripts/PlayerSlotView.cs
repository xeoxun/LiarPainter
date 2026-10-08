using TMPro;
using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerSlotView : MonoBehaviour
{
    [SerializeField] private TMP_Text numberText;
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Image background;
    [SerializeField] private Image stateLine;
    [SerializeField] private Button readyButton;
    [SerializeField] private TMP_Text readyButtonText;
    [SerializeField] private Button startGameButton;

    private static readonly Color32 EmptyBackground = new(27, 35, 46, 255);
    private static readonly Color32 OccupiedBackground = new(30, 77, 88, 255);
    private static readonly Color32 EmptyLine = new(49, 59, 72, 255);
    private static readonly Color32 OccupiedLine = new(38, 157, 178, 255);

    public bool IsOccupied { get; private set; }
    public bool IsReady { get; private set; }
    public bool IsLocalPlayer { get; private set; }
    public bool IsHost { get; private set; }
    public ulong NetworkClientId { get; private set; } = ulong.MaxValue;

    public event Action<PlayerSlotView> ReadyButtonClicked;
    public event Action<PlayerSlotView> StartGameButtonClicked;

    private void Awake()
    {
        BindButtonListeners();
    }

    private void OnDestroy()
    {
        if (readyButton != null) readyButton.onClick.RemoveListener(HandleReadyClicked);
        if (startGameButton != null) startGameButton.onClick.RemoveListener(HandleStartClicked);
    }

    public void Configure(TMP_Text number, TMP_Text playerName, TMP_Text status, Image slotBackground, Image line)
    {
        numberText = number;
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
        BindButtonListeners();
        RefreshReadyControls();
    }

    public void SetEmpty(int slotNumber)
    {
        IsOccupied = false;
        IsReady = false;
        IsLocalPlayer = false;
        IsHost = false;
        NetworkClientId = ulong.MaxValue;
        if (numberText != null) numberText.text = slotNumber.ToString();
        if (playerNameText != null) playerNameText.text = "빈 자리";
        if (statusText != null) statusText.text = "플레이어 대기 중";
        if (background != null) background.color = EmptyBackground;
        if (stateLine != null) stateLine.color = EmptyLine;
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
        if (numberText != null) numberText.text = slotNumber.ToString();
        if (playerNameText != null) playerNameText.text = playerName;
        if (statusText != null) statusText.text = status;
        if (background != null) background.color = isLocalPlayer ? OccupiedBackground : EmptyBackground;
        if (stateLine != null) stateLine.color = isLocalPlayer ? OccupiedLine : EmptyLine;
        RefreshReadyControls();
        gameObject.SetActive(true);
    }

    public void SetReadyState(bool isReady)
    {
        IsReady = isReady;
        if (statusText != null) statusText.text = isReady ? "준비 완료" : "대기 중";
        RefreshReadyControls();
    }

    public void SetStartButtonInteractable(bool interactable)
    {
        if (startGameButton != null) startGameButton.interactable = interactable;
    }

    private void BindButtonListeners()
    {
        if (readyButton != null)
        {
            readyButton.onClick.RemoveListener(HandleReadyClicked);
            readyButton.onClick.AddListener(HandleReadyClicked);
        }

        if (startGameButton != null)
        {
            startGameButton.onClick.RemoveListener(HandleStartClicked);
            startGameButton.onClick.AddListener(HandleStartClicked);
        }
    }

    private void HandleReadyClicked()
    {
        ReadyButtonClicked?.Invoke(this);
    }

    private void HandleStartClicked()
    {
        StartGameButtonClicked?.Invoke(this);
    }

    private void RefreshReadyControls()
    {
        if (readyButton != null) readyButton.gameObject.SetActive(IsOccupied && IsLocalPlayer);
        if (readyButtonText != null) readyButtonText.text = IsReady ? "준비 취소" : "준비하기";
        if (startGameButton != null) startGameButton.gameObject.SetActive(IsOccupied && IsLocalPlayer && IsHost);
    }
}
