using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MainMenuSettingsController : MonoBehaviour
{
    private static readonly Vector2Int[] Resolutions = {new Vector2Int(960, 540), new Vector2Int(1280, 720), new Vector2Int(1920, 1080)};

    [SerializeField] private GameObject settingsPopup;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown screenModeDropdown;
    [SerializeField] private CanvasGroup resolutionGroup;
    [SerializeField] private GameObject soundPage;
    [SerializeField] private GameObject displayPage;
    [SerializeField] private GameObject languagePage;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]

    private static void RestoreDisplaySettings()
    {
        if (Application.isEditor || !PlayerPrefs.HasKey("DisplayResolutionIndex")) return;
        ApplyDisplay(PlayerPrefs.GetInt("DisplayResolutionIndex"), PlayerPrefs.GetInt("DisplayModeIndex", 0));
    }

    private static void ApplyDisplay(int resolutionIndex, int modeIndex)
    {
        Vector2Int resolution = modeIndex == 1
            ? new Vector2Int(1920, 1080)
            : Resolutions[Mathf.Clamp(resolutionIndex, 0, Resolutions.Length - 1)];
        Screen.SetResolution(resolution.x, resolution.y, modeIndex == 1 ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.Windowed);
    }
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private TMP_Text sfxValueText;
    [SerializeField] private TMP_Dropdown languageDropdown;

    public static float SfxVolume { get; private set; } = 0.8f;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RestoreMasterVolume()
        => AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat("MasterVolume", 1f));
    public static int LanguageIndex { get; private set; }
    private bool initialized;

    private void Awake()
    {
        Initialize();
    }

    public void ToggleSettings()
    {
        if (settingsPopup == null) return;
        if (settingsPopup.activeSelf) CloseSettings();
        else OpenSettings();
    }

    private void Initialize()
    {
        if (initialized) return;
        initialized = true;
        SfxVolume = PlayerPrefs.GetFloat("SfxVolume", 0.8f);
        if (masterVolumeSlider != null) masterVolumeSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("MasterVolume", 1f));
        if (musicVolumeSlider != null) musicVolumeSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("MusicVolume", 1f));
        if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(UpdateMasterVolume);
        if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.AddListener(UpdateMusicVolume);
        RefreshVolumeLabels();
        LanguageIndex = Mathf.Clamp(PlayerPrefs.GetInt("LanguageIndex", 0), 0, 2);

        ConfigureDisplayMenus();

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


    }

    public void OpenSettings()
    {
        Initialize();
        ConfigureDisplayMenus();
        if (sfxVolumeSlider != null) sfxVolumeSlider.value = SfxVolume;
        if (masterVolumeSlider != null) masterVolumeSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("MasterVolume", 1f));
        if (musicVolumeSlider != null) musicVolumeSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("MusicVolume", 1f));
        RefreshVolumeLabels();
        if (languageDropdown != null) languageDropdown.SetValueWithoutNotify(LanguageIndex);
        if (settingsPopup != null) settingsPopup.transform.SetAsLastSibling();
        if (settingsPopup != null) settingsPopup.SetActive(true);
        SelectSettingsTab(0);
    }

    public void SelectSettingsTab(int tab)
    {
        HideDisplayMenus();
        if (languageDropdown != null) languageDropdown.Hide();
        if (soundPage != null) soundPage.SetActive(tab == 0);
        if (displayPage != null) displayPage.SetActive(tab == 1);
        if (languagePage != null) languagePage.SetActive(tab == 2);
    }

    public void CloseSettings()
    {
        HideDisplayMenus();
        if (settingsPopup != null) settingsPopup.SetActive(false);
    }

    public void ConfigureDisplayMenus()
    {
        if (screenModeDropdown == null)
            screenModeDropdown = settingsPopup?.transform.Find("PopupWindow/ScreenModeDropdown")?.GetComponent<TMP_Dropdown>();

        ConfigureExistingMenu(resolutionDropdown, new[] { "960 × 540", "1280 × 720", "1920 × 1080" },
            PlayerPrefs.GetInt("DisplayResolutionIndex", Screen.width <= 960 ? 0 : Screen.width <= 1280 ? 1 : 2));

        // Keep the existing saved convention: 0 = window, 1 = full screen.
        ConfigureExistingMenu(screenModeDropdown, new[] { "창 모드", "전체 화면" },
            PlayerPrefs.GetInt("DisplayModeIndex", Screen.fullScreen ? 1 : 0));
        RefreshResolutionAvailability();
    }

    private void RefreshResolutionAvailability()
    {
        if (resolutionDropdown == null) return;
        if (resolutionGroup == null)
        {
            var row = resolutionDropdown.transform.parent;
            resolutionGroup = row.GetComponent<CanvasGroup>();
            if (resolutionGroup == null) resolutionGroup = row.gameObject.AddComponent<CanvasGroup>();
        }
        bool windowed = screenModeDropdown == null || screenModeDropdown.value == 0;
        if (!windowed) SelectFullHdResolution();
        resolutionDropdown.interactable = windowed;
        resolutionGroup.alpha = windowed ? 1f : 0.4f;
        resolutionGroup.interactable = windowed;
        resolutionGroup.blocksRaycasts = windowed;
        if (!windowed && resolutionDropdown.template != null)
            resolutionDropdown.template.gameObject.SetActive(false);
    }

    private void SelectFullHdResolution()
    {
        if (resolutionDropdown == null) return;
        resolutionDropdown.SetValueWithoutNotify(Resolutions.Length - 1);
        resolutionDropdown.RefreshShownValue();
        if (resolutionDropdown.template == null) return;
        var items = resolutionDropdown.template.GetComponentsInChildren<Toggle>(true);
        for (int i = 0; i < items.Length; i++)
            items[i].SetIsOnWithoutNotify(i == resolutionDropdown.value);
    }

    private void ConfigureExistingMenu(TMP_Dropdown dropdown, string[] options, int selected)
    {
        if (dropdown == null || dropdown.template == null) return;

        dropdown.Hide();
        dropdown.enabled = false; // Use the authored Template, not TMP's cloned Dropdown List.
        dropdown.ClearOptions();
        dropdown.AddOptions(new System.Collections.Generic.List<string>(options));
        dropdown.SetValueWithoutNotify(Mathf.Clamp(selected, 0, options.Length - 1));
        dropdown.RefreshShownValue();

        var template = dropdown.template;
        var canvas = template.GetComponent<Canvas>();

        if (canvas == null) canvas = template.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;

        var parentCanvas = dropdown.GetComponentInParent<Canvas>(true);
        canvas.sortingOrder = (parentCanvas != null ? parentCanvas.sortingOrder : 0) + 100;

        if (template.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            template.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var items = template.GetComponentsInChildren<UnityEngine.UI.Toggle>(true);

        for (int i = 0; i < items.Length && i < options.Length; i++)
        {
            int index = i;
            var item = items[i];
            item.enabled = false;
            item.group = null;
            item.SetIsOnWithoutNotify(i == dropdown.value);
            var label = item.GetComponentInChildren<TMP_Text>(true);

            if (label != null) { label.text = options[i]; label.raycastTarget = false; }
            if (item.targetGraphic != null) item.targetGraphic.raycastTarget = true;

            var trigger = item.GetComponent<UnityEngine.EventSystems.EventTrigger>();

            if (trigger == null) trigger = item.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            trigger.triggers.RemoveAll(entry => entry.eventID == UnityEngine.EventSystems.EventTriggerType.PointerClick);

            var click = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerClick };
            click.callback.AddListener(_ =>
            {
                if (dropdown == resolutionDropdown && screenModeDropdown != null && screenModeDropdown.value != 0) return;
                bool switchingToWindow = dropdown == screenModeDropdown && dropdown.value == 1 && index == 0;
                dropdown.SetValueWithoutNotify(index);
                dropdown.RefreshShownValue();
                foreach (var other in items) other.SetIsOnWithoutNotify(other == item);
                template.gameObject.SetActive(false);
                if (dropdown == screenModeDropdown)
                {
                    if (switchingToWindow) SelectFullHdResolution();
                    RefreshResolutionAvailability();
                }
            });
            trigger.triggers.Add(click);
        }

        template.gameObject.SetActive(false);
    }

    public void ToggleResolutionMenu() => ToggleDisplayMenu(resolutionDropdown);
    public void ToggleScreenModeMenu() => ToggleDisplayMenu(screenModeDropdown);

    private void ToggleDisplayMenu(TMP_Dropdown dropdown)
    {
        if (dropdown == null || dropdown.template == null) return;
        if (dropdown == resolutionDropdown && screenModeDropdown != null && screenModeDropdown.value != 0) return;
        bool open = !dropdown.template.gameObject.activeSelf;
        HideDisplayMenus();
        dropdown.template.gameObject.SetActive(open);
    }

    private void HideDisplayMenus()
    {
        if (resolutionDropdown != null && resolutionDropdown.template != null)
            resolutionDropdown.template.gameObject.SetActive(false);
        if (screenModeDropdown != null && screenModeDropdown.template != null)
            screenModeDropdown.template.gameObject.SetActive(false);
    }

    public void ApplySettings()
    {
        int resolutionIndex = resolutionDropdown != null ? resolutionDropdown.value : 0;
        resolutionIndex = Mathf.Clamp(resolutionIndex, 0, Resolutions.Length - 1);
        int modeIndex = screenModeDropdown != null ? screenModeDropdown.value : 0;
        ApplyDisplay(resolutionIndex, modeIndex);

        SfxVolume = sfxVolumeSlider != null ? sfxVolumeSlider.value : SfxVolume;
        LanguageIndex = languageDropdown != null ? languageDropdown.value : LanguageIndex;

        PlayerPrefs.SetInt("ResolutionIndex", resolutionIndex);
        PlayerPrefs.SetInt("DisplayResolutionIndex", resolutionIndex);
        PlayerPrefs.SetInt("DisplayModeIndex", modeIndex);
        PlayerPrefs.SetFloat("SfxVolume", SfxVolume);
        float master = masterVolumeSlider != null ? masterVolumeSlider.value : PlayerPrefs.GetFloat("MasterVolume", 1f);
        PlayerPrefs.SetFloat("MasterVolume", Mathf.Clamp01(master));
        AudioListener.volume = Mathf.Clamp01(master);
        if (musicVolumeSlider != null) PlayerPrefs.SetFloat("MusicVolume", Mathf.Clamp01(musicVolumeSlider.value));
        PlayerPrefs.SetInt("LanguageIndex", LanguageIndex);
        PlayerPrefs.Save();

        CloseSettings();
    }

    private void UpdateSfxValueText(float value)
    {
        SfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat("SfxVolume", SfxVolume);
        if (sfxValueText != null)
        {
            sfxValueText.text = $"{Mathf.RoundToInt(value * 100f)}%";
        }
    }

    private void UpdateMasterVolume(float value)
    {
        AudioListener.volume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat("MasterVolume", AudioListener.volume);
        UpdateVolumeLabel(masterVolumeSlider, "MasterValueText", value);
    }

    private void UpdateMusicVolume(float value)
    {
        PlayerPrefs.SetFloat("MusicVolume", Mathf.Clamp01(value));
        UpdateVolumeLabel(musicVolumeSlider, "BackgroundValueText", value);
    }

    private void RefreshVolumeLabels()
    {
        if (masterVolumeSlider != null) UpdateVolumeLabel(masterVolumeSlider, "MasterValueText", masterVolumeSlider.value);
        if (musicVolumeSlider != null) UpdateVolumeLabel(musicVolumeSlider, "BackgroundValueText", musicVolumeSlider.value);
        if (sfxVolumeSlider != null && sfxValueText != null) sfxValueText.text = $"{Mathf.RoundToInt(sfxVolumeSlider.value * 100f)}%";
    }

    private static void UpdateVolumeLabel(Slider slider, string labelName, float value)
    {
        if (slider == null) return;
        foreach (var text in slider.transform.parent.GetComponentsInChildren<TMP_Text>(true))
            if (text.name == labelName) text.text = $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%";
    }
}
