using TMPro;
using UnityEngine;

public sealed class GameResultPopupController : MonoBehaviour
{
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private TMP_Text victoryTitleText;
    [SerializeField] private TMP_Text playerNamesText;
    [SerializeField] private TMP_Text playerRolesText;
    [SerializeField] private TMP_Text playerKeywordsText;
    [SerializeField] private TMP_Text playerResultsText;
    [SerializeField] private TMP_Text countdownText;

    public void HideResult()
    {
        if (popupRoot != null) popupRoot.SetActive(false);
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
        string playerResults,
        bool managedByNetwork = false)
    {
        if (popupRoot == null) return;
        if (victoryTitleText != null) victoryTitleText.text = victoryTitle;
        if (playerNamesText != null) playerNamesText.text = playerNames;
        if (playerRolesText != null) playerRolesText.text = playerRoles;
        if (playerKeywordsText != null) playerKeywordsText.text = playerKeywords;
        if (playerResultsText != null) playerResultsText.text = playerResults;
        popupRoot.SetActive(true);
        popupRoot.transform.SetAsLastSibling();

        if (countdownText != null) countdownText.gameObject.SetActive(false);
    }

}
