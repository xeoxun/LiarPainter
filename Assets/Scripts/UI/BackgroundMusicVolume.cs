using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public sealed class BackgroundMusicVolume : MonoBehaviour
{
    private AudioSource source;
    private float authoredVolume;
    private void Awake()
    {
        source = GetComponent<AudioSource>();
        authoredVolume = source.volume;
        Update();
    }
    private void Update()
    {
        source.volume = authoredVolume * Mathf.Clamp01(PlayerPrefs.GetFloat("MusicVolume", 1f));
    }
}
