using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ColorWheelPicker : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    [SerializeField] private RawImage wheelImage;
    [SerializeField] private RectTransform selector;
    [SerializeField] private Image colorPreview;
    [SerializeField] private TMP_Text colorValueText;
    [SerializeField] private Slider valueSlider;
    [SerializeField] private RawImage valueGradientImage;
    [SerializeField] private TMP_Text valueText;

    private float hue;
    private float saturation;
    private Texture2D wheelTexture;
    private Texture2D valueGradientTexture;

    public Color32 SelectedColor { get; private set; } = new Color32(255, 255, 255, 255);
    public event Action<Color32> ColorChanged;

    public void Configure(RawImage wheel, RectTransform marker, Image preview, TMP_Text colorText, Slider brightness, RawImage gradient, TMP_Text brightnessText)
    {
        wheelImage = wheel;
        selector = marker;
        colorPreview = preview;
        colorValueText = colorText;
        valueSlider = brightness;
        valueGradientImage = gradient;
        valueText = brightnessText;
    }

    private void Awake()
    {
        GenerateWheelTexture();
        GenerateValueGradientTexture();
        if (valueSlider != null) valueSlider.onValueChanged.AddListener(_ => UpdateSelectedColor());
        SetHSV(0f, 0f);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        UpdateFromPointer(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        UpdateFromPointer(eventData);
    }

    public void SetHSV(float newHue, float newSaturation)
    {
        hue = Mathf.Repeat(newHue, 1f);
        saturation = Mathf.Clamp01(newSaturation);
        UpdateSelectorPosition();
        RefreshValueGradient();
        UpdateSelectedColor();
    }

    private void UpdateFromPointer(PointerEventData eventData)
    {
        if (wheelImage == null) return;
        RectTransform rect = wheelImage.rectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out Vector2 local)) return;

        float radius = Mathf.Min(rect.rect.width, rect.rect.height) * 0.5f;
        Vector2 centered = local - rect.rect.center;
        if (centered.magnitude > radius) centered = centered.normalized * radius;

        saturation = Mathf.Clamp01(centered.magnitude / radius);
        hue = Mathf.Repeat(Mathf.Atan2(centered.y, centered.x) / (Mathf.PI * 2f), 1f);
        if (selector != null) selector.anchoredPosition = centered;
        RefreshValueGradient();
        UpdateSelectedColor();
    }

    private void UpdateSelectorPosition()
    {
        if (wheelImage == null || selector == null) return;
        float radius = Mathf.Min(wheelImage.rectTransform.rect.width, wheelImage.rectTransform.rect.height) * 0.5f;
        float angle = hue * Mathf.PI * 2f;
        selector.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * saturation * radius;
    }

    private void UpdateSelectedColor()
    {
        float value = valueSlider != null ? valueSlider.value / 100f : 1f;
        Color color = Color.HSVToRGB(hue, saturation, value);
        SelectedColor = color;
        if (colorPreview != null) colorPreview.color = SelectedColor;
        if (colorValueText != null) colorValueText.text = $"#{ColorUtility.ToHtmlStringRGB(SelectedColor)}";
        if (valueText != null) valueText.text = $"{Mathf.RoundToInt(value * 100f)}%";
        ColorChanged?.Invoke(SelectedColor);
    }

    private void GenerateValueGradientTexture()
    {
        if (valueGradientImage == null) return;
        valueGradientTexture = new Texture2D(256, 1, TextureFormat.RGBA32, false)
        {
            name = "RuntimeValueGradient",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        valueGradientImage.texture = valueGradientTexture;
        RefreshValueGradient();
    }

    private void RefreshValueGradient()
    {
        if (valueGradientTexture == null) return;
        Color fullColor = Color.HSVToRGB(hue, saturation, 1f);
        Color32[] pixels = new Color32[valueGradientTexture.width];
        for (int x = 0; x < pixels.Length; x++) pixels[x] = Color.Lerp(Color.black, fullColor, x / (pixels.Length - 1f));
        valueGradientTexture.SetPixels32(pixels);
        valueGradientTexture.Apply(false, false);
    }

    private void GenerateWheelTexture()
    {
        if (wheelImage == null) return;
        const int size = 256;
        wheelTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "RuntimeColorWheel",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color32[] pixels = new Color32[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 delta = new Vector2(x, y) - center;
                float distance = delta.magnitude;
                if (distance > radius)
                {
                    pixels[y * size + x] = new Color32(0, 0, 0, 0);
                    continue;
                }

                float pixelHue = Mathf.Repeat(Mathf.Atan2(delta.y, delta.x) / (Mathf.PI * 2f), 1f);
                float pixelSaturation = Mathf.Clamp01(distance / radius);
                pixels[y * size + x] = Color.HSVToRGB(pixelHue, pixelSaturation, 1f);
            }
        }

        wheelTexture.SetPixels32(pixels);
        wheelTexture.Apply(false, false);
        wheelImage.texture = wheelTexture;
    }

    private void OnDestroy()
    {
        if (wheelTexture != null) Destroy(wheelTexture);
        if (valueGradientTexture != null) Destroy(valueGradientTexture);
    }
}
