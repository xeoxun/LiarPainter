using System.Linq;
using TMPro;
using UnityEngine;

public sealed class NicknamePopupController : MonoBehaviour
{
    public const string PlayerNameKey = "LiarPainter.PlayerName";

    [SerializeField] private GameObject popup;
    [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private UnityEngine.UI.Button confirmButton;
    [SerializeField] private TMP_Text errorText;

    public void Configure(
        GameObject popupRoot,
        TMP_InputField input,
        UnityEngine.UI.Button confirm,
        TMP_Text error)
    {
        popup = popupRoot;
        nicknameInput = input;
        confirmButton = confirm;
        errorText = error;
    }

    private void Awake()
    {
        if (popup == null) popup = transform.Find("NicknamePopup")?.gameObject;
        if (nicknameInput == null)
            nicknameInput = transform.Find("NicknamePopup/PopupWindow/NicknameInputField")?.GetComponent<TMP_InputField>();
        if (confirmButton == null)
            confirmButton = transform.Find("NicknamePopup/PopupWindow/ConfirmButton")?.GetComponent<UnityEngine.UI.Button>();
        if (errorText == null)
            errorText = transform.Find("NicknamePopup/PopupWindow/ErrorText")?.GetComponent<TMP_Text>();

        if (confirmButton != null) confirmButton.onClick.AddListener(ConfirmNickname);
        if (nicknameInput != null)
        {
            nicknameInput.characterLimit = 12;
            nicknameInput.onSubmit.AddListener(HandleSubmit);
        }

        string savedName = PlayerPrefs.GetString(PlayerNameKey, string.Empty).Trim();
        bool needsNickname = string.IsNullOrWhiteSpace(savedName);
        if (popup != null)
        {
            popup.SetActive(needsNickname);
            if (needsNickname) popup.transform.SetAsLastSibling();
        }

        if (errorText != null) errorText.text = string.Empty;
        if (needsNickname)
        {
            if (nicknameInput != null)
            {
                nicknameInput.text = string.Empty;
                nicknameInput.ActivateInputField();
            }
        }
        else
        {
            UpdatePlayerNameLabels(savedName);
        }
    }

    private void OnDestroy()
    {
        if (confirmButton != null) confirmButton.onClick.RemoveListener(ConfirmNickname);
        if (nicknameInput != null) nicknameInput.onSubmit.RemoveListener(HandleSubmit);
    }

    private void HandleSubmit(string _)
    {
        ConfirmNickname();
    }

    private void ConfirmNickname()
    {
        string nickname = nicknameInput != null ? nicknameInput.text.Trim() : string.Empty;
        if (nickname.Length < 2)
        {
            ShowError("닉네임은 2글자 이상 입력해 주세요.");
            return;
        }

        PlayerPrefs.SetString(PlayerNameKey, nickname);
        PlayerPrefs.Save();
        UpdatePlayerNameLabels(nickname);
        if (popup != null) popup.SetActive(false);
    }

    private void ShowError(string message)
    {
        if (errorText != null) errorText.text = message;
        nicknameInput?.ActivateInputField();
    }

    private void UpdatePlayerNameLabels(string nickname)
    {
        foreach (TMP_Text label in GetComponentsInChildren<TMP_Text>(true)
                     .Where(text => text.name == "PlayerNameText"))
        {
            label.text = $"PLAYER  |  {nickname}";
        }
    }
}
