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
    private TMP_InputField roomNameSearchInput, roomNameInput, createPasswordInput, joinPasswordInput;
    private TMP_Dropdown maxPlayersDropdown, privacyDropdown;
    private TMP_Text loadingText, joinPasswordError;
    private Transform roomListContent;
    private GameObject joinPasswordPopup;
    private ISessionInfo pendingPasswordRoom;
    private readonly List<ISessionInfo> cachedRooms = new List<ISessionInfo>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSceneHook()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AttachToRoomBrowser()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "MainScene") return;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                if (canvas.transform.Find("RoomBrowserPanel") == null) continue;
                if (canvas.GetComponent<RoomNetworkUIController>() == null)
                    canvas.gameObject.AddComponent<RoomNetworkUIController>();
                return;
            }
        }
    }

    private void Awake()
    {
        Transform canvas = transform;
        roomBrowserPanel = canvas.Find("RoomBrowserPanel")?.gameObject;
        createRoomPopup = canvas.Find("CreateRoomPopup")?.gameObject;
        roomNameSearchInput = canvas.Find("RoomBrowserPanel/SearchArea/SearchInputField")?.GetComponent<TMP_InputField>();
        roomNameInput = canvas.Find("CreateRoomPopup/PopupWindow/RoomNameRow/InputField")?.GetComponent<TMP_InputField>();
        createPasswordInput = canvas.Find("CreateRoomPopup/PopupWindow/PasswordRow/InputField")?.GetComponent<TMP_InputField>();
        maxPlayersDropdown = canvas.Find("CreateRoomPopup/PopupWindow/MaxPlayersRow/Dropdown")?.GetComponent<TMP_Dropdown>();
        privacyDropdown = canvas.Find("CreateRoomPopup/PopupWindow/PrivateRoomRow/Dropdown")?.GetComponent<TMP_Dropdown>();
        passwordRow = canvas.Find("CreateRoomPopup/PopupWindow/PasswordRow")?.gameObject;
        roomListContent = canvas.Find("RoomBrowserPanel/RoomListScrollView/Viewport/Content");
        emptyRoomMessage = canvas.Find("RoomBrowserPanel/EmptyRoomMessage")?.gameObject;
        loadingPanel = canvas.Find("RoomBrowserPanel/LoadingPanel")?.gameObject;
        loadingText = canvas.Find("RoomBrowserPanel/LoadingPanel/LoadingText")?.GetComponent<TMP_Text>();

        Transform mainPanel = canvas.Find("MainPanel");
        Bind(FindChildByName(mainPanel, "Start_btn"), ShowRoomBrowser);
        Bind(FindChildByName(mainPanel, "Finish_btn"), QuitGame);
        if (roomBrowserPanel != null) roomBrowserPanel.SetActive(false);
        if (createRoomPopup != null) createRoomPopup.SetActive(false);

        PrepareRoomItemTemplate();
        ConfigurePinInput(createPasswordInput);
        ConfigurePrivacyDropdown();
        BuildJoinPasswordPopup();
        Bind(canvas.Find("RoomBrowserPanel/SearchArea/CreateRoomButton"), ShowCreatePopup);
        Bind(canvas.Find("RoomBrowserPanel/SearchArea/RefreshButton"), RefreshRooms);
        Bind(canvas.Find("CreateRoomPopup/PopupWindow/CancelButton"), HideCreatePopup);
        Bind(canvas.Find("CreateRoomPopup/PopupWindow/CreateButton"), CreateRoom);
        Bind(canvas.Find("RoomBrowserPanel/Footer/QuickJoinButton"), ApplyRoomNameFilter);
        Bind(canvas.Find("RoomBrowserPanel/Header/BackButton"), LeaveOrCloseBrowser);
        ConfigureRoomNameSearch(canvas);
        if (privacyDropdown != null)
        {
            privacyDropdown.onValueChanged.AddListener(HandlePrivacyChanged);
            HandlePrivacyChanged(privacyDropdown.value);
        }
        if (roomBrowserPanel != null) roomBrowserPanel.SetActive(false);
        if (createRoomPopup != null) createRoomPopup.SetActive(false);
    }

    private void ShowRoomBrowser()
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

    private static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static void Bind(Transform target, Action action)
    {
        Button button = target != null ? target.GetComponent<Button>() : null;
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => action());
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
        bool protectedRoom = value == 1;
        if (passwordRow != null) passwordRow.SetActive(protectedRoom);
        if (!protectedRoom && createPasswordInput != null) createPasswordInput.text = string.Empty;
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

    private void ShowCreatePopup()
    {
        if (createRoomPopup != null) createRoomPopup.SetActive(true);
        HandlePrivacyChanged(privacyDropdown != null ? privacyDropdown.value : 0);
    }

    private void HideCreatePopup() { if (createRoomPopup != null) createRoomPopup.SetActive(false); }

    private async void CreateRoom()
    {
        LiarPainterSessionManager sessions = LiarPainterSessionManager.Instance;
        if (sessions == null) return;
        bool passwordProtected = privacyDropdown != null && privacyDropdown.value == 1;
        string pin = createPasswordInput != null ? createPasswordInput.text.Trim() : string.Empty;
        if (passwordProtected && !LiarPainterSessionManager.IsValidFourDigitPin(pin))
        {
            SetLoading(true, "비밀번호는 숫자 4자리로 입력하세요.");
            return;
        }
        SetLoading(true, "방을 생성하는 중...");
        int maxPlayers = maxPlayersDropdown != null ? Mathf.Clamp(4 + maxPlayersDropdown.value, 4, 8) : 8;
        bool created = await sessions.CreateRoomAsync(roomNameInput != null ? roomNameInput.text : string.Empty,
            maxPlayers, passwordProtected, pin);
        if (created) { HideCreatePopup(); await EnterGameSceneAsHostAsync(); }
        else SetLoading(true, "방 생성에 실패했습니다.");
    }

    private async void RefreshRooms()
    {
        LiarPainterSessionManager sessions = LiarPainterSessionManager.Instance;
        if (sessions == null || roomListContent == null || roomItemTemplate == null) return;
        SetLoading(true, "방 목록을 불러오는 중...");
        IList<ISessionInfo> rooms = await sessions.QueryRoomsAsync();
        cachedRooms.Clear();
        cachedRooms.AddRange(rooms);
        ApplyRoomNameFilter();
        SetLoading(false, string.Empty);
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
        if (searchButtonText != null) searchButtonText.text = "검색";
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
            SetChildText(item.transform, "HostNameText", "방장");
            SetChildText(item.transform, "PlayerCountText", $"{room.MaxPlayers - room.AvailableSlots} / {room.MaxPlayers}");
            SetChildText(item.transform, "StatusText", room.IsLocked ? "잠김" : "대기 중");
            SetChildText(item.transform, "PrivacyText", room.HasPassword ? "비밀번호" : "공개");
            Button join = item.transform.Find("JoinButton")?.GetComponent<Button>();
            if (join != null)
            {
                ISessionInfo captured = room;
                join.interactable = !room.IsLocked && room.AvailableSlots > 0;
                join.onClick.RemoveAllListeners();
                join.onClick.AddListener(() => SelectRoom(captured));
            }
        }
        if (emptyRoomMessage != null) emptyRoomMessage.SetActive(rooms.Count == 0);
    }

    private static void SetChildText(Transform root, string name, string value)
    {
        TMP_Text label = root.Find(name)?.GetComponent<TMP_Text>();
        if (label != null) label.text = value;
    }

    private void SelectRoom(ISessionInfo room)
    {
        if (!room.HasPassword) { JoinSelectedRoom(room, string.Empty); return; }
        pendingPasswordRoom = room;
        if (joinPasswordInput != null) joinPasswordInput.text = string.Empty;
        if (joinPasswordError != null) joinPasswordError.text = string.Empty;
        if (joinPasswordPopup != null) joinPasswordPopup.SetActive(true);
        joinPasswordInput?.ActivateInputField();
    }

    private async void JoinSelectedRoom(ISessionInfo room, string pin)
    {
        if (room == null || LiarPainterSessionManager.Instance == null) return;
        SetLoading(true, "방에 참가하는 중...");
        bool joined = await LiarPainterSessionManager.Instance.JoinRoomByIdAsync(room.Id, pin);
        if (joined) return;
        SetLoading(false, string.Empty);
        if (room.HasPassword)
        {
            if (joinPasswordPopup != null) joinPasswordPopup.SetActive(true);
            if (joinPasswordError != null) joinPasswordError.text = "비밀번호가 올바르지 않거나 입장할 수 없습니다.";
        }
        else SetLoading(true, "방 참가에 실패했습니다.");
    }

    private void SubmitRoomPassword()
    {
        string pin = joinPasswordInput != null ? joinPasswordInput.text.Trim() : string.Empty;
        if (!LiarPainterSessionManager.IsValidFourDigitPin(pin))
        {
            if (joinPasswordError != null) joinPasswordError.text = "숫자 4자리를 입력하세요.";
            return;
        }
        ISessionInfo room = pendingPasswordRoom;
        if (joinPasswordPopup != null) joinPasswordPopup.SetActive(false);
        JoinSelectedRoom(room, pin);
    }

    private void ClosePasswordPopup()
    {
        pendingPasswordRoom = null;
        if (joinPasswordPopup != null) joinPasswordPopup.SetActive(false);
    }

    private async Task EnterGameSceneAsHostAsync()
    {
        NetworkManager manager = NetworkManager.Singleton;
        for (int i = 0; i < 120 && (manager == null || !manager.IsListening); i++) { await Task.Delay(50); manager = NetworkManager.Singleton; }
        if (manager != null && manager.IsServer && manager.IsListening) manager.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
        else SetLoading(true, "네트워크 시작을 확인하지 못했습니다.");
    }

    private async void LeaveOrCloseBrowser()
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

    private static void ConfigurePinInput(TMP_InputField input)
    {
        if (input == null) return;
        input.characterLimit = 4;
        input.contentType = TMP_InputField.ContentType.Pin;
        input.characterValidation = TMP_InputField.CharacterValidation.Digit;
        input.keyboardType = TouchScreenKeyboardType.NumberPad;
    }

    private void BuildJoinPasswordPopup()
    {
        TMP_Text style = GetComponentInChildren<TMP_Text>(true);
        joinPasswordPopup = Ui("JoinPasswordPopup", transform, typeof(Image));
        Stretch((RectTransform)joinPasswordPopup.transform);
        joinPasswordPopup.GetComponent<Image>().color = new Color(0f, 0f, 0f, .72f);
        GameObject window = Ui("PopupWindow", joinPasswordPopup.transform, typeof(Image));
        RectTransform wr = (RectTransform)window.transform;
        wr.anchorMin = wr.anchorMax = new Vector2(.5f, .5f); wr.sizeDelta = new Vector2(520, 330); wr.anchoredPosition = Vector2.zero;
        window.GetComponent<Image>().color = new Color32(28, 36, 48, 255);
        Label("Title", window.transform, "비밀번호 입력", 30, new Vector2(0, 108), new Vector2(440, 50), style);
        Label("Guide", window.transform, "숫자 4자리 비밀번호를 입력하세요.", 18, new Vector2(0, 57), new Vector2(440, 38), style);
        GameObject inputObject = Ui("PasswordInput", window.transform, typeof(Image), typeof(TMP_InputField));
        RectTransform ir = (RectTransform)inputObject.transform;
        ir.anchorMin = ir.anchorMax = new Vector2(.5f, .5f); ir.sizeDelta = new Vector2(320, 58); ir.anchoredPosition = new Vector2(0, 5);
        inputObject.GetComponent<Image>().color = new Color32(16, 22, 30, 255);
        joinPasswordInput = inputObject.GetComponent<TMP_InputField>();
        joinPasswordInput.textComponent = Label("Text", inputObject.transform, "", 27, Vector2.zero, new Vector2(280, 46), style);
        ConfigurePinInput(joinPasswordInput);
        joinPasswordError = Label("Error", window.transform, "", 16, new Vector2(0, -42), new Vector2(440, 35), style);
        joinPasswordError.color = new Color32(255, 120, 110, 255);
        PopupButton("CancelButton", window.transform, "취소", new Vector2(-105, -112), new Color32(74, 82, 94, 255), ClosePasswordPopup, style);
        PopupButton("EnterButton", window.transform, "입장", new Vector2(105, -112), new Color32(57, 124, 214, 255), SubmitRoomPassword, style);
        joinPasswordPopup.SetActive(false);
    }

    private static GameObject Ui(string name, Transform parent, params Type[] components)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        foreach (Type type in components) result.AddComponent(type);
        return result;
    }

    private static TMP_Text Label(string name, Transform parent, string value, float size, Vector2 position, Vector2 dimensions, TMP_Text style)
    {
        TextMeshProUGUI label = Ui(name, parent, typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        RectTransform rect = (RectTransform)label.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.sizeDelta = dimensions; rect.anchoredPosition = position;
        label.text = value; label.fontSize = size; label.alignment = TextAlignmentOptions.Center; label.color = Color.white;
        if (style != null) label.font = style.font;
        return label;
    }

    private static void PopupButton(string name, Transform parent, string caption, Vector2 position, Color color, Action action, TMP_Text style)
    {
        GameObject result = Ui(name, parent, typeof(Image), typeof(Button));
        RectTransform rect = (RectTransform)result.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(170, 58); rect.anchoredPosition = position;
        result.GetComponent<Image>().color = color;
        result.GetComponent<Button>().onClick.AddListener(() => action());
        Label("Text", result.transform, caption, 21, Vector2.zero, rect.sizeDelta, style);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
    }
}
