using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DrawingToolSettings : MonoBehaviour
{
    public const float FixedBrushSize = 3f;
    public static readonly Color32[] PaletteColors =
    {
        new(255, 0, 0, 255), new(255, 128, 0, 255), new(255, 255, 0, 255),
        new(0, 180, 0, 255), new(0, 100, 255, 255), new(35, 35, 140, 255),
        new(150, 0, 210, 255), new(255, 105, 180, 255), new(0, 0, 0, 255), new(150, 75, 0, 255)
    };
    [SerializeField] private DrawingCanvasLineRenderer drawingCanvas;

    public Color32 SelectedColor { get; private set; } = new Color32(0, 0, 0, 255);
    public float BrushSize => FixedBrushSize;

    public void UndoLatestStroke()
    {
        if (drawingCanvas != null) drawingCanvas.UndoLatestOwnStroke();
    }

    public void SelectPaletteColor(int index)
    {
        if (index < 0 || index >= PaletteColors.Length) return;
        SelectedColor = PaletteColors[index];
    }
}