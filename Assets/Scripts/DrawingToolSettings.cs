using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum DrawingToolMode
{
    Pen,
    PartialEraser,
    StrokeEraser
}

public sealed class DrawingToolSettings : MonoBehaviour
{
    [SerializeField] private GameObject palettePopup;
    [SerializeField] private Button paletteButton;
    [SerializeField] private Button partialEraserButton;
    [SerializeField] private Button strokeEraserButton;
    [SerializeField] private Button eraserMenuButton;
    [SerializeField] private GameObject eraserSubmenu;
    [SerializeField] private GameObject eraserSizePopup;
    [SerializeField] private Button[] eraserSizeButtons;
    [SerializeField] private Button closePaletteButton;
    [SerializeField] private Button applyPaletteButton;
    [SerializeField] private ColorWheelPicker colorWheelPicker;
    [SerializeField] private Slider brushSizeSlider;
    [SerializeField] private TMP_Text brushSizeValueText;
    [SerializeField] private Slider opacitySlider;
    [SerializeField] private TMP_Text opacityValueText;
    [SerializeField] private TMP_Text selectedToolText;
    [SerializeField] private Image colorPreview;
    [SerializeField] private Image toolbarColorPreview;

    private readonly Color normalButtonColor = new Color32(61, 73, 88, 255);
    private readonly Color selectedButtonColor = new Color32(38, 157, 178, 255);

    public Color32 SelectedColor { get; private set; } = new Color32(25, 25, 25, 255);
    public float BrushSize { get; private set; } = 10f;
    public float Opacity { get; private set; } = 1f;
    public float EraserSize { get; private set; } = 6f;
    public DrawingToolMode CurrentTool { get; private set; } = DrawingToolMode.Pen;

    public void Configure(
        GameObject popup,
        Button palette,
        Button partialEraser,
        Button strokeEraser,
        Button closePalette,
        Button applyPalette,
        ColorWheelPicker wheelPicker,
        Slider brushSize,
        TMP_Text brushSizeValue,
        Slider opacity,
        TMP_Text opacityValue,
        TMP_Text toolText,
        Image preview,
        Image toolbarPreview)
    {
        palettePopup = popup;
        paletteButton = palette;
        partialEraserButton = partialEraser;
        strokeEraserButton = strokeEraser;
        closePaletteButton = closePalette;
        applyPaletteButton = applyPalette;
        colorWheelPicker = wheelPicker;
        brushSizeSlider = brushSize;
        brushSizeValueText = brushSizeValue;
        opacitySlider = opacity;
        opacityValueText = opacityValue;
        selectedToolText = toolText;
        colorPreview = preview;
        toolbarColorPreview = toolbarPreview;
    }

    public void ConfigureEraserMenu(Button menuButton, GameObject submenu, GameObject sizePopup, Button[] sizeButtons)
    {
        eraserMenuButton = menuButton;
        eraserSubmenu = submenu;
        eraserSizePopup = sizePopup;
        eraserSizeButtons = sizeButtons;
    }

    private void Awake()
    {
        paletteButton?.onClick.AddListener(TogglePalette);
        eraserMenuButton?.onClick.AddListener(ToggleEraserSubmenu);
        partialEraserButton?.onClick.AddListener(OpenEraserSizePopup);
        strokeEraserButton?.onClick.AddListener(SelectStrokeEraser);
        if (eraserSizeButtons != null)
        {
            float[] sizes = { 2f, 4f, 6f, 8f, 10f };
            for (int i = 0; i < eraserSizeButtons.Length && i < sizes.Length; i++)
            {
                float size = sizes[i];
                eraserSizeButtons[i]?.onClick.AddListener(() => SelectPartialEraser(size));
            }
        }
        closePaletteButton?.onClick.AddListener(ClosePalette);
        applyPaletteButton?.onClick.AddListener(ApplyPalette);
        if (colorWheelPicker != null) colorWheelPicker.ColorChanged += OnColorChanged;
        brushSizeSlider?.onValueChanged.AddListener(UpdateBrushSize);
        opacitySlider?.onValueChanged.AddListener(UpdateOpacity);

        if (colorWheelPicker != null) OnColorChanged(colorWheelPicker.SelectedColor);
        UpdateBrushSize(brushSizeSlider != null ? brushSizeSlider.value : BrushSize);
        UpdateOpacity(opacitySlider != null ? opacitySlider.value : Opacity * 100f);
        SelectTool(DrawingToolMode.Pen);
    }

    public void SelectPen()
    {
        SelectTool(DrawingToolMode.Pen);
    }

    private void TogglePalette()
    {
        CloseEraserMenus();
        if (palettePopup != null)
        {
            palettePopup.SetActive(!palettePopup.activeSelf);
        }

        SelectTool(DrawingToolMode.Pen);
    }

    private void ToggleEraserSubmenu()
    {
        ClosePalette();
        if (eraserSubmenu == null) return;
        bool show = !eraserSubmenu.activeSelf;
        eraserSubmenu.SetActive(show);
        if (!show && eraserSizePopup != null) eraserSizePopup.SetActive(false);
    }

    private void OpenEraserSizePopup()
    {
        if (eraserSizePopup != null) eraserSizePopup.SetActive(true);
    }

    private void SelectPartialEraser(float size)
    {
        EraserSize = size;
        SelectTool(DrawingToolMode.PartialEraser);
        CloseEraserMenus();
    }

    private void SelectStrokeEraser()
    {
        SelectTool(DrawingToolMode.StrokeEraser);
        CloseEraserMenus();
    }

    private void CloseEraserMenus()
    {
        if (eraserSubmenu != null) eraserSubmenu.SetActive(false);
        if (eraserSizePopup != null) eraserSizePopup.SetActive(false);
    }

    private void ClosePalette()
    {
        if (palettePopup != null) palettePopup.SetActive(false);
    }

    private void ApplyPalette()
    {
        UpdatePaletteValues();
        SelectTool(DrawingToolMode.Pen);
        ClosePalette();
    }

    private void UpdatePaletteValues()
    {
        if (colorWheelPicker != null) OnColorChanged(colorWheelPicker.SelectedColor);
        UpdateBrushSize(brushSizeSlider != null ? brushSizeSlider.value : BrushSize);
        UpdateOpacity(opacitySlider != null ? opacitySlider.value : Opacity * 100f);
    }

    private void OnColorChanged(Color32 color)
    {
        color.a = (byte)Mathf.RoundToInt(Opacity * 255f);
        SelectedColor = color;
        if (colorPreview != null) colorPreview.color = SelectedColor;
        if (toolbarColorPreview != null) toolbarColorPreview.color = SelectedColor;
    }

    private void UpdateOpacity(float percent)
    {
        Opacity = Mathf.Clamp01(percent / 100f);
        if (opacityValueText != null) opacityValueText.text = $"{Mathf.RoundToInt(percent)}%";

        Color32 color = colorWheelPicker != null ? colorWheelPicker.SelectedColor : SelectedColor;
        color.a = (byte)Mathf.RoundToInt(Opacity * 255f);
        SelectedColor = color;
        if (colorPreview != null) colorPreview.color = SelectedColor;
        if (toolbarColorPreview != null) toolbarColorPreview.color = SelectedColor;
    }

    private void UpdateBrushSize(float value)
    {
        BrushSize = value;
        if (brushSizeValueText != null) brushSizeValueText.text = $"{Mathf.RoundToInt(BrushSize)} px";
    }

    private void SelectTool(DrawingToolMode tool)
    {
        CurrentTool = tool;

        SetButtonColor(paletteButton, tool == DrawingToolMode.Pen);
        SetButtonColor(eraserMenuButton, tool != DrawingToolMode.Pen);
        SetButtonColor(partialEraserButton, tool == DrawingToolMode.PartialEraser);
        SetButtonColor(strokeEraserButton, tool == DrawingToolMode.StrokeEraser);

        if (selectedToolText == null) return;

        selectedToolText.text = tool switch
        {
            DrawingToolMode.PartialEraser => "선택 도구: 부분 지우개",
            DrawingToolMode.StrokeEraser => "선택 도구: 선 전체 지우개",
            _ => "선택 도구: 펜"
        };
    }

    private void SetButtonColor(Button button, bool selected)
    {
        if (button == null || button.targetGraphic == null) return;
        button.targetGraphic.color = selected ? selectedButtonColor : normalButtonColor;
    }
}
