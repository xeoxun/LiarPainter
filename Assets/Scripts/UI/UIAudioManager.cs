using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class UIAudioManager : MonoBehaviour
{
    public static UIAudioManager Instance { get; private set; }
    [SerializeField] private AudioClip buttonClickClip;
    [SerializeField] private AudioClip roundStartClip;
    [SerializeField] private AudioClip myTurnClip;
    [SerializeField] private AudioClip otherTurnClip;
    [SerializeField] private AudioClip appraisalConfirmClip;
    private AudioSource source;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
    }

    private void LateUpdate()
    {
        if (source != null) source.volume = Mathf.Clamp01(PlayerPrefs.GetFloat("SfxVolume", 0.8f));
    }

    public void PlayClick()
    {
        PlayEffect(buttonClickClip);
    }

    public void PlayRoundStart()
    {
        PlayEffect(roundStartClip);
    }

    public void PlayDrawingTurn(bool isMyTurn)
    {
        PlayEffect(isMyTurn ? myTurnClip : otherTurnClip);
    }

    public void PlayAppraisalConfirm()
    {
        PlayEffect(appraisalConfirmClip);
    }

    private void PlayEffect(AudioClip clip)
    {
        if (source == null || clip == null) return;
        float volume = Mathf.Clamp01(PlayerPrefs.GetFloat("SfxVolume", 0.8f));
        source.volume = volume;
        source.PlayOneShot(clip);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
