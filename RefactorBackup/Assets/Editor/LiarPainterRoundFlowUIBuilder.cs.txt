using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.Netcode;

[InitializeOnLoad]
public static class LiarPainterRoundFlowUIBuilder
{
    private const string PlayerSlotPrefabPath = "Assets/Prefabs/UI/PlayerSlot.prefab";
    private const string FontPath = "Assets/Fonts/Griun_Oksooni-Rg SDF.asset";
    private static TMP_FontAsset font;

    static LiarPainterRoundFlowUIBuilder()
    {
        EditorApplication.delayCall += ApplyToOpenGameScene;
    }

    [MenuItem("Liar Painter/UI/Add Single Round Result And Ready UI")]
    public static void ApplyToOpenGameScene()
    {
        if (Application.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ApplyToOpenGameScene;
            return;
        }

        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        AddButtonsToPlayerSlotPrefab();

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != "GameScene") return;

        GameObject gameUI = GameObject.Find("Canvas/GameUI");
        if (gameUI == null)
        {
            Debug.LogWarning("GameScene의 Canvas/GameUI를 찾을 수 없어 단일 라운드 UI를 추가하지 않았습니다.");
            return;
        }

        AddLobbyReadyController(gameUI);
        AddResultPopup(gameUI);
        AddGameCoordinator(gameUI);
        AddNetworkGameCoordinator(gameUI);
        AddGameHud(gameUI);

        Transform resultPopup = gameUI.transform.Find("GameResultPopup");
        if (resultPopup != null) resultPopup.SetAsLastSibling();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = gameUI;
        Debug.Log("기존 GameScene을 유지한 채 PlayerSlot 준비/시작 버튼과 7초 결과 팝업을 추가했습니다.");
    }

    public static void ApplyToGameSceneAsset()
    {
        if (Application.isPlaying) return;
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Single);
        ApplyToOpenGameScene();
    }

    private static void AddButtonsToPlayerSlotPrefab()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerSlotPrefabPath);
        if (prefabRoot == null) return;

        try
        {
            PlayerSlotView view = prefabRoot.GetComponent<PlayerSlotView>();
            if (view == null) return;

            Button readyButton = GetOrCreateButton(
                prefabRoot.transform,
                "ReadyButton",
                "준비하기",
                new Vector2(128f, 48f),
                new Vector2(88f, -164f),
                new Color32(38, 157, 178, 255));

            Button startButton = GetOrCreateButton(
                prefabRoot.transform,
                "StartGameButton",
                "시작하기",
                new Vector2(128f, 48f),
                new Vector2(232f, -164f),
                new Color32(210, 155, 32, 255));

            TMP_Text readyLabel = readyButton.transform.Find("ButtonText")?.GetComponent<TMP_Text>();
            view.ConfigureReadyButtons(readyButton, readyLabel, startButton);
            EditorUtility.SetDirty(view);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerSlotPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void AddLobbyReadyController(GameObject gameUI)
    {
        LobbyReadyController controller = gameUI.GetComponent<LobbyReadyController>();
        if (controller == null) controller = gameUI.AddComponent<LobbyReadyController>();
        EditorUtility.SetDirty(controller);
    }

    private static void AddResultPopup(GameObject gameUI)
    {
        Transform existing = gameUI.transform.Find("GameResultPopup");
        GameObject popup = existing != null ? existing.gameObject : CreateImage(
            "GameResultPopup",
            gameUI.transform,
            new Color32(5, 9, 15, 225),
            true);

        Stretch(popup.GetComponent<RectTransform>());

        GameObject window = GetOrCreateImage(
            popup.transform,
            "ResultWindow",
            new Color32(27, 35, 46, 255),
            true);
        SetCentered(window.GetComponent<RectTransform>(), new Vector2(900f, 700f), new Vector2(960f, -540f));

        TMP_Text title = GetOrCreateText(
            window.transform,
            "VictoryTitleText",
            "시민 팀 승리!",
            46f,
            new Color32(242, 229, 202, 255),
            TextAlignmentOptions.Center,
            new Vector2(780f, 90f),
            new Vector2(450f, -80f));

        Transform legacyHeader = window.transform.Find("RoleHeaderText");
        if (legacyHeader != null) legacyHeader.gameObject.SetActive(false);
        Transform legacyResults = window.transform.Find("AllPlayerRolesText");
        if (legacyResults != null) legacyResults.gameObject.SetActive(false);

        GameObject table = GetOrCreateImage(
            window.transform,
            "ResultTable",
            new Color32(18, 25, 34, 255),
            false);
        SetCentered(table.GetComponent<RectTransform>(), new Vector2(800f, 430f), new Vector2(450f, -370f));

        GameObject headerBackground = GetOrCreateImage(
            table.transform,
            "HeaderBackground",
            new Color32(38, 157, 178, 110),
            false);
        SetCentered(headerBackground.GetComponent<RectTransform>(), new Vector2(780f, 58f), new Vector2(400f, -39f));

        CreateTableHeader(table.transform, "PlayerHeaderText", "플레이어", 105f, 190f);
        CreateTableHeader(table.transform, "RoleHeaderText", "해당 직업", 300f, 190f);
        CreateTableHeader(table.transform, "KeywordHeaderText", "선택받은 키워드", 510f, 230f);
        CreateTableHeader(table.transform, "ResultHeaderText", "결과", 705f, 140f);

        TMP_Text names = CreateTableColumn(table.transform, "PlayerNamesText", "Player 01\nPlayer 02\nPlayer 03\nPlayer 04", 105f, 190f);
        TMP_Text roles = CreateTableColumn(table.transform, "PlayerRolesText", "시민\n감정가\n사짜 화가\n알코올 중독 화가", 300f, 190f);
        TMP_Text keywords = CreateTableColumn(table.transform, "PlayerKeywordsText", "고양이\n고양이\n미부여\n호랑이", 510f, 230f);
        TMP_Text results = CreateTableColumn(table.transform, "PlayerResultsText", "승리\n승리\n패배\n승리", 705f, 140f);

        CreateVerticalDivider(table.transform, "Divider01", 202f);
        CreateVerticalDivider(table.transform, "Divider02", 405f);
        CreateVerticalDivider(table.transform, "Divider03", 625f);

        TMP_Text countdown = GetOrCreateText(
            window.transform,
            "LobbyCountdownText",
            "7초 후 대기실로 돌아갑니다",
            22f,
            new Color32(170, 181, 194, 255),
            TextAlignmentOptions.Center,
            new Vector2(700f, 54f),
            new Vector2(450f, -645f));

        GameResultPopupController controller = gameUI.GetComponent<GameResultPopupController>();
        if (controller == null) controller = gameUI.AddComponent<GameResultPopupController>();
        controller.Configure(popup, title, names, roles, keywords, results, countdown);
        EditorUtility.SetDirty(controller);

        popup.transform.SetAsLastSibling();
        popup.SetActive(false);
    }

    private static void AddGameCoordinator(GameObject gameUI)
    {
        LiarPainterGameCoordinator coordinator = gameUI.GetComponent<LiarPainterGameCoordinator>();
        if (coordinator == null) coordinator = gameUI.AddComponent<LiarPainterGameCoordinator>();
        EditorUtility.SetDirty(coordinator);
    }

    private static void AddNetworkGameCoordinator(GameObject gameUI)
    {
        NetworkObject networkObject = gameUI.GetComponent<NetworkObject>();
        if (networkObject == null) networkObject = gameUI.AddComponent<NetworkObject>();
        LiarPainterNetworkGameCoordinator coordinator = gameUI.GetComponent<LiarPainterNetworkGameCoordinator>();
        if (coordinator == null) coordinator = gameUI.AddComponent<LiarPainterNetworkGameCoordinator>();
        EditorUtility.SetDirty(networkObject);
        EditorUtility.SetDirty(coordinator);
    }

    private static void AddGameHud(GameObject gameUI)
    {
        TMP_Text topic = gameUI.transform.Find("DrawingArea/DrawingHeader/TopicText")?.GetComponent<TMP_Text>();
        TMP_Text round = gameUI.transform.Find("DrawingArea/DrawingHeader/RoundText")?.GetComponent<TMP_Text>();
        DrawingCanvasLineRenderer drawingCanvas = gameUI.GetComponentInChildren<DrawingCanvasLineRenderer>(true);

        TMP_Text status = GetOrCreateText(
            gameUI.transform,
            "GameStatusText",
            "플레이어 준비를 기다리고 있습니다.",
            30f,
            new Color32(242, 229, 202, 255),
            TextAlignmentOptions.Center,
            new Vector2(1200f, 64f),
            new Vector2(960f, -155f));
        status.fontStyle = FontStyles.Bold;
        status.outlineWidth = 0.12f;
        status.outlineColor = new Color32(5, 9, 15, 220);

        LiarPainterGameHudController controller = gameUI.GetComponent<LiarPainterGameHudController>();
        if (controller == null) controller = gameUI.AddComponent<LiarPainterGameHudController>();
        controller.Configure(topic, round, status, drawingCanvas);
        controller.EnsureAssignmentPopup();
        EditorUtility.SetDirty(status);
        EditorUtility.SetDirty(controller);
    }

    private static void CreateTableHeader(Transform parent, string name, string value, float x, float width)
    {
        GetOrCreateText(
            parent,
            name,
            value,
            21f,
            new Color32(242, 229, 202, 255),
            TextAlignmentOptions.Center,
            new Vector2(width, 54f),
            new Vector2(x, -39f));
    }

    private static TMP_Text CreateTableColumn(Transform parent, string name, string value, float x, float width)
    {
        TMP_Text text = GetOrCreateText(
            parent,
            name,
            value,
            21f,
            new Color32(224, 228, 232, 255),
            TextAlignmentOptions.Top,
            new Vector2(width, 340f),
            new Vector2(x, -245f));
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.lineSpacing = 20f;
        return text;
    }

    private static void CreateVerticalDivider(Transform parent, string name, float x)
    {
        GameObject divider = GetOrCreateImage(parent, name, new Color32(61, 73, 88, 255), false);
        SetCentered(divider.GetComponent<RectTransform>(), new Vector2(2f, 410f), new Vector2(x, -215f));
    }

    private static Button GetOrCreateButton(Transform parent, string name, string label, Vector2 size, Vector2 position, Color color)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing.GetComponent<Button>();

        GameObject root = CreateImage(name, parent, color, true);
        SetCentered(root.GetComponent<RectTransform>(), size, position);
        Button button = root.AddComponent<Button>();
        button.targetGraphic = root.GetComponent<UnityEngine.UI.Image>();

        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.84f, 0.42f, 1f);
        colors.pressedColor = new Color(0.72f, 0.62f, 0.35f, 1f);
        colors.disabledColor = new Color(0.28f, 0.31f, 0.35f, 0.7f);
        button.colors = colors;

        TMP_Text text = GetOrCreateText(root.transform, "ButtonText", label, 18f, Color.white,
            TextAlignmentOptions.Center, size, size * new Vector2(0.5f, -0.5f));
        text.fontStyle = FontStyles.Bold;
        return button;
    }

    private static GameObject GetOrCreateImage(Transform parent, string name, Color color, bool raycastTarget)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing.gameObject;
        return CreateImage(name, parent, color, raycastTarget);
    }

    private static GameObject CreateImage(string name, Transform parent, Color color, bool raycastTarget)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        go.transform.SetParent(parent, false);
        UnityEngine.UI.Image image = go.GetComponent<UnityEngine.UI.Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return go;
    }

    private static TMP_Text GetOrCreateText(Transform parent, string name, string value, float size, Color color,
        TextAlignmentOptions alignment, Vector2 rectSize, Vector2 position)
    {
        Transform existing = parent.Find(name);
        TextMeshProUGUI text;
        if (existing != null)
        {
            text = existing.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            text = go.GetComponent<TextMeshProUGUI>();
        }

        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        if (font != null) text.font = font;
        SetCentered(text.rectTransform, rectSize, position);
        return text;
    }

    private static void SetCentered(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
