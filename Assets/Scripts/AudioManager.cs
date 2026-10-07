using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public AudioClip coin, hit, win, lose, music;
    [Range(0f, 1f)] public float sfxVolume = 0.7f;
    [Range(0f, 1f)] public float musicVolume = 0.25f;

    AudioSource sfx;
    AudioSource musicSource;

    public bool Muted { get; private set; }

    void Awake()
    {
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.clip = music;
        musicSource.volume = musicVolume;
    }

    public void PlayCoin() => Play(coin);
    public void PlayHit() => Play(hit);
    public void PlayWin() => Play(win);
    public void PlayLose() => Play(lose);

    void Play(AudioClip clip)
    {
        if (clip) sfx.PlayOneShot(clip, sfxVolume);
    }

    public void StartMusic()
    {
        if (music && !musicSource.isPlaying) musicSource.Play();
    }

    public void StopMusic() => musicSource.Stop();

    public void ToggleMute()
    {
        Muted = !Muted;
        AudioListener.volume = Muted ? 0f : 1f;
    }
}
