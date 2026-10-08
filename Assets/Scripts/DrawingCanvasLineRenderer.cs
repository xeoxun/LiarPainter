using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public sealed class DrawingCanvasLineRenderer : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private const int MaxNetworkPointsPerStroke = 96;
    [SerializeField] private DrawingToolSettings toolSettings;
    [SerializeField] private float cameraOrthoSize = 5f;
    [SerializeField] private int strokeLayer = 31;
    [SerializeField] private bool inputEnabled = true;

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
    private string activePlayerId = string.Empty;

    public event Action<NetworkDrawingStroke> NetworkStrokeCompleted;
    public event Action<int> NetworkStrokeEraseRequested;

    public int StrokeCount => strokeContainer != null ? strokeContainer.childCount : 0;
    public float WorldUnitsPerPixel { get; private set; }

    public void SetDrawingPermission(bool canDraw, string playerId)
    {
        inputEnabled = canDraw;
        activePlayerId = canDraw ? playerId ?? string.Empty : string.Empty;
        if (!canDraw && currentLine != null) FinishStroke();
    }

    public void ClearCanvas()
    {
        if (strokeContainer == null) return;
        for (int i = strokeContainer.childCount - 1; i >= 0; i--)
            Destroy(strokeContainer.GetChild(i).gameObject);
        strokeSequence = 0;
    }

    private void Awake()
    {
        drawingRect = GetComponent<RectTransform>();
        rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        if (toolSettings == null) toolSettings = GetComponentInParent<DrawingToolSettings>();
        CreateDrawingSurface();
    }

    private void CreateDrawingSurface()
    {
        int width = Mathf.Max(1, Mathf.RoundToInt(drawingRect.rect.width * (rootCanvas != null ? rootCanvas.scaleFactor : 1f)));
        int height = Mathf.Max(1, Mathf.RoundToInt(drawingRect.rect.height * (rootCanvas != null ? rootCanvas.scaleFactor : 1f)));

        drawingTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            name = "DrawingCanvasTexture",
            antiAliasing = 4,
            filterMode = FilterMode.Bilinear
        };
        drawingTexture.Create();

        GameObject outputObject = new("LineRendererOutput", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        outputObject.transform.SetParent(transform, false);
        RectTransform outputRect = outputObject.GetComponent<RectTransform>();
        outputRect.anchorMin = Vector2.zero;
        outputRect.anchorMax = Vector2.one;
        outputRect.offsetMin = Vector2.zero;
        outputRect.offsetMax = Vector2.zero;
        outputImage = outputObject.GetComponent<RawImage>();
        outputImage.texture = drawingTexture;
        outputImage.raycastTarget = false;

        GameObject cameraObject = new("DrawingLineCamera");
        cameraObject.transform.position = new Vector3(10000f, 10000f, -10f);
        drawingCamera = cameraObject.AddComponent<Camera>();
        drawingCamera.orthographic = true;
        drawingCamera.orthographicSize = cameraOrthoSize;
        drawingCamera.clearFlags = CameraClearFlags.SolidColor;
        drawingCamera.backgroundColor = Color.clear;
        drawingCamera.cullingMask = 1 << strokeLayer;
        drawingCamera.targetTexture = drawingTexture;
        drawingCamera.allowHDR = false;
        drawingCamera.allowMSAA = true;

        GameObject containerObject = new("Strokes");
        containerObject.layer = strokeLayer;
        containerObject.transform.position = new Vector3(10000f, 10000f, 0f);
        strokeContainer = containerObject.transform;

        WorldUnitsPerPixel = drawingCamera.orthographicSize * 2f / drawingTexture.height;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        lineMaterial = new Material(shader) { name = "RuntimeDrawingLineMaterial" };
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!inputEnabled || eventData.button != PointerEventData.InputButton.Left || toolSettings == null) return;
        if (!inputEnabled || !TryGetWorldPoint(eventData, out Vector3 point)) return;

        if (toolSettings.CurrentTool == DrawingToolMode.Pen) StartStroke(point);
        else if (toolSettings.CurrentTool == DrawingToolMode.PartialEraser) ErasePartial(point);
        else if (toolSettings.CurrentTool == DrawingToolMode.StrokeEraser) EraseStroke(point);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!TryGetWorldPoint(eventData, out Vector3 point)) return;

        if (toolSettings != null && toolSettings.CurrentTool == DrawingToolMode.PartialEraser)
        {
            ErasePartial(point);
            return;
        }

        if (currentLine == null) return;

        float minimumDistance = WorldUnitsPerPixel * 1.5f;
        if (currentPoints.Count > 0 && Vector3.Distance(currentPoints[^1], point) < minimumDistance) return;
        AddPoint(point);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || currentLine == null) return;
        FinishStroke();
    }

    private void StartStroke(Vector3 point)
    {
        GameObject strokeObject = new($"Stroke_{++strokeSequence:0000}");
        strokeObject.layer = strokeLayer;
        strokeObject.transform.SetParent(strokeContainer, false);
        strokeObject.AddComponent<DrawingStrokeOwner>().Initialize(strokeSequence, activePlayerId);

        currentLine = strokeObject.AddComponent<LineRenderer>();
        currentLine.useWorldSpace = false;
        currentLine.material = lineMaterial;
        currentLine.textureMode = LineTextureMode.Stretch;
        currentLine.alignment = LineAlignment.View;
        currentLine.numCapVertices = 8;
        currentLine.numCornerVertices = 8;
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
    }

    private void AddPoint(Vector3 point)
    {
        currentPoints.Add(point - strokeContainer.position);
        currentLine.positionCount = currentPoints.Count;
        currentLine.SetPositions(currentPoints.ToArray());
    }

    private void FinishStroke()
    {
        LineRenderer finishedLine = currentLine;
        if (currentPoints.Count < 3)
        {
            Destroy(finishedLine.gameObject);
        }
        else if (NetworkStrokeCompleted != null)
        {
            NetworkStrokeCompleted.Invoke(CreateNetworkStroke(finishedLine, currentPoints));
            // The server broadcasts the authoritative copy (including to the sender).
            Destroy(finishedLine.gameObject);
        }

        currentLine = null;
        currentPoints.Clear();
    }

    private void EraseStroke(Vector3 worldPoint)
    {
        LineRenderer closest = null;
        float closestDistance = float.MaxValue;
        foreach (LineRenderer line in strokeContainer.GetComponentsInChildren<LineRenderer>())
        {
            DrawingStrokeOwner owner = line.GetComponent<DrawingStrokeOwner>();
            if (owner != null && owner.OwnerPlayerId != activePlayerId) continue;
            Vector3[] points = new Vector3[line.positionCount];
            line.GetPositions(points);
            for (int i = 1; i < points.Length; i++)
            {
                float distance = DistanceToSegment(worldPoint, line.transform.TransformPoint(points[i - 1]), line.transform.TransformPoint(points[i]));
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = line;
                }
            }
        }

        if (closest != null && closestDistance <= closest.startWidth * 0.5f + WorldUnitsPerPixel * 8f)
        {
            DrawingStrokeOwner owner = closest.GetComponent<DrawingStrokeOwner>();
            if (NetworkStrokeEraseRequested != null && owner != null)
            {
                NetworkStrokeEraseRequested.Invoke(owner.StrokeId);
                return;
            }

            closest.gameObject.SetActive(false);
            Destroy(closest.gameObject);
        }
    }

    private void ErasePartial(Vector3 worldPoint)
    {
        float radius = toolSettings.EraserSize * WorldUnitsPerPixel * 0.5f;
        LineRenderer[] lines = strokeContainer.GetComponentsInChildren<LineRenderer>();
        foreach (LineRenderer line in lines)
        {
            DrawingStrokeOwner owner = line.GetComponent<DrawingStrokeOwner>();
            if (owner != null && owner.OwnerPlayerId != activePlayerId) continue;
            Vector3[] source = new Vector3[line.positionCount];
            line.GetPositions(source);
            List<List<Vector3>> segments = new();
            List<Vector3> current = new();

            foreach (Vector3 localPoint in source)
            {
                Vector3 point = line.transform.TransformPoint(localPoint);
                if (Vector3.Distance(point, worldPoint) <= radius)
                {
                    if (current.Count >= 2) segments.Add(current);
                    current = new List<Vector3>();
                }
                else
                {
                    current.Add(point);
                }
            }
            if (current.Count >= 2) segments.Add(current);
            if (segments.Count == 1 && segments[0].Count == source.Length) continue;

            DrawingStrokeOwner originalOwner = line.GetComponent<DrawingStrokeOwner>();
            bool networked = NetworkStrokeEraseRequested != null && NetworkStrokeCompleted != null && originalOwner != null;
            if (networked) NetworkStrokeEraseRequested.Invoke(originalOwner.StrokeId);

            foreach (List<Vector3> segment in segments)
            {
                LineRenderer replacement = CreateStrokeFromWorldPoints(segment, line);
                if (networked)
                {
                    NetworkStrokeCompleted.Invoke(CreateNetworkStroke(replacement, segment));
                    Destroy(replacement.gameObject);
                }
            }
            line.gameObject.SetActive(false);
            Destroy(line.gameObject);
        }
    }

    private LineRenderer CreateStrokeFromWorldPoints(List<Vector3> points, LineRenderer source)
    {
        GameObject strokeObject = new($"Stroke_{++strokeSequence:0000}");
        strokeObject.layer = strokeLayer;
        strokeObject.transform.SetParent(strokeContainer, false);
        DrawingStrokeOwner sourceOwner = source.GetComponent<DrawingStrokeOwner>();
        strokeObject.AddComponent<DrawingStrokeOwner>().Initialize(
            strokeSequence,
            sourceOwner != null ? sourceOwner.OwnerPlayerId : activePlayerId);
        LineRenderer line = strokeObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.material = lineMaterial;
        line.alignment = source.alignment;
        line.textureMode = source.textureMode;
        line.numCapVertices = source.numCapVertices;
        line.numCornerVertices = source.numCornerVertices;
        line.startColor = source.startColor;
        line.endColor = source.endColor;
        line.startWidth = source.startWidth;
        line.endWidth = source.endWidth;
        line.sortingOrder = strokeSequence;
        line.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++) line.SetPosition(i, points[i] - strokeContainer.position);
        return line;
    }

    public void ApplyNetworkStroke(NetworkDrawingStroke stroke)
    {
        if (stroke == null || stroke.points == null || stroke.points.Length < 2 || strokeContainer == null) return;
        RemoveNetworkStroke(stroke.strokeId);

        GameObject strokeObject = new($"Stroke_{stroke.strokeId:0000}");
        strokeObject.layer = strokeLayer;
        strokeObject.transform.SetParent(strokeContainer, false);
        strokeObject.AddComponent<DrawingStrokeOwner>().Initialize(stroke.strokeId, stroke.ownerPlayerId);

        LineRenderer line = strokeObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.material = lineMaterial;
        line.textureMode = LineTextureMode.Stretch;
        line.alignment = LineAlignment.View;
        line.numCapVertices = 8;
        line.numCornerVertices = 8;
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
            ownerPlayerId = activePlayerId,
            color = line.startColor,
            width = line.startWidth,
            points = points
        };
    }

    private static float DistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector3 segment = end - start;
        float lengthSquared = segment.sqrMagnitude;
        if (lengthSquared <= Mathf.Epsilon) return Vector3.Distance(point, start);
        float t = Mathf.Clamp01(Vector3.Dot(point - start, segment) / lengthSquared);
        return Vector3.Distance(point, start + segment * t);
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
        worldPoint = drawingCamera.ViewportToWorldPoint(new Vector3(normalizedX, normalizedY, 10f));
        worldPoint.z = strokeContainer.position.z;
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
    public int strokeId;
    public string ownerPlayerId;
    public Color color;
    public float width;
    public Vector3[] points;
}
