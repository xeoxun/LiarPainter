using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[InitializeOnLoad]
public static class LiarPainterRoomUIBuilder
{
    private const string RootName = "RoomBrowserPanel";
    private const string FontPath = "Assets/Fonts/Griun_Oksooni-Rg SDF.asset";
    private const float FontSizeIncrease = 5f;

    private static TMP_FontAsset font;

    static LiarPainterRoomUIBuilder()
    {
        EditorApplication.delayCall += BuildOnceWhenEditorIsReady;
    }

    [MenuItem("Liar Painter/UI/Build Room Browser UI")]
    public static void BuildRoomBrowserUI()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("방 찾기 UI는 Edit Mode에서 생성해 주세요.");
            return;
        }

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            canvas = CreateCanvas();
        }

        if (canvas.transform.Find(RootName) != null)
        {
            Debug.Log("RoomBrowserPanel이 이미 존재하므로 중복 생성하지 않았습니다.");
            Selection.activeGameObject = canvas.transform.Find(RootName).gameObject;
            return;
        }

        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        GameObject roomBrowser = CreatePanel(RootName, canvas.transform, new Color32(18, 23, 31, 255));
        BuildHeader(roomBrowser.transform);
        BuildSearchArea(roomBrowser.transform);
        BuildRoomList(roomBrowser.transform);
        BuildFooter(roomBrowser.transform);
        BuildEmptyState(roomBrowser.transform);
        BuildLoadingPanel(roomBrowser.transform);

        GameObject popup = BuildCreateRoomPopup(canvas.transform);
        popup.SetActive(false);

        EnsureEventSystem();
        Canvas.ForceUpdateCanvases();
        RectTransform listContent = roomBrowser.transform.Find("RoomListScrollView/Viewport/Content") as RectTransform;
        if (listContent != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = roomBrowser;
        Debug.Log("Liar Painter 방 찾기 UI 생성 완료: RoomBrowserPanel과 CreateRoomPopup이 추가되었습니다.");
    }

    [MenuItem("Liar Painter/UI/Arrange Panels Side By Side")]
    public static void ArrangePanelsSideBySide()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("Canvas를 찾을 수 없습니다.");
            return;
        }

        Transform roomBrowser = canvas.transform.Find("RoomBrowserPanel");
        Transform createRoomPopup = canvas.transform.Find("CreateRoomPopup");
        Transform popupWindow = createRoomPopup != null ? createRoomPopup.Find("PopupWindow") : null;
        Transform blocker = createRoomPopup != null ? createRoomPopup.Find("Blocker") : null;

        if (roomBrowser == null || createRoomPopup == null || popupWindow == null)
        {
            Debug.LogWarning("RoomBrowserPanel 또는 CreateRoomPopup을 찾을 수 없습니다.");
            return;
        }

        RectTransform browserRect = roomBrowser.GetComponent<RectTransform>();
        browserRect.localScale = Vector3.one * 0.42f;
        browserRect.anchoredPosition = new Vector2(100f, 20f);

        createRoomPopup.gameObject.SetActive(true);
        if (blocker != null)
        {
            blocker.gameObject.SetActive(false);
        }

        RectTransform popupRect = popupWindow.GetComponent<RectTransform>();
        popupRect.localScale = Vector3.one * 0.55f;
        popupRect.anchoredPosition = new Vector2(700f, 20f);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = roomBrowser.gameObject;
        Debug.Log("MainPanel 옆에 RoomBrowserPanel과 CreateRoomPopup을 나란히 배치했습니다.");
    }

    [MenuItem("Liar Painter/UI/Arrange Room Browser With Overlay Popup")]
    public static void ArrangeRoomBrowserWithOverlayPopup()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("Canvas를 찾을 수 없습니다.");
            return;
        }

        Transform roomBrowser = canvas.transform.Find("RoomBrowserPanel");
        Transform createRoomPopup = canvas.transform.Find("CreateRoomPopup");
        Transform popupWindow = createRoomPopup != null ? createRoomPopup.Find("PopupWindow") : null;
        Transform blocker = createRoomPopup != null ? createRoomPopup.Find("Blocker") : null;

        if (roomBrowser == null || createRoomPopup == null || popupWindow == null)
        {
            Debug.LogWarning("RoomBrowserPanel 또는 CreateRoomPopup을 찾을 수 없습니다.");
            return;
        }

        RectTransform browserRect = roomBrowser.GetComponent<RectTransform>();
        browserRect.localScale = Vector3.one * 0.66f;
        browserRect.anchoredPosition = new Vector2(320f, 0f);
        roomBrowser.gameObject.SetActive(true);

        createRoomPopup.gameObject.SetActive(true);
        if (blocker != null)
        {
            blocker.gameObject.SetActive(false);
        }

        RectTransform popupRect = popupWindow.GetComponent<RectTransform>();
        popupRect.localScale = Vector3.one * 0.72f;
        popupRect.anchoredPosition = new Vector2(320f, 0f);

        createRoomPopup.SetAsLastSibling();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = popupWindow.gameObject;
        Debug.Log("RoomBrowserPanel을 오른쪽 공간에 확대하고 CreateRoomPopup을 그 위에 배치했습니다.");
    }

    [MenuItem("Liar Painter/UI/Build Settings Popup")]
    public static void BuildSettingsPopup()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("Canvas를 찾을 수 없습니다.");
            return;
        }

        Transform existing = canvas.transform.Find("SettingsPopup");
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            Debug.Log("SettingsPopup이 이미 존재하므로 중복 생성하지 않았습니다.");
            return;
        }

        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        GameObject root = CreateUIObject("SettingsPopup", canvas.transform);
        Stretch(root.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

        GameObject blocker = CreateImage("Blocker", root.transform, new Color32(0, 0, 0, 155));
        Stretch(blocker.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

        GameObject window = CreateImage("PopupWindow", root.transform, new Color32(28, 35, 45, 255));
        SetRect(window.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(320f, 0f), new Vector2(720f, 650f));

        CreateText("TitleText", window.transform, "설정", 38f, new Color32(242, 229, 202, 255), TextAlignmentOptions.Center, new Vector2(620f, 70f), new Vector2(360f, -58f));
        CreateText("ResolutionLabel", window.transform, "화면 해상도", 24f, Color.white, TextAlignmentOptions.MidlineLeft, new Vector2(220f, 60f), new Vector2(150f, -165f));
        TMP_Dropdown resolution = CreateDropdown("ResolutionDropdown", window.transform, new[] { "1920 × 1080", "1600 × 900", "1280 × 720" }, new Vector2(360f, 62f), new Vector2(470f, -165f));

        CreateText("SfxLabel", window.transform, "효과음", 24f, Color.white, TextAlignmentOptions.MidlineLeft, new Vector2(220f, 60f), new Vector2(150f, -280f));
        Slider sfxSlider = CreateSlider("SfxVolumeSlider", window.transform, new Vector2(280f, 30f), new Vector2(430f, -280f));
        TextMeshProUGUI sfxValue = CreateText("SfxValueText", window.transform, "80%", 24f, new Color32(120, 205, 218, 255), TextAlignmentOptions.Center, new Vector2(90f, 60f), new Vector2(625f, -280f));

        CreateText("LanguageLabel", window.transform, "언어", 24f, Color.white, TextAlignmentOptions.MidlineLeft, new Vector2(220f, 60f), new Vector2(150f, -395f));
        TMP_Dropdown language = CreateDropdown("LanguageDropdown", window.transform, new[] { "한국어", "English", "日本語" }, new Vector2(360f, 62f), new Vector2(470f, -395f));

        Button close = CreateButton("CloseButton", window.transform, "닫기", new Vector2(220f, 66f), new Vector2(210f, -570f), new Color32(75, 86, 100, 255), TextAlignmentOptions.Center);
        Button apply = CreateButton("ApplyButton", window.transform, "적용", new Vector2(300f, 66f), new Vector2(510f, -570f), new Color32(210, 155, 32, 255), TextAlignmentOptions.Center);

        Button settingsButton = canvas.transform.Find("MainPanel/Setting_btn")?.GetComponent<Button>();
        MainMenuSettingsController controller = canvas.GetComponent<MainMenuSettingsController>();
        if (controller == null)
        {
            controller = canvas.gameObject.AddComponent<MainMenuSettingsController>();
        }
        controller.Configure(root, settingsButton, close, apply, resolution, sfxSlider, sfxValue, language);

        root.SetActive(false);
        root.transform.SetAsLastSibling();
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = root;
        Debug.Log("설정 버튼과 연결된 SettingsPopup을 생성했습니다.");
    }

    [MenuItem("Liar Painter/UI/Build Nickname Popup")]
    public static void BuildNicknamePopup()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("Canvas를 찾을 수 없습니다.");
            return;
        }

        if (canvas.transform.Find("NicknamePopup") != null)
        {
            EnsureNicknameController(canvas);
            return;
        }

        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        GameObject root = CreateUIObject("NicknamePopup", canvas.transform);
        Stretch(root.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

        GameObject blocker = CreateImage("Blocker", root.transform, new Color32(3, 6, 10, 210));
        Stretch(blocker.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

        GameObject window = CreateImage("PopupWindow", root.transform, new Color32(27, 35, 46, 255));
        SetRect(window.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(660f, 430f));

        TextMeshProUGUI title = CreateText("TitleText", window.transform, "화가 이름 정하기", 40f,
            new Color32(242, 229, 202, 255), TextAlignmentOptions.Center,
            new Vector2(560f, 70f), new Vector2(330f, -65f));
        title.fontStyle = FontStyles.Bold;

        CreateText("DescriptionText", window.transform, "게임에서 사용할 닉네임을 입력해 주세요.", 23f,
            new Color32(190, 199, 209, 255), TextAlignmentOptions.Center,
            new Vector2(560f, 52f), new Vector2(330f, -135f));

        TMP_InputField input = CreateInputField("NicknameInputField", window.transform, "2~12글자 닉네임",
            new Vector2(480f, 68f), new Vector2(330f, -215f));
        input.characterLimit = 12;

        TextMeshProUGUI error = CreateText("ErrorText", window.transform, string.Empty, 19f,
            new Color32(225, 92, 92, 255), TextAlignmentOptions.Center,
            new Vector2(520f, 38f), new Vector2(330f, -275f));

        Button confirm = CreateButton("ConfirmButton", window.transform, "확인", new Vector2(300f, 66f),
            new Vector2(330f, -350f), new Color32(210, 155, 32, 255), TextAlignmentOptions.Center);

        NicknamePopupController controller = canvas.GetComponent<NicknamePopupController>();
        if (controller == null) controller = canvas.gameObject.AddComponent<NicknamePopupController>();
        controller.Configure(root, input, confirm, error);
        root.transform.SetAsLastSibling();

        EnsureEventSystem();
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = root;
        Debug.Log("MainScene에 최초 실행용 닉네임 입력 팝업을 추가했습니다.");
    }

    private static void EnsureNicknameController(Canvas canvas)
    {
        Transform root = canvas.transform.Find("NicknamePopup");
        if (root == null) return;
        Transform window = root.Find("PopupWindow");
        NicknamePopupController controller = canvas.GetComponent<NicknamePopupController>();
        if (controller == null) controller = canvas.gameObject.AddComponent<NicknamePopupController>();
        controller.Configure(
            root.gameObject,
            window?.Find("NicknameInputField")?.GetComponent<TMP_InputField>(),
            window?.Find("ConfirmButton")?.GetComponent<Button>(),
            window?.Find("ErrorText")?.GetComponent<TMP_Text>());
        EditorUtility.SetDirty(controller);
    }

    private static Slider CreateSlider(string name, Transform parent, Vector2 size, Vector2 position)
    {
        GameObject root = CreateUIObject(name, parent);
        SetCenteredRect(root.GetComponent<RectTransform>(), size, position);

        GameObject background = CreateImage("Background", root.transform, new Color32(14, 19, 26, 255));
        Stretch(background.GetComponent<RectTransform>(), 0f, 0f, 8f, 8f);

        GameObject fillArea = CreateUIObject("Fill Area", root.transform);
        Stretch(fillArea.GetComponent<RectTransform>(), 8f, 8f, 8f, 8f);
        GameObject fill = CreateImage("Fill", fillArea.transform, new Color32(38, 157, 178, 255));
        Stretch(fill.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

        GameObject handleArea = CreateUIObject("Handle Slide Area", root.transform);
        Stretch(handleArea.GetComponent<RectTransform>(), 8f, 8f, 0f, 0f);
        GameObject handle = CreateImage("Handle", handleArea.transform, new Color32(242, 229, 202, 255));
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 0.5f);
        handleRect.anchorMax = new Vector2(0f, 0.5f);
        handleRect.sizeDelta = new Vector2(26f, 42f);

        Slider slider = root.AddComponent<Slider>();
        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = handleRect;
        slider.targetGraphic = handle.GetComponent<UnityEngine.UI.Image>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0.8f;
        return slider;
    }

    private static void BuildOnceWhenEditorIsReady()
    {
        if (Application.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }

        if (!EditorSceneManager.GetActiveScene().IsValid() ||
            EditorSceneManager.GetActiveScene().path != "Assets/Scenes/MainScene.unity")
        {
            return;
        }

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas != null && canvas.transform.Find("NicknamePopup") == null)
        {
            BuildNicknamePopup();
            canvas = Object.FindFirstObjectByType<Canvas>();
        }

        if (canvas != null && canvas.transform.Find(RootName) != null)
        {
            return;
        }

        BuildRoomBrowserUI();
    }

    private static Canvas CreateCanvas()
    {
        GameObject go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private static void BuildHeader(Transform parent)
    {
        GameObject header = CreateImage("Header", parent, new Color32(12, 16, 22, 245));
        SetRect(header.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 112f));

        CreateButton("BackButton", header.transform, "뒤로가기", new Vector2(142f, 58f), new Vector2(96f, -56f), new Color32(52, 61, 73, 255), TextAlignmentOptions.Center);
        CreateText("TitleText", header.transform, "방 찾기", 42f, new Color32(241, 229, 205, 255), TextAlignmentOptions.Center, new Vector2(420f, 70f), new Vector2(960f, -56f));
        CreateText("PlayerNameText", header.transform, "PLAYER  |  Painter01", 24f, new Color32(120, 205, 218, 255), TextAlignmentOptions.Center, new Vector2(300f, 52f), new Vector2(1690f, -56f));
    }

    private static void BuildSearchArea(Transform parent)
    {
        GameObject area = CreateImage("SearchArea", parent, new Color32(27, 34, 44, 255));
        SetRect(area.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(54f, -138f), new Vector2(-108f, 94f));

        CreateInputField("SearchInputField", area.transform, "방 이름 검색", new Vector2(520f, 58f), new Vector2(280f, -47f));
        CreateDropdown("RoomFilterDropdown", area.transform, new[] { "전체 방", "대기 중", "공개 방" }, new Vector2(220f, 58f), new Vector2(680f, -47f));
        CreateButton("RefreshButton", area.transform, "새로고침", new Vector2(180f, 58f), new Vector2(920f, -47f), new Color32(50, 63, 78, 255), TextAlignmentOptions.Center);
        CreateButton("CreateRoomButton", area.transform, "방 만들기", new Vector2(220f, 58f), new Vector2(1645f, -47f), new Color32(210, 155, 32, 255), TextAlignmentOptions.Center);
    }

    private static void BuildRoomList(Transform parent)
    {
        GameObject header = CreateImage("RoomListHeader", parent, new Color32(35, 43, 54, 255));
        SetRect(header.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(54f, -250f), new Vector2(-108f, 56f));
        CreateColumnLabels(header.transform);

        GameObject scroll = CreateImage("RoomListScrollView", parent, new Color32(14, 19, 26, 235));
        Stretch(scroll.GetComponent<RectTransform>(), 54f, 54f, 318f, 116f);
        ScrollRect scrollRect = scroll.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 25f;

        GameObject viewport = CreateImage("Viewport", scroll.transform, Color.white);
        Stretch(viewport.GetComponent<RectTransform>(), 10f, 28f, 10f, 10f);
        Mask mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject content = CreateUIObject("Content", viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = contentRect;

        CreateRoomListItem(content.transform, "초보자 환영", "Host01", "3 / 8", "대기 중", "공개", true);
        CreateRoomListItem(content.transform, "그림 고수방", "Artist", "7 / 8", "대기 중", "비공개", true);
        CreateRoomListItem(content.transform, "게임 진행 중", "Liar", "8 / 8", "게임 중", "공개", false);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
    }

    private static void CreateColumnLabels(Transform parent)
    {
        CreateText("RoomNameLabel", parent, "방 이름", 22f, Color.white, TextAlignmentOptions.MidlineLeft, new Vector2(480f, 50f), new Vector2(280f, -28f));
        CreateText("HostLabel", parent, "방장", 22f, Color.white, TextAlignmentOptions.Center, new Vector2(220f, 50f), new Vector2(760f, -28f));
        CreateText("PlayerCountLabel", parent, "인원", 22f, Color.white, TextAlignmentOptions.Center, new Vector2(170f, 50f), new Vector2(1010f, -28f));
        CreateText("StatusLabel", parent, "상태", 22f, Color.white, TextAlignmentOptions.Center, new Vector2(190f, 50f), new Vector2(1220f, -28f));
        CreateText("PrivacyLabel", parent, "공개 여부", 22f, Color.white, TextAlignmentOptions.Center, new Vector2(200f, 50f), new Vector2(1425f, -28f));
    }

    private static void CreateRoomListItem(Transform parent, string roomName, string hostName, string players, string status, string privacy, bool joinable)
    {
        GameObject item = CreateImage("RoomListItem_" + (parent.childCount + 1).ToString("00"), parent, new Color32(29, 37, 48, 255));
        LayoutElement layout = item.AddComponent<LayoutElement>();
        layout.preferredHeight = 90f;
        layout.minHeight = 90f;

        CreateText("RoomNameText", item.transform, roomName, 25f, new Color32(242, 231, 211, 255), TextAlignmentOptions.MidlineLeft, new Vector2(500f, 70f), new Vector2(250f, -45f));
        CreateText("HostNameText", item.transform, hostName, 22f, new Color32(190, 199, 209, 255), TextAlignmentOptions.Center, new Vector2(220f, 70f), new Vector2(730f, -45f));
        CreateText("PlayerCountText", item.transform, players, 24f, Color.white, TextAlignmentOptions.Center, new Vector2(170f, 70f), new Vector2(980f, -45f));
        CreateText("StatusText", item.transform, status, 22f, joinable ? new Color32(85, 205, 145, 255) : new Color32(205, 92, 92, 255), TextAlignmentOptions.Center, new Vector2(190f, 70f), new Vector2(1190f, -45f));
        CreateText("PrivacyText", item.transform, privacy, 22f, new Color32(205, 184, 130, 255), TextAlignmentOptions.Center, new Vector2(200f, 70f), new Vector2(1395f, -45f));
        Button button = CreateButton("JoinButton", item.transform, joinable ? "입장" : "진행 중", new Vector2(160f, 56f), new Vector2(1625f, -45f), joinable ? new Color32(38, 157, 178, 255) : new Color32(73, 79, 88, 255), TextAlignmentOptions.Center);
        button.interactable = joinable;
    }

    private static void BuildFooter(Transform parent)
    {
        GameObject footer = CreateImage("Footer", parent, new Color32(12, 16, 22, 245));
        SetRect(footer.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(0f, 96f));
        CreateText("PageText", footer.transform, "1 / 1", 22f, new Color32(174, 184, 196, 255), TextAlignmentOptions.Center, new Vector2(200f, 52f), new Vector2(960f, 48f));
        CreateButton("QuickJoinButton", footer.transform, "검색", new Vector2(220f, 58f), new Vector2(1745f, 48f), new Color32(178, 45, 58, 255), TextAlignmentOptions.Center);
    }

    private static void BuildEmptyState(Transform parent)
    {
        GameObject empty = CreateUIObject("EmptyRoomMessage", parent);
        SetRect(empty.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(620f, 180f));
        CreateText("MessageText", empty.transform, "생성된 방이 없습니다.\n새로운 방을 만들어 보세요.", 28f, new Color32(184, 193, 204, 255), TextAlignmentOptions.Center, new Vector2(620f, 100f), new Vector2(310f, -50f));
        empty.SetActive(false);
    }

    private static void BuildLoadingPanel(Transform parent)
    {
        GameObject loading = CreateImage("LoadingPanel", parent, new Color32(10, 14, 20, 190));
        Stretch(loading.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        CreateText("LoadingText", loading.transform, "방 목록을 불러오는 중...", 30f, Color.white, TextAlignmentOptions.Center, new Vector2(520f, 80f), new Vector2(960f, 540f));
        loading.SetActive(false);
    }

    private static GameObject BuildCreateRoomPopup(Transform canvas)
    {
        GameObject root = CreateUIObject("CreateRoomPopup", canvas);
        Stretch(root.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

        GameObject blocker = CreateImage("Blocker", root.transform, new Color32(0, 0, 0, 170));
        Stretch(blocker.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

        GameObject window = CreateImage("PopupWindow", root.transform, new Color32(28, 35, 45, 255));
        SetRect(window.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 790f));

        CreateText("PopupTitleText", window.transform, "방 만들기", 38f, new Color32(242, 229, 202, 255), TextAlignmentOptions.Center, new Vector2(620f, 70f), new Vector2(360f, -56f));
        CreateFieldRow(window.transform, "RoomNameRow", "방 이름", 135f, FieldType.Input, new[] { "방 이름을 입력하세요" });
        CreateFieldRow(window.transform, "MaxPlayersRow", "최대 인원", 225f, FieldType.Dropdown, new[] { "4명", "5명", "6명", "7명", "8명" });
        CreateFieldRow(window.transform, "GameModeRow", "게임 모드", 315f, FieldType.Dropdown, new[] { "라이어 + 마피아", "라이어", "마피아" });
        CreateFieldRow(window.transform, "PrivateRoomRow", "공개 설정", 405f, FieldType.Dropdown, new[] { "공개 방", "비공개 방" });
        CreateFieldRow(window.transform, "PasswordRow", "비밀번호", 495f, FieldType.Input, new[] { "비공개 방에서 사용" });
        CreateFieldRow(window.transform, "RoundCountRow", "라운드", 585f, FieldType.Dropdown, new[] { "3 라운드", "5 라운드", "7 라운드" });

        CreateButton("CancelButton", window.transform, "취소", new Vector2(220f, 64f), new Vector2(210f, -735f), new Color32(67, 75, 87, 255), TextAlignmentOptions.Center);
        CreateButton("CreateButton", window.transform, "방 생성", new Vector2(300f, 64f), new Vector2(510f, -735f), new Color32(207, 151, 31, 255), TextAlignmentOptions.Center);
        return root;
    }

    private enum FieldType { Input, Dropdown }

    private static void CreateFieldRow(Transform parent, string name, string label, float y, FieldType type, string[] values)
    {
        GameObject row = CreateUIObject(name, parent);
        SetRect(row.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(52f, -y), new Vector2(-104f, 72f));
        CreateText("LabelText", row.transform, label, 24f, new Color32(211, 217, 224, 255), TextAlignmentOptions.MidlineLeft, new Vector2(180f, 60f), new Vector2(90f, -36f));
        if (type == FieldType.Input)
        {
            CreateInputField("InputField", row.transform, values[0], new Vector2(380f, 58f), new Vector2(420f, -36f));
        }
        else
        {
            CreateDropdown("Dropdown", row.transform, values, new Vector2(380f, 58f), new Vector2(420f, -36f));
        }
    }

    private static TMP_InputField CreateInputField(string name, Transform parent, string placeholder, Vector2 size, Vector2 position)
    {
        GameObject root = CreateImage(name, parent, new Color32(16, 21, 28, 255));
        SetCenteredRect(root.GetComponent<RectTransform>(), size, position);

        GameObject viewport = CreateUIObject("Text Area", root.transform);
        Stretch(viewport.GetComponent<RectTransform>(), 18f, 18f, 8f, 8f);
        RectMask2D mask = viewport.AddComponent<RectMask2D>();

        TextMeshProUGUI placeholderText = CreateText("Placeholder", viewport.transform, placeholder, 21f, new Color32(126, 137, 151, 255), TextAlignmentOptions.MidlineLeft, size - new Vector2(36f, 16f), new Vector2((size.x - 36f) * 0.5f, -(size.y - 16f) * 0.5f));
        TextMeshProUGUI inputText = CreateText("Text", viewport.transform, string.Empty, 21f, Color.white, TextAlignmentOptions.MidlineLeft, size - new Vector2(36f, 16f), new Vector2((size.x - 36f) * 0.5f, -(size.y - 16f) * 0.5f));

        TMP_InputField input = root.AddComponent<TMP_InputField>();
        input.textViewport = viewport.GetComponent<RectTransform>();
        input.textComponent = inputText;
        input.placeholder = placeholderText;
        input.lineType = TMP_InputField.LineType.SingleLine;
        _ = mask;
        return input;
    }

    private static TMP_Dropdown CreateDropdown(string name, Transform parent, string[] options, Vector2 size, Vector2 position)
    {
        GameObject root = CreateImage(name, parent, new Color32(35, 44, 56, 255));
        SetCenteredRect(root.GetComponent<RectTransform>(), size, position);

        TextMeshProUGUI label = CreateText("Label", root.transform, options[0], 21f, Color.white, TextAlignmentOptions.MidlineLeft, size - new Vector2(56f, 8f), new Vector2((size.x - 56f) * 0.5f + 10f, -size.y * 0.5f));
        CreateText("Arrow", root.transform, "▼", 18f, new Color32(214, 177, 75, 255), TextAlignmentOptions.Center, new Vector2(42f, size.y), new Vector2(size.x - 28f, -size.y * 0.5f));

        GameObject template = CreateImage("Template", root.transform, new Color32(24, 31, 40, 255));
        SetRect(template.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(0f, 180f));
        ScrollRect scrollRect = template.AddComponent<ScrollRect>();
        GameObject viewport = CreateImage("Viewport", template.transform, Color.white);
        Stretch(viewport.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        viewport.AddComponent<Mask>().showMaskGraphic = false;
        GameObject content = CreateUIObject("Content", viewport.transform);
        Stretch(content.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        GameObject item = CreateUIObject("Item", content.transform);
        SetRect(item.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 52f));
        Toggle toggle = item.AddComponent<Toggle>();
        GameObject itemBg = CreateImage("Item Background", item.transform, new Color32(35, 44, 56, 255));
        Stretch(itemBg.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        TextMeshProUGUI itemLabel = CreateText("Item Label", item.transform, options[0], 20f, Color.white, TextAlignmentOptions.MidlineLeft, new Vector2(size.x - 32f, 52f), new Vector2((size.x - 32f) * 0.5f + 12f, -26f));
        toggle.targetGraphic = itemBg.GetComponent<UnityEngine.UI.Image>();
        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = content.GetComponent<RectTransform>();
        template.SetActive(false);

        TMP_Dropdown dropdown = root.AddComponent<TMP_Dropdown>();
        dropdown.targetGraphic = root.GetComponent<UnityEngine.UI.Image>();
        dropdown.template = template.GetComponent<RectTransform>();
        dropdown.captionText = label;
        dropdown.itemText = itemLabel;
        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string>(options));
        return dropdown;
    }

    private static Button CreateButton(string name, Transform parent, string text, Vector2 size, Vector2 position, Color color, TextAlignmentOptions alignment)
    {
        GameObject root = CreateImage(name, parent, color);
        SetCenteredRect(root.GetComponent<RectTransform>(), size, position);
        Button button = root.AddComponent<Button>();
        button.targetGraphic = root.GetComponent<UnityEngine.UI.Image>();

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.65f);
        button.colors = colors;

        TextMeshProUGUI label = CreateText("ButtonText", root.transform, text, 23f, Color.white, alignment, size, new Vector2(size.x * 0.5f, -size.y * 0.5f));
        label.fontStyle = FontStyles.Bold;
        return button;
    }

    private static GameObject CreatePanel(string name, Transform parent, Color color)
    {
        GameObject panel = CreateImage(name, parent, color);
        Stretch(panel.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        return panel;
    }

    private static GameObject CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = CreateUIObject(name, parent);
        UnityEngine.UI.Image image = go.AddComponent<UnityEngine.UI.Image>();
        image.color = color;
        image.raycastTarget = color.a > 0.01f;
        return go;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions alignment, Vector2 rectSize, Vector2 position)
    {
        GameObject go = CreateUIObject(name, parent);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size + FontSizeIncrease;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;
        if (font != null)
        {
            tmp.font = font;
        }
        SetCenteredRect(go.GetComponent<RectTransform>(), rectSize, position);
        return tmp;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void SetCenteredRect(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void EnsureEventSystem()
    {
        EventSystem[] systems = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
        if (systems.Length > 0)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }
}
