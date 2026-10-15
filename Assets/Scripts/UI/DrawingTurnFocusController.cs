using UnityEngine;

public sealed class DrawingTurnFocusController : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGlow;
    [SerializeField] private UnityEngine.UI.Image drawingAreaBackground;
    private bool drawingActive;
    [SerializeField, Min(0.01f)] private float fadeSeconds = 0.3f;
    [SerializeField, Min(0.2f)] private float pulseSeconds = 2.6f;
    private PlayerSlotView[] slots;
    private float visibility;
    private float pulseTime;
    private bool myTurn;
    private LiarPainterNetworkGameCoordinator coordinator;

    private void Awake()
    {
        coordinator = GetComponent<LiarPainterNetworkGameCoordinator>();
        if (drawingAreaBackground == null && canvasGlow != null)
            drawingAreaBackground = canvasGlow.transform.parent.GetComponent<UnityEngine.UI.Image>();
    }

    private void OnEnable()
    {
        if (coordinator == null) coordinator = GetComponent<LiarPainterNetworkGameCoordinator>();
        if (coordinator == null) return;
        coordinator.LocalDrawingPermissionChanged += HandleDrawingPermission;
        coordinator.Phase.OnValueChanged += HandlePhaseChanged;
        coordinator.ReturnedToWaitingRoom += HideCanvasGlow;
        HandleDrawingPermission(coordinator.LocalCanDraw);
    }

    private void HandleDrawingPermission(bool allowed)
    {
        bool drawingPhase = coordinator != null &&
            (coordinator.Phase.Value == LiarPainterGamePhase.Drawing ||
             coordinator.Phase.Value == LiarPainterGamePhase.FinalDefense);
        drawingActive = drawingPhase;
        SetCanvasGlow(drawingPhase && allowed);
    }

    private void HandlePhaseChanged(LiarPainterGamePhase previous, LiarPainterGamePhase current)
    {
        HandleDrawingPermission(coordinator.LocalCanDraw);
    }

    private void HideCanvasGlow()
    {
        drawingActive = false;
        SetCanvasGlow(false);
    }

    private void SetCanvasGlow(bool visible)
    {
        if (visible && !myTurn) pulseTime = 0f;
        myTurn = visible;
        if (canvasGlow != null && myTurn && !canvasGlow.gameObject.activeSelf)
            canvasGlow.gameObject.SetActive(true);
    }

    public void SetTurnPresentation(bool drawingPhase, bool canDraw)
    {
        drawingActive = drawingPhase;
        slots = GetComponentsInChildren<PlayerSlotView>(true);
        SetCanvasGlow(drawingPhase && canDraw);
        foreach (var slot in slots) slot.SetTurnDimmed(drawingPhase && !myTurn);
    }

    private void Update() => Animate(Time.unscaledDeltaTime);

    private void Animate(float delta)
    {
        if (drawingAreaBackground != null)
        {
            Color color = drawingAreaBackground.color;
            color.a = Mathf.MoveTowards(color.a, drawingActive && !myTurn ? 60f / 255f : 1f,
                delta / Mathf.Max(0.01f, fadeSeconds));
            drawingAreaBackground.color = color;
        }
        visibility = Mathf.MoveTowards(visibility, myTurn ? 1f : 0f,
            delta / Mathf.Max(0.01f, fadeSeconds));
        pulseTime += delta;
        if (canvasGlow == null) return;
        float blink = 0.5f + 0.5f * Mathf.Cos(pulseTime * Mathf.PI * 2f / Mathf.Max(0.2f, pulseSeconds));
        canvasGlow.alpha = Mathf.SmoothStep(0f, 1f, visibility) * Mathf.SmoothStep(0f, 1f, blink);
    }

    private void OnDisable()
    {
        if (coordinator != null)
        {
            coordinator.LocalDrawingPermissionChanged -= HandleDrawingPermission;
            coordinator.Phase.OnValueChanged -= HandlePhaseChanged;
            coordinator.ReturnedToWaitingRoom -= HideCanvasGlow;
        }
        visibility = 0f;
        myTurn = false;
        drawingActive = false;
        if (drawingAreaBackground != null)
        {
            Color color = drawingAreaBackground.color;
            color.a = 1f;
            drawingAreaBackground.color = color;
        }
        if (canvasGlow != null) canvasGlow.alpha = 0f;
        if (slots != null) foreach (var slot in slots) slot.SetTurnDimmed(false);
    }
}
