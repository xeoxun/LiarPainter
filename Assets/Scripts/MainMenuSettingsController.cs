using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MainMenuSettingsController : MonoBehaviour
{
    private static readonly Vector2Int[] Resolutions =
    {
        new Vector2Int(1920, 1080),
        new Vector2Int(1600, 900),
        new Vector2Int(1280, 720)
    };

    [SerializeField] private GameObject settingsPopup;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button applyButton;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private TMP_Text sfxValueText;
    [SerializeField] private TMP_Dropdown languageDropdown;

    public static float SfxVolume { get; private set; } = 0.8f;
    public static int LanguageIndex { get; private set; }

    public void Configure(
        GameObject popup,
        Button open,
        Button close,
        Button apply,
        TMP_Dropdown resolution,
        Slider sfxSlider,
        TMP_Text sfxLabel,
        TMP_Dropdown language)
    {
        settingsPopup = popup;
        settingsButton = open;
        closeButton = close;
        applyButton = apply;
        resolutionDropdown = resolution;
        sfxVolumeSlider = sfxSlider;
        sfxValueText = sfxLabel;
        languageDropdown = language;
    }

    private void Awake()
    {
        SfxVolume = PlayerPrefs.GetFloat("SfxVolume", 0.8f);
        LanguageIndex = Mathf.Clamp(PlayerPrefs.GetInt("LanguageIndex", 0), 0, 2);

        if (resolutionDropdown != null)
        {
            resolutionDropdown.value = Mathf.Clamp(PlayerPrefs.GetInt("ResolutionIndex", 0), 0, Resolutions.Length - 1);
            resolutionDropdown.RefreshShownValue();
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.value = SfxVolume;
            sfxVolumeSlider.onValueChanged.AddListener(UpdateSfxValueText);
            UpdateSfxValueText(SfxVolume);
        }

        if (languageDropdown != null)
        {
            languageDropdown.value = LanguageIndex;
            languageDropdown.RefreshShownValue();
        }

        if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
        if (closeButton != null) closeButton.onClick.AddListener(CloseSettings);
        if (applyButton != null) applyButton.onClick.AddListener(ApplySettings);

        if (settingsPopup != null) settingsPopup.SetActive(false);
    }

    public void OpenSettings()
    {
        if (settingsPopup != null) settingsPopup.SetActive(true);
    }

    public void CloseSettings()
    {
        if (settingsPopup != null) settingsPopup.SetActive(false);
    }

    public void ApplySettings()
    {
        int resolutionIndex = resolutionDropdown != null ? resolutionDropdown.value : 0;
        resolutionIndex = Mathf.Clamp(resolutionIndex, 0, Resolutions.Length - 1);
        Vector2Int resolution = Resolutions[resolutionIndex];
        Screen.SetResolution(resolution.x, resolution.y, Screen.fullScreenMode);

        SfxVolume = sfxVolumeSlider != null ? sfxVolumeSlider.value : SfxVolume;
        LanguageIndex = languageDropdown != null ? languageDropdown.value : LanguageIndex;

        PlayerPrefs.SetInt("ResolutionIndex", resolutionIndex);
        PlayerPrefs.SetFloat("SfxVolume", SfxVolume);
        PlayerPrefs.SetInt("LanguageIndex", LanguageIndex);
        PlayerPrefs.Save();
        CloseSettings();
    }

    private void UpdateSfxValueText(float value)
    {
        if (sfxValueText != null)
        {
            sfxValueText.text = $"{Mathf.RoundToInt(value * 100f)}%";
        }
    }
}
