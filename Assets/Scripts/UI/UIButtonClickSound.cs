using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class UIButtonClickSound : MonoBehaviour
{
    private Button button;
    private void Awake() => button = GetComponent<Button>();

    public void Play()
    {
        if (button == null) button = GetComponent<Button>();
        if (button == null || !button.IsActive() || !button.IsInteractable()) return;
        if (UIAudioManager.Instance != null) UIAudioManager.Instance.PlayClick();
    }
}
