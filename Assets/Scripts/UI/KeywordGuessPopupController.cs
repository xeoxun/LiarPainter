using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public sealed class KeywordGuessPopupController : MonoBehaviour
{
    [SerializeField] private GameObject popup;
    [SerializeField] private TMP_InputField input;
    [SerializeField] private TMP_Text feedback;
    [SerializeField] private Button submitButton;
    private LiarPainterNetworkGameCoordinator coordinator;
    private bool pending;

    private void Awake()
    {
        coordinator = GetComponent<LiarPainterNetworkGameCoordinator>();
        if (input != null) { input.characterLimit = 32; input.onSubmit.AddListener(Submit); }
        if (submitButton != null) submitButton.onClick.AddListener(Submit);
        if (popup != null) popup.SetActive(false);
    }

    private void OnEnable()
    {
        if (coordinator != null) coordinator.KeywordGuessCompleted += HandleFeedback;
    }

    private void OnDisable()
    {
        if (coordinator != null) coordinator.KeywordGuessCompleted -= HandleFeedback;
        if (popup != null) popup.SetActive(false);
        pending = false;
    }

    private void Update()
    {
        if (popup == null) return;
        var manager = NetworkManager.Singleton;
        var obj = manager != null && manager.LocalClient != null ? manager.LocalClient.PlayerObject : null;
        bool visible = coordinator != null && coordinator.IsSpawned &&
            coordinator.Phase.Value == LiarPainterGamePhase.Discussion && obj != null &&
            obj.TryGetComponent<LiarPainterNetworkPlayer>(out var player) && player.HasLocalAssignment &&
            player.LocalAssignment.VisibleRole == LiarPainterRole.FakePainter;
        if (popup.activeSelf == visible) return;
        pending = false;
        if (visible)
        {
            input.text = string.Empty;
            feedback.text = "검증 시간이 끝나기 전까지 횟수 제한 없이 도전할 수 있습니다.";
            submitButton.interactable = true;
            popup.transform.SetAsLastSibling();
        }
        popup.SetActive(visible);
    }

    private void Submit(string _) => Submit();

    public void Submit()
    {
        if (pending || popup == null || !popup.activeInHierarchy || coordinator == null) return;
        if (string.IsNullOrWhiteSpace(input.text)) { feedback.text = "제시어를 입력하세요."; return; }
        pending = true;
        submitButton.interactable = false;
        feedback.text = "정답 확인 중…";
        coordinator.SubmitKeywordGuess(input.text);
    }

    private void HandleFeedback(bool correct)
    {
        pending = false;
        submitButton.interactable = true;
        feedback.text = correct ? "정답입니다! 사짜 화가 승리!" : "정답이 아닙니다. 다시 도전하세요.";
        if (!correct) { input.Select(); input.ActivateInputField(); }
    }
}
