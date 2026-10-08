using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Displays the join code in the existing GameTitleText.</summary>
public sealed class LiarPainterSessionHud : MonoBehaviour
{
    private TMP_Text titleText;
    private string originalTitle;
    private Coroutine refreshRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallSceneHook()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        AttachToGameScene(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => AttachToGameScene(scene);

    private static void AttachToGameScene(Scene scene)
    {
        if (scene.name != "GameScene") return;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                if (canvas.transform.Find("GameUI/Header/GameTitleText") == null) continue;
                if (canvas.GetComponent<LiarPainterSessionHud>() == null)
                    canvas.gameObject.AddComponent<LiarPainterSessionHud>();
                return;
            }
        }
    }

    private void Awake()
    {
        titleText = transform.Find("GameUI/Header/GameTitleText")?.GetComponent<TMP_Text>();
        if (titleText != null) originalTitle = titleText.text;
    }

    private void OnEnable() => refreshRoutine = StartCoroutine(RefreshUntilReady());

    private void OnDisable()
    {
        if (refreshRoutine != null) StopCoroutine(refreshRoutine);
        refreshRoutine = null;
        if (titleText != null) titleText.text = originalTitle;
    }

    private IEnumerator RefreshUntilReady()
    {
        while (isActiveAndEnabled)
        {
            string code = LiarPainterSessionManager.Instance?.JoinCode;
            if (titleText != null)
                titleText.text = string.IsNullOrWhiteSpace(code) ? originalTitle : $"참가 코드  {code}";
            yield return new WaitForSecondsRealtime(0.5f);
        }
    }
}
