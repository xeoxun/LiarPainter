using System.Linq;
using TMPro;
using UnityEngine;

public sealed class NicknamePopupController : MonoBehaviour
{
    public const string PlayerNameKey = "LiarPainter.PlayerName";
    public const int MaximumNicknameLength = 10;
    public static bool IsValidNickname(string value) => !string.IsNullOrEmpty(value) &&
        value.Length >= 2 && value.Length <= MaximumNicknameLength &&
        !value.Any(character => char.IsWhiteSpace(character) || char.IsControl(character));
    public static string CurrentNickname { get; private set; } = string.Empty;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetNickname() => CurrentNickname = string.Empty;

    [SerializeField] private GameObject popup;
    [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private TMP_Text errorText;

    private void Awake()
    {
        if (popup == null) popup = transform.Find("NicknamePopup")?.gameObject;
        if (nicknameInput == null)
            nicknameInput = transform.Find("NicknamePopup/PopupWindow/NicknameInputField")?.GetComponent<TMP_InputField>();
        if (errorText == null)
            errorText = transform.Find("NicknamePopup/PopupWindow/ErrorText")?.GetComponent<TMP_Text>();

        if (nicknameInput != null)
        {
            nicknameInput.characterLimit = MaximumNicknameLength;
            nicknameInput.onValidateInput += ValidateNicknameCharacter;
            nicknameInput.onSubmit.AddListener(HandleSubmit);
        }

        string savedName = CurrentNickname;
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
        if (nicknameInput != null)
        {
            nicknameInput.onSubmit.RemoveListener(HandleSubmit);
            nicknameInput.onValidateInput -= ValidateNicknameCharacter;
        }
    }

    private char ValidateNicknameCharacter(string text, int index, char character) =>
        char.IsWhiteSpace(character) || char.IsControl(character) ? '\0' : character;

    private void HandleSubmit(string _)
    {
        ConfirmNickname();
    }

    public void ConfirmNickname()
    {
        string nickname = nicknameInput != null ? nicknameInput.text : string.Empty;
        if (!IsValidNickname(nickname))
        {
            ShowError("닉네임은 공백 없이 2~10글자로 입력해 주세요.");
            return;
        }

        CurrentNickname = nickname;
        PlayerPrefs.SetString(PlayerNameKey, nickname);
        PlayerPrefs.Save();
        UpdatePlayerNameLabels(nickname);
        foreach (LiarPainterNetworkPlayer player in FindObjectsByType<LiarPainterNetworkPlayer>(FindObjectsSortMode.None))
        {
            if (player.IsOwner && player.IsSpawned) player.UpdateLocalNickname(nickname);
        }
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
