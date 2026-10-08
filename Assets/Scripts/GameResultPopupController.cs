using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GameResultPopupController : MonoBehaviour
{
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private TMP_Text victoryTitleText;
    [SerializeField] private TMP_Text playerNamesText;
    [SerializeField] private TMP_Text playerRolesText;
    [SerializeField] private TMP_Text playerKeywordsText;
    [SerializeField] private TMP_Text playerResultsText;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField, Min(0.1f)] private float displaySeconds = 7f;
    [SerializeField] private string lobbySceneName = "MainScene";

    private Coroutine closeRoutine;

    public void SetDisplaySeconds(float seconds)
    {
        displaySeconds = Mathf.Max(0.1f, seconds);
    }

    public void Configure(
        GameObject root,
        TMP_Text title,
        TMP_Text names,
        TMP_Text roles,
        TMP_Text keywords,
        TMP_Text results,
        TMP_Text countdown)
    {
        popupRoot = root;
        victoryTitleText = title;
        playerNamesText = names;
        playerRolesText = roles;
        playerKeywordsText = keywords;
        playerResultsText = results;
        countdownText = countdown;
        if (popupRoot != null) popupRoot.SetActive(false);
    }

    public void ShowResult(
        string victoryTitle,
        string playerNames,
        string playerRoles,
        string playerKeywords,
        string playerResults)
    {
        if (popupRoot == null) return;
        if (victoryTitleText != null) victoryTitleText.text = victoryTitle;
        if (playerNamesText != null) playerNamesText.text = playerNames;
        if (playerRolesText != null) playerRolesText.text = playerRoles;
        if (playerKeywordsText != null) playerKeywordsText.text = playerKeywords;
        if (playerResultsText != null) playerResultsText.text = playerResults;
        popupRoot.SetActive(true);
        popupRoot.transform.SetAsLastSibling();

        if (closeRoutine != null) StopCoroutine(closeRoutine);
        closeRoutine = StartCoroutine(ReturnToLobbyAfterDelay());
    }

    private IEnumerator ReturnToLobbyAfterDelay()
    {
        float remaining = displaySeconds;
        while (remaining > 0f)
        {
            if (countdownText != null)
                countdownText.text = $"{Mathf.CeilToInt(remaining)}초 후 대기실로 돌아갑니다";
            remaining -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (popupRoot != null) popupRoot.SetActive(false);
        SceneManager.LoadScene(lobbySceneName);
    }
}
