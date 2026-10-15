using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(DrawingCanvasLineRenderer))]
public sealed class AppraisalInteractionController : MonoBehaviour, IPointerMoveHandler, IPointerExitHandler, IPointerDownHandler
{
    private DrawingCanvasLineRenderer drawing;
    private LiarPainterNetworkGameCoordinator coordinator;
    private LineRenderer hovered;
    private TMP_Text label;
    private RectTransform labelRoot;
    private UnityEngine.UI.Image connector;
    private Vector3 anchor;
    private bool used, pending;
    private int requestedStroke;

    private void Awake()
    {
        drawing = GetComponent<DrawingCanvasLineRenderer>();
        coordinator = GetComponentInParent<LiarPainterNetworkGameCoordinator>();
        EnsureUI();
    }

    public void EnsureUI()
    {
        labelRoot = transform.Find("AppraisalLabel") as RectTransform;
        label = labelRoot != null ? labelRoot.GetComponentInChildren<TMP_Text>(true) : null;
        connector = transform.Find("AppraisalConnector")?.GetComponent<UnityEngine.UI.Image>();
        if (label != null) label.raycastTarget = false;
        if (labelRoot != null) labelRoot.gameObject.SetActive(false);
        if (connector != null) { connector.raycastTarget = false; connector.gameObject.SetActive(false); }
    }

    private void OnEnable()
    {
        if (coordinator != null)
        {
            coordinator.AppraisalCompleted += HandleCompleted;
            coordinator.ReturnedToWaitingRoom += ResetInteraction;
            coordinator.Phase.OnValueChanged += PhaseChanged;
        }
    }

    private void OnDisable()
    {
        if (coordinator != null)
        {
            coordinator.AppraisalCompleted -= HandleCompleted;
            coordinator.ReturnedToWaitingRoom -= ResetInteraction;
            coordinator.Phase.OnValueChanged -= PhaseChanged;
        }
        ResetInteraction();
    }

    private bool CanAppraise()
    {
        var obj = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        return !used && !pending && coordinator != null && coordinator.IsSpawned &&
            coordinator.Phase.Value == LiarPainterGamePhase.Discussion && obj != null &&
            obj.TryGetComponent(out LiarPainterNetworkPlayer player) && player.HasLocalAssignment &&
            player.LocalAssignment.VisibleRole == LiarPainterRole.Appraiser;
    }

    public void OnPointerMove(PointerEventData data)
    {
        if (!CanAppraise()) return;
        if (!drawing.TryPickAppraisal(data, out var line, out var point)) { HideHover(); return; }
        if (hovered != line) { hovered = line; drawing.HighlightAppraisal(line); }
        anchor = point;
        labelRoot.gameObject.SetActive(true);
        SetLabelText("눌러서 감정하기");
        labelRoot.anchoredPosition = ClampLabel(drawing.DrawingWorldToUI(point) + new Vector2(160, 42));
        labelRoot.SetAsLastSibling();
        labelRoot.gameObject.SetActive(true);
    }

    public void OnPointerDown(PointerEventData data)
    {
        if (data.button != PointerEventData.InputButton.Left || !CanAppraise()) return;
        OnPointerMove(data);
        if (hovered == null) return;
        requestedStroke = hovered.GetComponent<DrawingStrokeOwner>().StrokeId;
        pending = true;
        SetLabelText("감정 중…");
        labelRoot.anchoredPosition = ClampLabel(labelRoot.anchoredPosition);
        coordinator.RequestAppraisal(requestedStroke);
    }

    private Vector2 ClampLabel(Vector2 position)
    {
        Rect rect = ((RectTransform)transform).rect;
        Vector2 half = labelRoot.rect.size * .5f;
        return new Vector2(Mathf.Clamp(position.x, rect.xMin + half.x, rect.xMax - half.x),
            Mathf.Clamp(position.y, rect.yMin + half.y, rect.yMax - half.y));
    }

    private void SetLabelText(string text)
    {
        label.text = text;
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(labelRoot);
    }

    private void HandleCompleted(int strokeId, string name, bool success)
    {
        if (!pending || requestedStroke != strokeId) return;
        pending = false;
        if (!success) { HideHover(); return; }
        used = true;
        if (UIAudioManager.Instance != null) UIAudioManager.Instance.PlayAppraisalConfirm();
        SetLabelText($"{name}의 그림");
        labelRoot.anchoredPosition = ClampLabel(labelRoot.anchoredPosition);
        Vector2 from = drawing.DrawingWorldToUI(anchor);
        Vector2 to = labelRoot.anchoredPosition;
        var rect = connector.rectTransform;
        rect.anchoredPosition = (from + to) * .5f;
        rect.sizeDelta = new Vector2(Vector2.Distance(from, to), rect.sizeDelta.y);
        rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
        connector.gameObject.SetActive(true);
        labelRoot.SetAsLastSibling();
    }

    public void OnPointerExit(PointerEventData _) { if (!used && !pending) HideHover(); }
    private void HideHover()
    {
        hovered = null;
        drawing?.HighlightAppraisal(null);
        if (labelRoot != null) labelRoot.gameObject.SetActive(false);
    }
    private void PhaseChanged(LiarPainterGamePhase previous, LiarPainterGamePhase current)
    {
        if (current == LiarPainterGamePhase.Lobby || current == LiarPainterGamePhase.RoleReveal || current == LiarPainterGamePhase.QuestionReveal) ResetInteraction();
        else if (current != LiarPainterGamePhase.Discussion && !used) { pending = false; HideHover(); }
    }
    private void ResetInteraction()
    {
        used = pending = false;
        HideHover();
        if (connector != null) connector.gameObject.SetActive(false);
    }
}
