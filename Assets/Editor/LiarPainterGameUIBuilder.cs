using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class LiarPainterGameUIBuilder
{
    private const string RootName = "GameUI";
    private const string FontPath = "Assets/Fonts/Griun_Oksooni-Rg SDF.asset";
    private const string PlayerSlotPrefabPath = "Assets/Prefabs/UI/PlayerSlot.prefab";
    private static TMP_FontAsset font;

    [MenuItem("Liar Painter/UI/Build Game UI")]
    public static void BuildGameUI()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("게임 UI는 Edit Mode에서 생성해 주세요.");
            return;
        }

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            canvas = CreateCanvas();
        }

        Transform existing = canvas.transform.Find(RootName);
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            Debug.Log("GameUI가 이미 존재하므로 중복 생성하지 않았습니다.");
            return;
        }

        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        GameObject root = CreateImage(RootName, canvas.transform, new Color32(24, 48, 78, 255), false);
        Stretch(root.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

        BuildHeader(root.transform);
        BuildPlayerColumn(root.transform, "LeftPlayerList", true, 1);
        BuildPlayerColumn(root.transform, "RightPlayerList", false, 5);
        BuildDrawingArea(root.transform);
        BuildDrawingToolsUI();

        EnsureEventSystem();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = root;
        Debug.Log("GameScene용 게임 UI 생성 완료: 중앙 그림 캔버스, 플레이어 8명, 헤더가 추가되었습니다.");
    }

    [MenuItem("Liar Painter/UI/Build Drawing Tools UI")]
    public static void BuildDrawingToolsUI()
    {
        Transform drawingArea = GameObject.Find("Canvas/GameUI/DrawingArea")?.transform;
        Transform footer = drawingArea?.Find("DrawingFooter");
        if (drawingArea == null || footer == null)
        {
            Debug.LogWarning("DrawingArea 또는 DrawingFooter를 찾을 수 없습니다.");
            return;
        }

        if (footer.Find("DrawingToolbar") != null)
        {
            Selection.activeGameObject = footer.Find("DrawingToolbar").gameObject;
            Debug.Log("DrawingToolbar가 이미 존재하므로 중복 생성하지 않았습니다.");
            return;
        }

        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        UnityEngine.UI.Image footerBackground = footer.GetComponent<UnityEngine.UI.Image>();
        if (footerBackground == null) footerBackground = footer.gameObject.AddComponent<UnityEngine.UI.Image>();
        footerBackground.color = new Color32(21, 28, 38, 255);
        footerBackground.raycastTarget = false;

        GameObject toolbar = CreateUIObject("DrawingToolbar", footer);
        Stretch(toolbar.GetComponent<RectTransform>(), 12f, 12f, 6f, 6f);

        Button paletteButton = CreateButton("PaletteButton", toolbar.transform, "팔레트", new Vector2(170f, 56f), new Vector2(120f, -29f), new Color32(61, 73, 88, 255));
        GameObject toolbarPreviewObject = CreateImage("CurrentColorPreview", toolbar.transform, new Color32(25, 25, 25, 255), false);
        SetCentered(toolbarPreviewObject.GetComponent<RectTransform>(), new Vector2(48f, 48f), new Vector2(232f, -29f));

        Button eraserMenuButton = CreateButton("EraserMenuButton", toolbar.transform, "지우개", new Vector2(170f, 56f), new Vector2(365f, -29f), new Color32(61, 73, 88, 255));
        GameObject eraserSubmenu = CreateUIObject("EraserSubmenu", toolbar.transform);
        Stretch(eraserSubmenu.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        Button partialEraserButton = CreateButton("PartialEraserButton", eraserSubmenu.transform, "부분 지우개", new Vector2(185f, 56f), new Vector2(550f, -29f), new Color32(61, 73, 88, 255));
        Button strokeEraserButton = CreateButton("StrokeEraserButton", eraserSubmenu.transform, "선 전체 지우개", new Vector2(210f, 56f), new Vector2(760f, -29f), new Color32(61, 73, 88, 255));
        TextMeshProUGUI selectedToolText = CreateText("SelectedToolText", toolbar.transform, "선택 도구: 펜", 18f, new Color32(170, 181, 194, 255), TextAlignmentOptions.MidlineLeft, new Vector2(250f, 52f), new Vector2(1000f, -29f));

        GameObject eraserSizePopup = CreateImage("EraserSizePopup", drawingArea, new Color32(27, 35, 46, 255), true);
        SetRect(eraserSizePopup.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(280f, 82f), new Vector2(590f, 92f));
        CreateText("TitleText", eraserSizePopup.transform, "지우개 굵기", 18f, new Color32(242, 229, 202, 255), TextAlignmentOptions.Center, new Vector2(120f, 44f), new Vector2(70f, -46f));
        Button[] eraserSizeButtons = new Button[5];
        for (int i = 0; i < eraserSizeButtons.Length; i++)
        {
            int size = (i + 1) * 2;
            eraserSizeButtons[i] = CreateButton($"EraserSize{size}Button", eraserSizePopup.transform, "", new Vector2(72f, 52f), new Vector2(170f + i * 82f, -46f), new Color32(61, 73, 88, 255));
            GameObject sizeCircle = CreateImage("SizeCircle", eraserSizeButtons[i].transform, new Color32(242, 229, 202, 255), false);
            UnityEngine.UI.Image circleImage = sizeCircle.GetComponent<UnityEngine.UI.Image>();
            circleImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            circleImage.preserveAspect = true;
            float circleDiameter = 6f + size * 3f;
            SetCentered(sizeCircle.GetComponent<RectTransform>(), new Vector2(circleDiameter, circleDiameter), Vector2.zero);
        }

        GameObject popup = CreateImage("PalettePopup", drawingArea, new Color32(27, 35, 46, 255), true);
        SetRect(popup.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(22f, 86f), new Vector2(670f, 500f));
        CreateText("TitleText", popup.transform, "컬러 팔레트", 28f, new Color32(242, 229, 202, 255), TextAlignmentOptions.Center, new Vector2(600f, 54f), new Vector2(335f, -36f));

        GameObject wheelObject = CreateUIObject("ColorWheel", popup.transform);
        RawImage wheelImage = wheelObject.AddComponent<RawImage>();
        wheelImage.color = Color.white;
        wheelImage.raycastTarget = true;
        SetCentered(wheelObject.GetComponent<RectTransform>(), new Vector2(300f, 300f), new Vector2(490f, -222f));

        GameObject selectorObject = CreateImage("Selector", wheelObject.transform, Color.white, false);
        RectTransform selectorRect = selectorObject.GetComponent<RectTransform>();
        selectorRect.anchorMin = selectorRect.anchorMax = new Vector2(0.5f, 0.5f);
        selectorRect.pivot = new Vector2(0.5f, 0.5f);
        selectorRect.sizeDelta = new Vector2(18f, 18f);
        selectorRect.anchoredPosition = Vector2.zero;
        Outline selectorOutline = selectorObject.AddComponent<Outline>();
        selectorOutline.effectColor = new Color32(10, 10, 10, 255);
        selectorOutline.effectDistance = new Vector2(2f, -2f);

        GameObject previewObject = CreateImage("ColorPreview", popup.transform, Color.white, false);
        SetCentered(previewObject.GetComponent<RectTransform>(), new Vector2(150f, 72f), new Vector2(165f, -132f));
        TextMeshProUGUI colorValueText = CreateText("ColorValueText", popup.transform, "#FFFFFF", 18f, new Color32(220, 226, 233, 255), TextAlignmentOptions.Center, new Vector2(170f, 40f), new Vector2(165f, -184f));

        TextMeshProUGUI brushValue;
        Slider brush = CreateToolSlider(popup.transform, "BrushSizeSlider", "굵기", 260f, new Color32(213, 164, 56, 255), 1f, 10f, 10f, out brushValue, true, true);

        TextMeshProUGUI opacityValue;
        Slider opacity = CreateToolSlider(popup.transform, "OpacitySlider", "불투명도", 330f, new Color32(38, 157, 178, 255), 0f, 100f, 100f, out opacityValue, false, true);
        opacityValue.text = "100%";

        TextMeshProUGUI valueText;
        Slider valueSlider = CreateToolSlider(popup.transform, "ValueSlider", "명도", 400f, Color.clear, 0f, 100f, 100f, out valueText, false, true);
        valueText.text = "100%";
        valueSlider.transform.Find("Fill Area/Fill").GetComponent<UnityEngine.UI.Image>().color = Color.clear;
        GameObject valueGradientObject = CreateUIObject("ValueGradient", valueSlider.transform);
        RawImage valueGradient = valueGradientObject.AddComponent<RawImage>();
        valueGradient.raycastTarget = false;
        Stretch(valueGradientObject.GetComponent<RectTransform>(), 4f, 4f, 8f, 8f);
        valueGradientObject.transform.SetSiblingIndex(1);

        ColorWheelPicker wheelPicker = wheelObject.AddComponent<ColorWheelPicker>();
        wheelPicker.Configure(wheelImage, selectorRect, previewObject.GetComponent<UnityEngine.UI.Image>(), colorValueText, valueSlider, valueGradient, valueText);

        Button closeButton = CreateButton("CloseButton", popup.transform, "닫기", new Vector2(170f, 55f), new Vector2(190f, -452f), new Color32(75, 86, 100, 255));
        Button applyButton = CreateButton("ApplyButton", popup.transform, "적용", new Vector2(210f, 55f), new Vector2(400f, -452f), new Color32(210, 155, 32, 255));

        DrawingToolSettings controller = drawingArea.GetComponent<DrawingToolSettings>();
        if (controller == null) controller = drawingArea.gameObject.AddComponent<DrawingToolSettings>();
        controller.Configure(
            popup,
            paletteButton,
            partialEraserButton,
            strokeEraserButton,
            closeButton,
            applyButton,
            wheelPicker,
            brush,
            brushValue,
            opacity,
            opacityValue,
            selectedToolText,
            previewObject.GetComponent<UnityEngine.UI.Image>(),
            toolbarPreviewObject.GetComponent<UnityEngine.UI.Image>());
        controller.ConfigureEraserMenu(eraserMenuButton, eraserSubmenu, eraserSizePopup, eraserSizeButtons);

        popup.SetActive(false);
        eraserSubmenu.SetActive(false);
        eraserSizePopup.SetActive(false);
        popup.transform.SetAsLastSibling();
        eraserSizePopup.transform.SetAsLastSibling();
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = toolbar;
        Debug.Log("그림 영역 하단에 팔레트와 두 가지 지우개 도구를 추가했습니다.");
    }

    private static Slider CreateToolSlider(Transform parent, string name, string label, float y, Color fillColor, float min, float max, float value, out TextMeshProUGUI valueText, bool showPixels, bool wholeNumbers)
    {
        string groupName = name.EndsWith("Slider") ? name.Substring(0, name.Length - "Slider".Length) : name + "Group";
        GameObject group = CreateUIObject(groupName, parent);
        SetCentered(group.GetComponent<RectTransform>(), new Vector2(360f, 50f), new Vector2(185f, -y));

        TextMeshProUGUI labelText = CreateText(name + "Label", group.transform, label, 19f, Color.white, TextAlignmentOptions.Center, new Vector2(70f, 42f), Vector2.zero);
        SetGroupChild(labelText.rectTransform, new Vector2(70f, 42f), new Vector2(-137f, 0f));

        GameObject sliderObject = CreateUIObject(name, group.transform);
        SetGroupChild(sliderObject.GetComponent<RectTransform>(), new Vector2(185f, 28f), Vector2.zero);

        GameObject background = CreateImage("Background", sliderObject.transform, new Color32(12, 17, 24, 255), false);
        Stretch(background.GetComponent<RectTransform>(), 0f, 0f, 9f, 9f);
        GameObject fillArea = CreateUIObject("Fill Area", sliderObject.transform);
        Stretch(fillArea.GetComponent<RectTransform>(), 4f, 4f, 9f, 9f);
        GameObject fill = CreateImage("Fill", fillArea.transform, fillColor, false);
        Stretch(fill.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        GameObject handleArea = CreateUIObject("Handle Slide Area", sliderObject.transform);
        Stretch(handleArea.GetComponent<RectTransform>(), 8f, 8f, 0f, 0f);
        GameObject handle = CreateImage("Handle", handleArea.transform, new Color32(242, 229, 202, 255), true);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 0.5f);
        handleRect.anchorMax = new Vector2(0f, 0.5f);
        handleRect.sizeDelta = new Vector2(22f, 38f);

        Slider slider = sliderObject.AddComponent<Slider>();
        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = handleRect;
        slider.targetGraphic = handle.GetComponent<UnityEngine.UI.Image>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = wholeNumbers;
        slider.value = value;

        string initialValue = showPixels ? $"{Mathf.RoundToInt(value)} px" : Mathf.RoundToInt(value).ToString();
        valueText = CreateText(name + "ValueText", group.transform, initialValue, 17f, new Color32(190, 201, 213, 255), TextAlignmentOptions.Center, new Vector2(60f, 42f), Vector2.zero);
        SetGroupChild(valueText.rectTransform, new Vector2(60f, 42f), new Vector2(115f, 0f));
        return slider;
    }

    private static void SetGroupChild(RectTransform rect, Vector2 size, Vector2 localPosition)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = localPosition;
    }

    private static void BuildHeader(Transform parent)
    {
        GameObject header = CreateImage("Header", parent, new Color32(11, 16, 24, 250), false);
        SetRect(header.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 112f));

        CreateButton("BackButton", header.transform, "뒤로가기", new Vector2(170f, 62f), new Vector2(115f, -56f), new Color32(65, 75, 89, 255));
        CreateText("GameTitleText", header.transform, "LIAR PAINTER", 26f, new Color32(112, 203, 218, 255), TextAlignmentOptions.MidlineLeft, new Vector2(300f, 60f), new Vector2(370f, -56f));

        GameObject timer = CreateImage("TimerPanel", header.transform, new Color32(29, 38, 50, 255), false);
        SetCentered(timer.GetComponent<RectTransform>(), new Vector2(270f, 76f), new Vector2(960f, -56f));
        CreateText("TimerLabel", timer.transform, "남은 시간", 17f, new Color32(170, 181, 194, 255), TextAlignmentOptions.Center, new Vector2(100f, 52f), new Vector2(60f, -38f));
        CreateText("TimerText", timer.transform, "01 : 00", 31f, new Color32(242, 229, 202, 255), TextAlignmentOptions.Center, new Vector2(150f, 58f), new Vector2(185f, -38f));

        CreateButton("SettingsButton", header.transform, "설정", new Vector2(150f, 62f), new Vector2(1805f, -56f), new Color32(65, 75, 89, 255));
    }

    private static void BuildPlayerColumn(Transform parent, string name, bool left, int firstPlayerNumber)
    {
        GameObject column = CreateUIObject(name, parent);
        RectTransform rect = column.GetComponent<RectTransform>();
        rect.anchorMin = left ? new Vector2(0f, 0f) : new Vector2(1f, 0f);
        rect.anchorMax = left ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
        rect.pivot = left ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f);
        rect.anchoredPosition = left ? new Vector2(34f, -56f) : new Vector2(-34f, -56f);
        rect.sizeDelta = new Vector2(330f, -152f);

        for (int i = 0; i < 4; i++)
        {
            int slotNumber = firstPlayerNumber + i;
            GameObject slot;
            GameObject slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerSlotPrefabPath);
            if (slotPrefab != null)
            {
                slot = (GameObject)PrefabUtility.InstantiatePrefab(slotPrefab, column.transform);
                slot.name = $"PlayerSlot_{slotNumber:00}";
                slot.GetComponent<PlayerSlotView>()?.SetEmpty(slotNumber);
            }
            else
            {
                CreatePlayerCard(column.transform, slotNumber, false);
                slot = column.transform.Find($"PlayerSlot_{slotNumber:00}").gameObject;
            }

            SetPlayerSlotPosition(slot.GetComponent<RectTransform>(), i);
        }
    }

    private static void SetPlayerSlotPosition(RectTransform rect, int index)
    {
        const float topPadding = 12f;
        const float slotHeight = 212.5f;
        const float spacing = 18f;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, slotHeight);
        rect.anchoredPosition = new Vector2(0f, -(topPadding + index * (slotHeight + spacing)));
    }

    private static void CreatePlayerCard(Transform parent, int playerNumber, bool currentPlayer)
    {
        Color cardColor = currentPlayer ? new Color32(30, 77, 88, 255) : new Color32(27, 35, 46, 255);
        GameObject card = CreateImage($"PlayerSlot_{playerNumber:00}", parent, cardColor, false);
        LayoutElement element = card.AddComponent<LayoutElement>();
        element.minHeight = 212.5f;
        element.preferredHeight = 212.5f;
        element.flexibleHeight = 0f;

        GameObject numberBadge = CreateImage("NumberBadge", card.transform, currentPlayer ? new Color32(38, 157, 178, 255) : new Color32(61, 73, 88, 255), false);
        SetCentered(numberBadge.GetComponent<RectTransform>(), new Vector2(64f, 64f), new Vector2(48f, -48f));
        CreateText("NumberText", numberBadge.transform, playerNumber.ToString(), 25f, Color.white, TextAlignmentOptions.Center, new Vector2(64f, 64f), new Vector2(32f, -32f));

        string playerName = currentPlayer ? "Painter01 (나)" : $"Player {playerNumber:00}";
        CreateText("PlayerNameText", card.transform, playerName, 23f, new Color32(242, 233, 214, 255), TextAlignmentOptions.MidlineLeft, new Vector2(220f, 54f), new Vector2(195f, -48f));
        CreateText("StatusText", card.transform, currentPlayer ? "그리는 중" : "대기 중", 18f, currentPlayer ? new Color32(102, 222, 184, 255) : new Color32(150, 161, 174, 255), TextAlignmentOptions.MidlineLeft, new Vector2(220f, 42f), new Vector2(195f, -98f));

        GameObject stateLine = CreateImage("StateLine", card.transform, currentPlayer ? new Color32(38, 157, 178, 255) : new Color32(49, 59, 72, 255), false);
        RectTransform lineRect = stateLine.GetComponent<RectTransform>();
        lineRect.anchorMin = new Vector2(0f, 0f);
        lineRect.anchorMax = new Vector2(1f, 0f);
        lineRect.pivot = new Vector2(0.5f, 0f);
        lineRect.offsetMin = Vector2.zero;
        lineRect.offsetMax = new Vector2(0f, 5f);
    }

    private static void BuildDrawingArea(Transform parent)
    {
        GameObject frame = CreateImage("DrawingArea", parent, new Color32(17, 23, 31, 255), false);
        RectTransform frameRect = frame.GetComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(0f, 0f);
        frameRect.anchorMax = new Vector2(1f, 1f);
        frameRect.offsetMin = new Vector2(390f, 42f);
        frameRect.offsetMax = new Vector2(-390f, -140f);

        GameObject topBar = CreateImage("DrawingHeader", frame.transform, new Color32(31, 40, 52, 255), false);
        SetRect(topBar.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 70f));
        CreateText("RoundText", topBar.transform, "ROUND 1", 20f, new Color32(112, 203, 218, 255), TextAlignmentOptions.MidlineLeft, new Vector2(230f, 54f), new Vector2(135f, -35f));
        CreateText("TopicText", topBar.transform, "제시어를 그림으로 표현하세요", 23f, new Color32(242, 229, 202, 255), TextAlignmentOptions.Center, new Vector2(520f, 54f), new Vector2(570f, -35f));

        GameObject canvas = CreateImage("DrawingCanvas", frame.transform, new Color32(239, 236, 225, 255), true);
        canvas.AddComponent<DrawingCanvasLineRenderer>();
        Stretch(canvas.GetComponent<RectTransform>(), 24f, 24f, 94f, 80f);
        CreateText("CanvasGuideText", canvas.transform, "그림을 그리는 공간", 23f, new Color32(145, 145, 140, 255), TextAlignmentOptions.Center, new Vector2(360f, 64f), new Vector2(550f, -350f));

        GameObject footer = CreateUIObject("DrawingFooter", frame.transform);
        SetRect(footer.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 70f));
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

    private static Button CreateButton(string name, Transform parent, string label, Vector2 size, Vector2 position, Color color)
    {
        GameObject root = CreateImage(name, parent, color, true);
        SetCentered(root.GetComponent<RectTransform>(), size, position);
        Button button = root.AddComponent<Button>();
        button.targetGraphic = root.GetComponent<UnityEngine.UI.Image>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        button.colors = colors;
        TextMeshProUGUI text = CreateText("ButtonText", root.transform, label, 22f, Color.white, TextAlignmentOptions.Center, size, size * new Vector2(0.5f, -0.5f));
        text.fontStyle = FontStyles.Bold;
        return button;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string value, float size, Color color, TextAlignmentOptions alignment, Vector2 rectSize, Vector2 position)
    {
        GameObject go = CreateUIObject(name, parent);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        if (font != null) text.font = font;
        SetCentered(go.GetComponent<RectTransform>(), rectSize, position);
        return text;
    }

    private static GameObject CreateImage(string name, Transform parent, Color color, bool raycastTarget)
    {
        GameObject go = CreateUIObject(name, parent);
        UnityEngine.UI.Image image = go.AddComponent<UnityEngine.UI.Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return go;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void SetCentered(RectTransform rect, Vector2 size, Vector2 position)
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
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }
}
