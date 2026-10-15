using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class RoomNetworkUIController : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "GameScene";
    private GameObject roomBrowserPanel, createRoomPopup, passwordRow, roomItemTemplate, emptyRoomMessage, loadingPanel;
    private TMP_InputField roomNameSearchInput, roomNameInput, joinCodeInput;
    private TMP_Dropdown privacyDropdown;
    [SerializeField] private Toggle publicRoomToggle;
    [SerializeField] private Toggle privateRoomToggle;
    private TMP_Text loadingText, joinCodeError;
    private Transform roomListContent;
    private GameObject joinCodePopup;
    private ISessionInfo codeSelectedRoom;
    private bool refreshingRooms;
    private GameObject roomEntryNotice;
    private TMP_Text roomEntryMessage;
    private readonly List<ISessionInfo> cachedRooms = new List<ISessionInfo>();

    private void Awake()
    {
        Transform canvas = transform;
        roomBrowserPanel = canvas.Find("RoomBrowserPanel")?.gameObject;
        createRoomPopup = canvas.Find("CreateRoomPopup")?.gameObject;
        roomNameSearchInput = canvas.Find("RoomBrowserPanel/SearchArea/SearchInputField")?.GetComponent<TMP_InputField>();
        roomNameInput = canvas.Find("CreateRoomPopup/PopupWindow/RoomNameRow/InputField")?.GetComponent<TMP_InputField>();
        if (roomNameInput != null) roomNameInput.characterLimit = LiarPainterSessionManager.MaximumRoomNameLength;
        canvas.Find("CreateRoomPopup/PopupWindow/MaxPlayersRow")?.gameObject.SetActive(false);
        privacyDropdown = canvas.Find("CreateRoomPopup/PopupWindow/PrivateRoomRow/Dropdown")?.GetComponent<TMP_Dropdown>();
        passwordRow = canvas.Find("CreateRoomPopup/PopupWindow/PasswordRow")?.gameObject;
        roomListContent = canvas.Find("RoomBrowserPanel/RoomListScrollView/Viewport/Content");
        emptyRoomMessage = canvas.Find("RoomBrowserPanel/EmptyRoomMessage")?.gameObject;
        loadingPanel = canvas.Find("RoomBrowserPanel/LoadingPanel")?.gameObject;
        loadingText = canvas.Find("RoomBrowserPanel/LoadingPanel/LoadingText")?.GetComponent<TMP_Text>();

        if (roomBrowserPanel != null) roomBrowserPanel.SetActive(false);
        if (createRoomPopup != null) createRoomPopup.SetActive(false);

        PrepareRoomItemTemplate();
        ConfigurePrivacyToggles();
        ConnectJoinCodePopup();
        roomEntryNotice = canvas.Find("RoomEntryNoticePopup")?.gameObject;
        roomEntryMessage = canvas.Find("RoomEntryNoticePopup/PopupWindow/Guide")?.GetComponent<TMP_Text>();
        if (roomEntryNotice != null) roomEntryNotice.SetActive(false);
        ConfigureRoomNameSearch(canvas);
        if (publicRoomToggle == null && privacyDropdown != null)
        {
            privacyDropdown.onValueChanged.AddListener(HandlePrivacyChanged);
            HandlePrivacyChanged(privacyDropdown.value);
        }
        if (roomBrowserPanel != null) roomBrowserPanel.SetActive(false);
        if (createRoomPopup != null) createRoomPopup.SetActive(false);
    }

    public void CloseRoomBrowser()
    {
        if (roomBrowserPanel != null) roomBrowserPanel.SetActive(false);
    }

    public void ShowRoomBrowser()
    {
        if (roomBrowserPanel == null) return;
        roomBrowserPanel.SetActive(true);
        RefreshRooms();
    }

    private static Transform FindChildByName(Transform parent, string objectName)
    {
        if (parent == null) return null;
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            if (child.name == objectName) return child;
        return null;
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void PrepareRoomItemTemplate()
    {
        if (roomListContent == null || roomListContent.childCount == 0) return;
        roomItemTemplate = Instantiate(roomListContent.GetChild(0).gameObject, transform);
        roomItemTemplate.name = "RoomListItemTemplate";
        roomItemTemplate.SetActive(false);
        for (int i = roomListContent.childCount - 1; i >= 0; i--) Destroy(roomListContent.GetChild(i).gameObject);
    }

    private void HandlePrivacyChanged(int value)
    {
        if (passwordRow != null) passwordRow.SetActive(false);
    }

    private void ConfigurePrivacyToggles()
    {
        Transform row = transform.Find("CreateRoomPopup/PopupWindow/PrivateRoomRow");
        if (row == null) return;
        if (publicRoomToggle == null) publicRoomToggle = row.Find("PublicRoom")?.GetComponent<Toggle>();
        if (privateRoomToggle == null) privateRoomToggle = row.Find("PrivateRoom")?.GetComponent<Toggle>();
        if (publicRoomToggle == null || privateRoomToggle == null)
        {
            ConfigurePrivacyDropdown();
            return;
        }
        ToggleGroup group = row.GetComponent<ToggleGroup>();
        if (group == null) group = row.gameObject.AddComponent<ToggleGroup>();
        group.allowSwitchOff = false;
        publicRoomToggle.group = group;
        privateRoomToggle.group = group;
        privateRoomToggle.SetIsOnWithoutNotify(false);
        publicRoomToggle.SetIsOnWithoutNotify(true);
        HandlePrivacyChanged(0);
    }

    private void ConfigurePrivacyDropdown()
    {
        if (privacyDropdown == null) return;
        int selected = Mathf.Clamp(privacyDropdown.value, 0, 1);
        privacyDropdown.ClearOptions();
        privacyDropdown.AddOptions(new List<string> { "공개 방", "비공개 방" });
        privacyDropdown.SetValueWithoutNotify(selected);
        privacyDropdown.interactable = true;
        Image background = privacyDropdown.GetComponent<Image>();
        if (background != null)
        {
            background.raycastTarget = true;
            privacyDropdown.targetGraphic = background;
        }
        if (privacyDropdown.captionText != null) privacyDropdown.captionText.raycastTarget = false;

        RectTransform template = privacyDropdown.template;
        if (template == null) return;
        Toggle item = template.GetComponentInChildren<Toggle>(true);
        ScrollRect scroll = template.GetComponent<ScrollRect>();
        if (scroll != null)
        {
            scroll.horizontal = false;
            scroll.vertical = true;
            if (item != null) scroll.content = item.transform.parent as RectTransform;
        }
        template.gameObject.SetActive(false);
        privacyDropdown.RefreshShownValue();
        ConfigureExistingPrivacyMenu(template);
    }

    private void ConfigureExistingPrivacyMenu(RectTransform template)
    {
        // TMP_Dropdown.Show creates a separate Dropdown List. Use the original template instead.
        privacyDropdown.Hide();
        Transform generated = privacyDropdown.transform.Find("Dropdown List");
        if (generated != null)
        {
            generated.gameObject.SetActive(false);
            Destroy(generated.gameObject);
        }
        privacyDropdown.enabled = false;
        BindPointerClick(privacyDropdown.gameObject, () =>
        {
            bool show = !template.gameObject.activeSelf;
            if (show) privacyDropdown.transform.parent.SetAsLastSibling();
            template.gameObject.SetActive(show);
        });
        Transform publicItem = FindChildByName(template, "Item");
        Transform privateItem = FindChildByName(template, "Item (1)")
            ?? FindChildByName(template, "Item(1)");
        if (publicItem == null || privateItem == null)
        {
            Debug.LogWarning("PrivateRoomRow의 Template에서 기존 Item과 Item (1)을 찾을 수 없습니다. 두 항목을 확인해 주세요.");
            return;
        }
        ConfigurePrivacyItem(publicItem, 0, "공개 방", template);
        ConfigurePrivacyItem(privateItem, 1, "비공개 방", template);
        ScrollRect scroll = template.GetComponent<ScrollRect>();
        if (scroll != null) scroll.enabled = false;
        CanvasGroup group = template.GetComponent<CanvasGroup>();
        if (group != null)
        {
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }
        template.gameObject.SetActive(false);
    }

    private void ConfigurePrivacyItem(Transform item, int value, string label, RectTransform template)
    {
        Toggle toggle = item.GetComponent<Toggle>();
        if (toggle != null) toggle.enabled = false;
        item.gameObject.SetActive(true);
        foreach (UnityEngine.UI.Graphic graphic in item.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
            graphic.raycastTarget = !(graphic is TMP_Text);
        TMP_Text text = item.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.text = label;
        BindPointerClick(item.gameObject, () =>
        {
            privacyDropdown.SetValueWithoutNotify(value);
            privacyDropdown.RefreshShownValue();
            HandlePrivacyChanged(value);
            template.gameObject.SetActive(false);
        });
    }

    private static void BindPointerClick(GameObject target, Action action)
    {
        // Dropdown, Toggle and Button inherit Selectable, which disallows a second
        // Selectable on the same object. Handle clicks without adding a Button.
        UnityEngine.EventSystems.EventTrigger trigger = target.GetComponent<UnityEngine.EventSystems.EventTrigger>();
        if (trigger == null) trigger = target.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        trigger.triggers.RemoveAll(entry => entry.eventID == UnityEngine.EventSystems.EventTriggerType.PointerClick);
        UnityEngine.EventSystems.EventTrigger.Entry click = new UnityEngine.EventSystems.EventTrigger.Entry
        {
            eventID = UnityEngine.EventSystems.EventTriggerType.PointerClick
        };
        click.callback.AddListener(data =>
        {
            if (data is UnityEngine.EventSystems.PointerEventData pointer &&
                pointer.button == UnityEngine.EventSystems.PointerEventData.InputButton.Left)
                action();
        });
        trigger.triggers.Add(click);
    }

    public void ShowCreatePopup()
    {
        if (createRoomPopup != null) createRoomPopup.SetActive(true);
        HandlePrivacyChanged(privacyDropdown != null ? privacyDropdown.value : 0);
    }

    public void HideCreatePopup() { if (createRoomPopup != null) createRoomPopup.SetActive(false); }

    public async void CreateRoom()
    {
        LiarPainterSessionManager sessions = LiarPainterSessionManager.Instance;
        if (sessions == null || sessions.IsBusy) return;
        string roomName = roomNameInput != null ? roomNameInput.text.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(roomName) || roomName.Length > LiarPainterSessionManager.MaximumRoomNameLength)
        {
            SetLoading(true, string.IsNullOrWhiteSpace(roomName) ? "방 제목을 입력하세요." : "방 제목은 최대 10글자까지 입력할 수 있습니다.");
            if (roomNameInput != null) roomNameInput.ActivateInputField();
            return;
        }
        bool isPrivate = privateRoomToggle != null
            ? privateRoomToggle.isOn
            : privacyDropdown != null && privacyDropdown.value == 1;
        SetLoading(true, "방을 생성하는 중...");
        bool created = await sessions.CreateRoomAsync(roomName, isPrivate);
        if (created) { HideCreatePopup(); await EnterGameSceneAsHostAsync(); }
        else SetLoading(true, "방 생성에 실패했습니다.");
    }

    public async void RefreshRooms()
    {
        LiarPainterSessionManager sessions = LiarPainterSessionManager.Instance;
        if (refreshingRooms || sessions == null || roomListContent == null || roomItemTemplate == null) return;
        refreshingRooms = true;
        try
        {
            SetLoading(true, "방 목록을 불러오는 중...");
            IList<ISessionInfo> rooms = await sessions.QueryRoomsAsync();
            if (this == null) return;
            cachedRooms.Clear();
            cachedRooms.AddRange(rooms);
            ApplyRoomNameFilter();
            SetLoading(false, string.Empty);
            if (sessions.LastRoomQueryFailed && cachedRooms.Count == 0 && emptyRoomMessage != null)
            {
                TMP_Text message = emptyRoomMessage.GetComponentInChildren<TMP_Text>(true);
                if (message != null) message.text = "방 목록 조회에 실패했습니다. 잠시 후 새로고침해 주세요.";
            }
            else if (emptyRoomMessage != null)
            {
                TMP_Text message = emptyRoomMessage.GetComponentInChildren<TMP_Text>(true);
                if (message != null) message.text = "생성된 방이 없습니다.";
            }
        }
        finally
        {
            refreshingRooms = false;
        }
    }

    private void ConfigureRoomNameSearch(Transform canvas)
    {
        if (roomNameSearchInput != null)
        {
            roomNameSearchInput.onValueChanged.RemoveAllListeners();
            roomNameSearchInput.onValueChanged.AddListener(_ => ApplyRoomNameFilter());
            if (roomNameSearchInput.placeholder is TMP_Text placeholder)
                placeholder.text = "방 이름 검색";
        }

        TMP_Text searchButtonText = canvas.Find("RoomBrowserPanel/Footer/QuickJoinButton/ButtonText")?.GetComponent<TMP_Text>();
        if (searchButtonText != null) searchButtonText.text = "코드로 입장";
    }

    private void ApplyRoomNameFilter()
    {
        string query = roomNameSearchInput != null ? roomNameSearchInput.text.Trim() : string.Empty;
        List<ISessionInfo> filteredRooms = cachedRooms.FindAll(room =>
            string.IsNullOrEmpty(query) ||
            (!string.IsNullOrEmpty(room.Name) && room.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0));
        RenderRooms(filteredRooms);
    }

    private void RenderRooms(IList<ISessionInfo> rooms)
    {
        if (roomListContent == null || roomItemTemplate == null) return;
        for (int i = roomListContent.childCount - 1; i >= 0; i--) Destroy(roomListContent.GetChild(i).gameObject);
        foreach (ISessionInfo room in rooms)
        {
            GameObject item = Instantiate(roomItemTemplate, roomListContent);

            item.name = "Room_" + room.Id;
            item.SetActive(true);

            SetChildText(item.transform, "RoomNameText", room.Name);
            SetChildText(item.transform, "HostNameText", LiarPainterSessionManager.GetHostNickname(room));
            SetChildText(item.transform, "PlayerCountText", $"{room.MaxPlayers - room.AvailableSlots} / {room.MaxPlayers}");
            TMP_Text status = item.transform.Find("StatusText")?.GetComponent<TMP_Text>();
            if (status != null)
            {
                status.richText = true;
                status.text = room.IsLocked ? "<color=#FF4444>게임 중</color>"
                    : room.AvailableSlots <= 0 ? "정원 마감" : "대기 중";
            }
            SetChildText(item.transform, "PrivacyText", LiarPainterSessionManager.RequiresJoinCode(room) ? "비공개" : "공개");

            Button join = item.transform.Find("JoinButton")?.GetComponent<Button>();

            if (join != null)
            {
                join.interactable = true;
            }
            item.GetComponent<RoomListItemAction>()?.Configure(this, room);
        }
        if (emptyRoomMessage != null) emptyRoomMessage.SetActive(rooms.Count == 0);
    }

    private static void SetChildText(Transform root, string name, string value)
    {
        TMP_Text label = root.Find(name)?.GetComponent<TMP_Text>();
        if (label != null) label.text = value;
    }

    public void SelectRoom(ISessionInfo room) => JoinSelectedRoom(room);

    private async void JoinSelectedRoom(ISessionInfo room)
    {
        LiarPainterSessionManager sessions = LiarPainterSessionManager.Instance;
        if (room == null || sessions == null || sessions.IsBusy) return;
        string entryError = LiarPainterSessionManager.GetRoomEntryError(room);
        if (!string.IsNullOrEmpty(entryError)) { ShowRoomEntryNotice(entryError); return; }
        if (LiarPainterSessionManager.RequiresJoinCode(room))
        {
            ShowJoinCodePopup();
            codeSelectedRoom = room;
            return;
        }
        SetLoading(true, "방에 참가하는 중...");
        if (!await sessions.JoinRoomByIdAsync(room.Id)) ShowRoomEntryNotice(sessions.LastJoinError);
    }

    public async void SubmitJoinCode()
    {
        LiarPainterSessionManager sessions = LiarPainterSessionManager.Instance;
        if (sessions == null || sessions.IsBusy) return;
        string code = joinCodeInput != null ? joinCodeInput.text.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(code))
        {
            if (joinCodeError != null) joinCodeError.text = "참가 코드를 입력하세요.";
            return;
        }
        if (joinCodeError != null) joinCodeError.text = "입장하는 중...";
        if (codeSelectedRoom != null && !LiarPainterSessionManager.MatchesJoinCode(codeSelectedRoom, code))
        {
            if (joinCodeError != null) joinCodeError.text = "선택한 방의 참가 코드와 일치하지 않습니다.";
            return;
        }
        bool joined = await sessions.JoinRoomByCodeAsync(code);
        if (joined) CloseCodePopup();
        else if (joinCodeError != null) joinCodeError.text = sessions.LastJoinError;
    }

    private void ShowRoomEntryNotice(string message)
    {
        SetLoading(false, string.Empty);
        if (roomEntryMessage != null) roomEntryMessage.text = message;
        if (roomEntryNotice == null) { Debug.LogWarning(message); return; }
        roomEntryNotice.transform.SetAsLastSibling();
        roomEntryNotice.SetActive(true);
    }

    public void CloseRoomEntryNotice()
    {
        if (roomEntryNotice != null) roomEntryNotice.SetActive(false);
    }

    public void CloseCodePopup()
    {
        codeSelectedRoom = null;
        if (joinCodePopup != null) joinCodePopup.SetActive(false);
    }

    public void ShowJoinCodePopup()
    {
        codeSelectedRoom = null;
        if (joinCodeInput != null) joinCodeInput.text = string.Empty;
        if (joinCodeError != null) joinCodeError.text = string.Empty;
        if (joinCodePopup != null)
        {
            joinCodePopup.transform.SetAsLastSibling();
            joinCodePopup.SetActive(true);
        }
        joinCodeInput?.ActivateInputField();
    }

    private async Task EnterGameSceneAsHostAsync()
    {
        NetworkManager manager = NetworkManager.Singleton;
        for (int i = 0; i < 120 && (manager == null || !manager.IsListening); i++) { await Task.Delay(50); manager = NetworkManager.Singleton; }
        if (manager != null && manager.IsServer && manager.IsListening) manager.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
        else SetLoading(true, "네트워크 시작을 확인하지 못했습니다.");
    }

    public async void LeaveOrCloseBrowser()
    {
        LiarPainterSessionManager sessions = LiarPainterSessionManager.Instance;
        if (sessions != null && sessions.CurrentSession != null) await sessions.LeaveRoomAsync();
        else if (roomBrowserPanel != null) roomBrowserPanel.SetActive(false);
    }

    private void SetLoading(bool visible, string message)
    {
        if (loadingText != null) loadingText.text = message;
        if (loadingPanel != null) loadingPanel.SetActive(visible);
    }



    private void ConnectJoinCodePopup()
    {
        joinCodePopup = transform.Find("JoinCodePopup")?.gameObject;
        if (joinCodePopup == null)
        {
            Debug.LogError("MainScene Canvas에 저장된 JoinCodePopup이 없습니다.");
            return;
        }
        Transform window = joinCodePopup.transform.Find("PopupWindow");
        joinCodeInput = window?.Find("CodeInput")?.GetComponent<TMP_InputField>();
        joinCodeError = window?.Find("Error")?.GetComponent<TMP_Text>();
        if (joinCodeInput != null)
        {
            joinCodeInput.onSubmit.RemoveAllListeners();
            joinCodeInput.onSubmit.AddListener(_ => SubmitJoinCode());
        }
        joinCodePopup.SetActive(false);
    }
}
