using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class RoomBrowserCloseButtonBuilder
{
    private const string SpritePath = "Assets/Sprites/UI/RoomBrowserCloseButton.png";

    static RoomBrowserCloseButtonBuilder()
    {
        EditorApplication.delayCall += ApplyWhenReady;
    }

    private static void ApplyWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ApplyWhenReady;
            return;
        }
        if (!Application.isPlaying && System.IO.File.Exists(SpritePath)) Apply();
    }

    [MenuItem("Liar Painter/UI/Add Room Browser Close Button")]
    public static void Apply()
    {
        if (Application.isPlaying) return;
        AssetDatabase.ImportAsset(SpritePath);
        TextureImporter importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
        if (importer == null) return;
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (sprite == null) return;

        Scene scene = SceneManager.GetSceneByPath("Assets/Scenes/MainScene.unity");
        bool openedForEdit = !scene.IsValid() || !scene.isLoaded;
        if (openedForEdit) scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity", OpenSceneMode.Additive);
        try
        {
            Transform panel = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    if (child.name == "RoomBrowserPanel") panel = child;
            if (panel == null) return;
            Transform existing = panel.Find("CloseButton");
            if (existing != null) return;

            GameObject go = new GameObject("CloseButton", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            go.transform.SetParent(panel, false);
            go.layer = panel.gameObject.layer;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.sizeDelta = new Vector2(72f, 72f);
            rect.anchoredPosition = new Vector2(-24f, -24f);
            UnityEngine.UI.Image image = go.GetComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = true;
            UnityEngine.UI.Button button = go.GetComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            UnityEngine.UI.ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 0.9f, 0.65f);
            colors.pressedColor = new Color(0.65f, 0.65f, 0.65f);
            button.colors = colors;
            UnityEventTools.AddBoolPersistentListener(button.onClick, panel.gameObject.SetActive, false);
            go.transform.SetAsLastSibling();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("RoomBrowserPanel의 기존 배치를 유지하고 이미지 닫기 버튼을 추가했습니다.");
        }
        finally
        {
            if (openedForEdit) EditorSceneManager.CloseScene(scene, true);
        }
    }
}

public sealed class RoomBrowserCloseButtonImportHook : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (string path in imported)
            if (path == "Assets/Sprites/UI/RoomBrowserCloseButton.png")
            {
                EditorApplication.delayCall += RoomBrowserCloseButtonBuilder.Apply;
                break;
            }
    }
}
