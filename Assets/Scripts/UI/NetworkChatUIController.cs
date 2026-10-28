using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public sealed class NetworkChatUIController : MonoBehaviour
{
    [SerializeField] private TMP_InputField chatInput;
    [SerializeField] private TMP_Text historyText;
    [SerializeField] private UnityEngine.UI.ScrollRect historyScroll;
    [SerializeField] private GameObject chatPopup;
    [SerializeField] private TMP_Text latestMessageText;
    private readonly Queue<string> messages = new();
    private float nextSendTime;
    private MainMenuSettingsController settingsController;
    public static bool IsTyping { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetTyping() => IsTyping = false;

    private void Awake() => EnsureUI();
    private void OnEnable()
    {
        LiarPainterNetworkPlayer.ChatReceived += Receive;
        if (chatInput != null) chatInput.onSubmit.AddListener(Submit);
    }
    private void OnDisable()
    {
        LiarPainterNetworkPlayer.ChatReceived -= Receive;
        if (chatInput != null) chatInput.onSubmit.RemoveListener(Submit);
        IsTyping = false;
    }
    private void Update()
    {
        if (gameObject.scene.name == "GameScene")
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame) ToggleChat();
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ToggleSettings();
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Tab)) ToggleChat();
            if (Input.GetKeyDown(KeyCode.Escape)) ToggleSettings();
#endif
        }
        IsTyping = chatPopup != null && chatPopup.activeInHierarchy && chatInput != null && chatInput.isFocused;
    }

    private void ToggleSettings()
    {
        if (settingsController == null)
            settingsController = FindObjectsByType<MainMenuSettingsController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(controller => controller.gameObject.scene == gameObject.scene);
        if (settingsController != null) settingsController.ToggleSettings();
    }
    public void ToggleChat() => SetChatOpen(chatPopup != null && !chatPopup.activeSelf);
    public void CloseChat() => SetChatOpen(false);
    public void SetChatOpen(bool open)
    {
        if (chatPopup == null) return;
        if (!open) { chatInput?.DeactivateInputField(); IsTyping = false; }
        chatPopup.SetActive(open);
        if (open) { chatPopup.transform.SetAsLastSibling(); RefreshHistoryScroll(); chatInput?.ActivateInputField(); }
    }
    private void Submit(string _) => Send();
    public void Send()
    {
        if (chatInput == null || Time.unscaledTime < nextSendTime) return;
        var obj = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        if (obj == null || !obj.TryGetComponent(out LiarPainterNetworkPlayer player) || !player.SendChat(chatInput.text)) return;
        nextSendTime = Time.unscaledTime + .3f;
        chatInput.SetTextWithoutNotify("");
        chatInput.ActivateInputField();
    }
    private static readonly string[] NameColors =
    {
        "FF8080", "FFB366", "FFE066", "80D98C",
        "80C7FF", "B5A0FF", "FF99D6", "80E0DA"
    };

    public static string FormatMessage(int colorIndex, string name, string message)
    {
        string color = NameColors[Mathf.Clamp(colorIndex, 0, NameColors.Length - 1)];
        // Keep player-supplied text from injecting TMP formatting tags.
        string safeName = (name ?? string.Empty).Replace("<", "＜").Replace(">", "＞");
        string safeMessage = (message ?? string.Empty).Replace("<", "＜").Replace(">", "＞");
        return $"<color=#{color}>{safeName}</color>: {safeMessage}";
    }

    private void Receive(int colorIndex, string name, string message)
    {
        string formatted = FormatMessage(colorIndex, name, message);
        messages.Enqueue(formatted);
        if (latestMessageText != null)
        {
            latestMessageText.richText = true;
            latestMessageText.text = formatted;
        }
        while (messages.Count > 100) messages.Dequeue();
        if (historyText != null) { historyText.richText = true; historyText.text = string.Join("\n", messages); }
        RefreshHistoryScroll();
    }

    private void RefreshHistoryScroll()
    {
        if (historyScroll == null || !historyScroll.gameObject.activeInHierarchy) return;
        Canvas.ForceUpdateCanvases();
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(historyScroll.content);
        historyScroll.StopMovement();
        historyScroll.verticalNormalizedPosition = 0f;
    }

    public void EnsureUI()
    {
        // Bind authored scene UI only. Design and layout belong to the scene/prefab.
        if (latestMessageText == null)
            latestMessageText = transform.Find("Footer/LatestChatPreview/LatestChatText")?.GetComponent<TMP_Text>();
        if (latestMessageText != null) latestMessageText.text = messages.LastOrDefault() ?? string.Empty;
        if (chatPopup == null) chatPopup = transform.Find("ChatPopup")?.gameObject;
        if (chatPopup == null) return;
        var popup = chatPopup.transform;
        if (chatInput == null) chatInput = popup.Find("ChatInput")?.GetComponent<TMP_InputField>();
        if (historyScroll == null) historyScroll = popup.Find("ChatScrollView")?.GetComponent<UnityEngine.UI.ScrollRect>();
        if (historyText == null) historyText = popup.Find("ChatScrollView/Viewport/Content/ChatHistory")?.GetComponent<TMP_Text>();
        if (chatInput != null) { chatInput.characterLimit = 120; chatInput.richText = false; }
        if (historyText != null) historyText.text = string.Join("\n", messages);
        chatPopup.SetActive(false);
    }
}

