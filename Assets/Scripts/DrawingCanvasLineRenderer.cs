using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public sealed class DrawingCanvasLineRenderer : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private const int MaxNetworkPointsPerStroke = 96;
    // Shared drawing space: never derive network geometry from a window's layout/scale.
    [SerializeField] private Vector2Int drawingReferenceSize = new Vector2Int(1040, 585);
    private int DrawingWidth => Mathf.Max(1, drawingReferenceSize.x);
    private int DrawingHeight => Mathf.Max(1, drawingReferenceSize.y);
    private const float DrawingWorldHeight = 10f;
    private float DrawingAspect => (float)DrawingWidth / DrawingHeight;

    // Capture the authored UI size in the Editor. Every client uses the same
    // serialized reference, regardless of its window resolution or Canvas scale.
    [ContextMenu("Match Drawing Reference To Current UI")]
    public void MatchDrawingReferenceToCurrentUI()
    {
        if (Application.isPlaying) return;
        Rect rect = GetComponent<RectTransform>().rect;
        if (rect.width <= 0f || rect.height <= 0f) return;
        drawingReferenceSize = new Vector2Int(Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height));
    }
    [SerializeField] private DrawingToolSettings toolSettings;
    [SerializeField] private LineRenderer strokeTemplate;
    [SerializeField] private float cameraOrthoSize = 5f;
    [SerializeField] private int strokeLayer = 31;
    [SerializeField] private bool inputEnabled = true;
    [SerializeField, Min(0.02f)] private float networkSendInterval = 0.05f;
    private float nextNetworkSendTime;
    private bool strokeDirty;
    private LineRenderer appraisalOutline;
    private readonly Dictionary<string, LineRenderer> networkLines = new();

    private readonly List<Vector3> currentPoints = new();
    private RectTransform drawingRect;
    private Canvas rootCanvas;
    private Camera drawingCamera;
    private RenderTexture drawingTexture;
    private RawImage outputImage;
    private Transform strokeContainer;
    private LineRenderer currentLine;
    private Material lineMaterial;
    private int strokeSequence;
    private bool pointerHeld;
    private string activePlayerId = string.Empty;

    public event Action<NetworkDrawingStroke> NetworkStrokeCompleted;
    public event Action<NetworkDrawingStroke> NetworkStrokeUpdated;
    public event Action NetworkUndoRequested;

    public int StrokeCount => strokeContainer != null ? strokeContainer.childCount : 0;
    public float WorldUnitsPerPixel { get; private set; }

    public void SetDrawingPermission(bool canDraw, string playerId)
    {
        if (!canDraw && currentLine != null) FinishStroke();
        inputEnabled = canDraw;
        activePlayerId = canDraw ? playerId ?? string.Empty : string.Empty;
        if (!canDraw) pointerHeld = false;
    }

    public void ClearCanvas()
    {
        HighlightAppraisal(null);
        currentLine = null;
        currentPoints.Clear();
        pointerHeld = false;
        strokeDirty = false;
        networkLines.Clear();
        if (strokeContainer != null)
            for (int i = strokeContainer.childCount - 1; i >= 0; i--)
            {
                GameObject stroke = strokeContainer.GetChild(i).gameObject;
                stroke.SetActive(false);
                Destroy(stroke);
            }
        strokeSequence = 0;
        if (drawingTexture != null)
        {
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = drawingTexture;
                GL.Clear(true, true, Color.clear);
            }
            finally { RenderTexture.active = previous; }
        }
    }

    private void Awake()
    {
        drawingRect = GetComponent<RectTransform>();
        var inputGraphic = GetComponent<UnityEngine.UI.Image>();
        if (inputGraphic != null)
        {
            // Background alpha is cosmetic; keep the drawing rectangle clickable.
            inputGraphic.enabled = true;
            inputGraphic.raycastTarget = true;
            inputGraphic.alphaHitTestMinimumThreshold = 0f;
        }
        rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        if (toolSettings == null) toolSettings = GetComponentInParent<DrawingToolSettings>();
        if (strokeTemplate == null) strokeTemplate = GetComponent<LineRenderer>();
        // The authored renderer supplies style only. Each mouse press creates a separate stroke.
        if (strokeTemplate != null) strokeTemplate.enabled = false;
        CreateDrawingSurface();
    }

    private void Update()
    {
        if (currentLine == null || !strokeDirty || Time.unscaledTime < nextNetworkSendTime) return;
        SendStrokeUpdate();
    }

    private void SendStrokeUpdate()
    {
        if (currentLine == null || currentPoints.Count < 2) return;
        NetworkStrokeUpdated?.Invoke(CreateNetworkStroke(currentLine, currentPoints));
        strokeDirty = false;
        nextNetworkSendTime = Time.unscaledTime + Mathf.Max(0.02f, networkSendInterval);
    }

    private void CreateDrawingSurface()
    {
        drawingTexture = new RenderTexture(DrawingWidth, DrawingHeight, 24, RenderTextureFormat.ARGB32)
        {
            name = "DrawingCanvasTexture",
            antiAliasing = 4,
            filterMode = FilterMode.Bilinear
        };
        drawingTexture.Create();

        outputImage = transform.Find("LineRendererOutput")?.GetComponent<RawImage>();
        if (outputImage == null)
        {
            Debug.LogError("DrawingCanvas requires an authored LineRendererOutput RawImage.", this);
            return;
        }
        outputImage.texture = drawingTexture;
        outputImage.canvasRenderer.cullTransparentMesh = false;
        outputImage.raycastTarget = false;

        GameObject cameraObject = new("DrawingLineCamera");
        cameraObject.transform.position = new Vector3(10000f, 10000f, -10f);
        drawingCamera = cameraObject.AddComponent<Camera>();
        drawingCamera.orthographic = true;
        drawingCamera.orthographicSize = DrawingWorldHeight * 0.5f;
        drawingCamera.clearFlags = CameraClearFlags.SolidColor;
        drawingCamera.backgroundColor = Color.clear;
        drawingCamera.cullingMask = 1 << strokeLayer;
        drawingCamera.targetTexture = drawingTexture;
        drawingCamera.aspect = DrawingAspect;
        drawingCamera.allowHDR = false;
        drawingCamera.allowMSAA = true;

        GameObject containerObject = new("Strokes");
        containerObject.layer = strokeLayer;
        containerObject.transform.position = new Vector3(10000f, 10000f, 0f);
        strokeContainer = containerObject.transform;

        WorldUnitsPerPixel = DrawingWorldHeight / DrawingHeight;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        lineMaterial = new Material(shader) { name = "RuntimeDrawingLineMaterial" };
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (NetworkChatUIController.IsTyping || !inputEnabled || eventData.button != PointerEventData.InputButton.Left || toolSettings == null) return;
        if (!inputEnabled || !TryGetWorldPoint(eventData, out Vector3 point)) return;
        pointerHeld = true;

        StartStroke(point);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!inputEnabled || !pointerHeld || eventData.button != PointerEventData.InputButton.Left) return;
        if (!TryGetWorldPoint(eventData, out Vector3 point)) return;

        if (currentLine == null) return;

        float minimumDistance = WorldUnitsPerPixel * 1.5f;
        if (currentPoints.Count > 0 && Vector3.Distance(currentPoints[^1], point) < minimumDistance) return;
        AddPoint(point);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        pointerHeld = false;
        if (currentLine != null) FinishStroke();
    }

    private LineRenderer CreateLine(GameObject strokeObject)
    {
        LineRenderer line = strokeObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.sharedMaterial = strokeTemplate != null && strokeTemplate.sharedMaterial != null
            ? strokeTemplate.sharedMaterial : lineMaterial;
        line.textureMode = strokeTemplate != null ? strokeTemplate.textureMode : LineTextureMode.Stretch;
        line.alignment = LineAlignment.View;
        line.numCapVertices = strokeTemplate != null ? Mathf.Max(8, strokeTemplate.numCapVertices) : 8;
        line.numCornerVertices = strokeTemplate != null ? Mathf.Max(8, strokeTemplate.numCornerVertices) : 8;
        if (strokeTemplate != null) line.sortingLayerID = strokeTemplate.sortingLayerID;
        line.enabled = true;
        line.positionCount = 0;
        return line;
    }

    private void StartStroke(Vector3 point)
    {
        GameObject strokeObject = new($"Stroke_{++strokeSequence:0000}");
        strokeObject.layer = strokeLayer;
        strokeObject.transform.SetParent(strokeContainer, false);
        strokeObject.AddComponent<DrawingStrokeOwner>().Initialize(strokeSequence, activePlayerId);

        currentLine = CreateLine(strokeObject);
        currentLine.sortingOrder = strokeSequence;

        Color color = toolSettings.SelectedColor;
        currentLine.startColor = color;
        currentLine.endColor = color;

        float worldWidth = Mathf.Max(WorldUnitsPerPixel, toolSettings.BrushSize * WorldUnitsPerPixel);
        currentLine.startWidth = worldWidth;
        currentLine.endWidth = worldWidth;

        currentPoints.Clear();
        AddPoint(point);
        AddPoint(point);
        SendStrokeUpdate();
    }

    private void AddPoint(Vector3 point)
    {
        currentPoints.Add(point - strokeContainer.position);
        currentLine.positionCount = currentPoints.Count;
        currentLine.SetPositions(currentPoints.ToArray());
        strokeDirty = true;
    }

    private void FinishStroke()
    {
        LineRenderer finishedLine = currentLine;
        if (currentPoints.Count < 2)
        {
            Destroy(finishedLine.gameObject);
        }
        else if (NetworkStrokeCompleted != null)
        {
            NetworkStrokeCompleted.Invoke(CreateNetworkStroke(finishedLine, currentPoints));
            // Keep local feedback; the authoritative echo updates this same renderer.
        }

        currentLine = null;
        currentPoints.Clear();
        strokeDirty = false;
    }

    public void UndoLatestOwnStroke()
    {
        if (!inputEnabled || string.IsNullOrEmpty(activePlayerId) || strokeContainer == null) return;
        if (currentLine != null) FinishStroke();
        pointerHeld = false;
        if (NetworkUndoRequested != null)
        {
            NetworkUndoRequested.Invoke();
            return;
        }
        DrawingStrokeOwner latest = null;
        foreach (var owner in strokeContainer.GetComponentsInChildren<DrawingStrokeOwner>())
            if (owner.OwnerPlayerId == activePlayerId && (latest == null || owner.StrokeId > latest.StrokeId)) latest = owner;
        if (latest != null) RemoveNetworkStroke(latest.StrokeId);
    }

    public void ApplyNetworkStroke(NetworkDrawingStroke stroke)
    {
        if (stroke == null || stroke.points == null || stroke.points.Length < 2 || strokeContainer == null) return;
        string key = $"{stroke.ownerPlayerId}:{stroke.clientStrokeId}";
        networkLines.TryGetValue(key, out LineRenderer line);
        if (line != null)
        {
            line.GetComponent<DrawingStrokeOwner>().Initialize(stroke.strokeId, stroke.ownerPlayerId);
            // Never rewind the sender's in-progress line to an older network snapshot.
            if (line == currentLine) return;
        }
        else
        {
        GameObject strokeObject = new($"Stroke_{stroke.strokeId:0000}");
        strokeObject.layer = strokeLayer;
        strokeObject.transform.SetParent(strokeContainer, false);
        strokeObject.AddComponent<DrawingStrokeOwner>().Initialize(stroke.strokeId, stroke.ownerPlayerId);

        line = CreateLine(strokeObject);
        networkLines[key] = line;
        }
        line.sortingOrder = stroke.strokeId;
        line.startColor = stroke.color;
        line.endColor = stroke.color;
        line.startWidth = stroke.width;
        line.endWidth = stroke.width;
        line.positionCount = stroke.points.Length;
        line.SetPositions(stroke.points);
        strokeSequence = Mathf.Max(strokeSequence, stroke.strokeId);
    }

    public void RemoveNetworkStroke(int strokeId)
    {
        if (strokeContainer == null || strokeId <= 0) return;
        foreach (DrawingStrokeOwner owner in strokeContainer.GetComponentsInChildren<DrawingStrokeOwner>(true))
        {
            if (owner.StrokeId != strokeId) continue;
            owner.gameObject.SetActive(false);
            Destroy(owner.gameObject);
            return;
        }
    }

    private NetworkDrawingStroke CreateNetworkStroke(LineRenderer line, IReadOnlyList<Vector3> sourcePoints)
    {
        string ownerId = line.GetComponent<DrawingStrokeOwner>()?.OwnerPlayerId ?? activePlayerId;
        int clientStrokeId = line.GetInstanceID();
        networkLines[$"{ownerId}:{clientStrokeId}"] = line;
        int outputCount = Mathf.Min(sourcePoints.Count, MaxNetworkPointsPerStroke);
        Vector3[] points = new Vector3[outputCount];
        for (int i = 0; i < outputCount; i++)
        {
            int sourceIndex = outputCount == 1
                ? 0
                : Mathf.RoundToInt(i * (sourcePoints.Count - 1f) / (outputCount - 1f));
            Vector3 point = sourcePoints[sourceIndex];
            points[i] = point.sqrMagnitude > 10000f ? Vector3.ClampMagnitude(point, 100f) : point;
        }

        return new NetworkDrawingStroke
        {
            clientStrokeId = clientStrokeId,
            ownerPlayerId = ownerId,
            color = line.startColor,
            width = line.startWidth,
            points = points
        };
    }

    public bool TryPickAppraisal(PointerEventData pointer, out LineRenderer picked, out Vector3 anchor)
    {
        picked = null;
        anchor = default;
        if (strokeContainer == null || !TryGetWorldPoint(pointer, out Vector3 world)) return false;
        float nearest = float.MaxValue;
        foreach (var line in strokeContainer.GetComponentsInChildren<LineRenderer>())
        {
            if (line == appraisalOutline || line.GetComponent<DrawingStrokeOwner>() == null) continue;
            for (int i = 1; i < line.positionCount; i++)
            {
                Vector3 a = line.transform.TransformPoint(line.GetPosition(i - 1));
                Vector3 b = line.transform.TransformPoint(line.GetPosition(i));
                Vector3 ab = b - a;
                Vector3 closest = a + ab * (ab.sqrMagnitude > 0 ? Mathf.Clamp01(Vector3.Dot(world - a, ab) / ab.sqrMagnitude) : 0);
                float distance = Vector3.Distance(world, closest);
                if (distance < nearest && distance <= line.startWidth * 0.5f + WorldUnitsPerPixel * 8)
                { nearest = distance; picked = line; anchor = closest; }
            }
        }
        return picked != null;
    }

    public Vector2 DrawingWorldToUI(Vector3 world)
    {
        Vector3 local = world - strokeContainer.position;
        return new Vector2((local.x / (DrawingWorldHeight * DrawingAspect) + .5f) * drawingRect.rect.width + drawingRect.rect.xMin,
            (local.y / DrawingWorldHeight + .5f) * drawingRect.rect.height + drawingRect.rect.yMin);
    }

    public void HighlightAppraisal(LineRenderer line)
    {
        if (appraisalOutline != null) { appraisalOutline.gameObject.SetActive(false); Destroy(appraisalOutline.gameObject); appraisalOutline = null; }
        if (line == null) return;
        var go = new GameObject("AppraisalOutline");
        go.layer = strokeLayer;
        go.transform.SetParent(strokeContainer, false);
        appraisalOutline = CreateLine(go);
        appraisalOutline.positionCount = line.positionCount;
        var points = new Vector3[line.positionCount];
        line.GetPositions(points);
        appraisalOutline.SetPositions(points);
        appraisalOutline.startWidth = appraisalOutline.endWidth = line.startWidth + WorldUnitsPerPixel * 6;
        appraisalOutline.startColor = appraisalOutline.endColor = new Color32(255, 192, 40, 255);
        appraisalOutline.sortingOrder = line.sortingOrder - 1;
    }

    private bool TryGetWorldPoint(PointerEventData eventData, out Vector3 worldPoint)
    {
        worldPoint = default;
        Camera eventCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera
            : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(drawingRect, eventData.position, eventCamera, out Vector2 localPoint)) return false;
        if (!drawingRect.rect.Contains(localPoint)) return false;

        Rect rect = drawingRect.rect;
        float normalizedX = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);
        float normalizedY = Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y);
        worldPoint = strokeContainer.position + new Vector3(
            (normalizedX - 0.5f) * DrawingWorldHeight * DrawingAspect,
            (normalizedY - 0.5f) * DrawingWorldHeight, 0f);
        return true;
    }

    private void OnDestroy()
    {
        if (drawingCamera != null) Destroy(drawingCamera.gameObject);
        if (strokeContainer != null) Destroy(strokeContainer.gameObject);
        if (drawingTexture != null)
        {
            drawingTexture.Release();
            Destroy(drawingTexture);
        }
        if (lineMaterial != null) Destroy(lineMaterial);
    }
}

[Serializable]
public sealed class NetworkDrawingStroke
{
    public int clientStrokeId;
    public int strokeId;
    public string ownerPlayerId;
    public Color color;
    public float width;
    public Vector3[] points;
}
