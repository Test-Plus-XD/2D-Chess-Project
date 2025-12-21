using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// Manages all audio including music and sound effects
public class AudioManager : MonoBehaviour
{
    // Singleton instance for easy access.
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [Tooltip("Audio source for music")]
    // Audio source for background music playback.
    [SerializeField] private AudioSource musicSource;
    [Tooltip("Audio source for sound effects")]
    // Audio source for playing sound effects.
    [SerializeField] private AudioSource sfxSource;

    [Header("Volume Settings")]
    [Tooltip("Master volume (0-1)")]
    // Master volume multiplier applied to all audio.
    [SerializeField][Range(0f, 1f)] private float masterVolume = 1f;
    [Tooltip("Music volume (0-1)")]
    // Volume multiplier for background music.
    [SerializeField][Range(0f, 1f)] private float musicVolume = 0.7f;
    [Tooltip("Sound effects volume (0-1)")]
    // Volume multiplier for sound effects.
    [SerializeField][Range(0f, 1f)] private float sfxVolume = 0.8f;

    [Header("Music Settings")]
    [Tooltip("Fade duration when changing music")]
    // Duration in seconds for music fade transitions.
    [SerializeField] private float musicFadeDuration = 1f;
    [Tooltip("Loop music by default")]
    // Whether background music should loop when playing.
    [SerializeField] private bool loopMusic = true;

    [Header("Music Library")]
    [Tooltip("Universal menu background music (Main Menu and Level Select)")]
    // Music track played in main menu and level select screens.
    [SerializeField] private AudioClip menuMusic;

    [Header("Sound Effects Library")]
    [Tooltip("Button click sound")]
    // Sound effect played when UI buttons are clicked.
    [SerializeField] private AudioClip buttonClickSound;
    [Tooltip("Pawn move sound")]
    // Sound effect played when a pawn moves.
    [SerializeField] private AudioClip pawnMoveSound;
    [Tooltip("Pawn capture sound")]
    // Sound effect played when a pawn is captured.
    [SerializeField] private AudioClip pawnCaptureSound;
    [Tooltip("Shooting sound")]
    // Sound effect played when a weapon fires.
    [SerializeField] private AudioClip shootSound;
    [Tooltip("Player hit sound")]
    // Sound effect played when the player takes damage.
    [SerializeField] private AudioClip playerHitSound;
    [Tooltip("Victory sound")]
    // Sound effect played when the player wins.
    [SerializeField] private AudioClip victorySound;
    [Tooltip("Defeat sound")]
    // Sound effect played when the player loses.
    [SerializeField] private AudioClip defeatSound;
    [Tooltip("Transition sound")]
    // Sound effect played during scene transitions.
    [SerializeField] private AudioClip transitionSound;

    [Header("Debug")]
    [Tooltip("Show debug information")]
    // Enable debug logging for audio events.
    [SerializeField] private bool showDebug = false;

    // Coroutine handle for music fade transitions.
    private Coroutine musicFadeCoroutine;
    // Dictionary mapping sound names to audio clips for quick lookup.
    private Dictionary<string, AudioClip> soundLibrary = new Dictionary<string, AudioClip>();

    private void Awake()
    {
        // Singleton setup
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Create audio sources if not assigned
        if (musicSource == null)
        {
            GameObject musicObj = new GameObject("MusicSource");
            musicObj.transform.SetParent(transform);
            musicSource = musicObj.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = loopMusic;
        }

        if (sfxSource == null)
        {
            GameObject sfxObj = new GameObject("SFXSource");
            sfxObj.transform.SetParent(transform);
            sfxSource = sfxObj.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }

        // Initialize sound library
        InitializeSoundLibrary();

        // Apply initial volumes
        UpdateVolumes();
    }

    /// Get the music audio source (for external systems like TimeController)
    public AudioSource GetMusicSource()
    {
        return musicSource;
    }

    /// Play music clip
    public void PlayMusic(AudioClip clip, bool fade = true)
    {
        if (clip == null) return;

        // Don't restart if already playing the same clip
        if (musicSource.clip == clip && musicSource.isPlaying) return;

        if (fade)
        {
            if (musicFadeCoroutine != null)
            {
                StopCoroutine(musicFadeCoroutine);
            }
            musicFadeCoroutine = StartCoroutine(FadeMusic(clip));
        }
        else
        {
            musicSource.clip = clip;
            musicSource.Play();
        }

        if (showDebug)
        {
            Debug.Log($"Playing music: {clip.name}");
        }
    }

    /// Play universal menu music
    public void PlayMenuMusic()
    {
        if (menuMusic != null)
        {
            PlayMusic(menuMusic, fade: true);
        }
        else if (showDebug)
        {
            Debug.LogWarning("[AudioManager] Menu music clip not assigned.");
        }
    }

    /// Play Chess mode music from level data
    public void PlayChessModeMusic(LevelData levelData)
    {
        if (levelData != null && levelData.ChessModeMusic != null)
        {
            PlayMusic(levelData.ChessModeMusic, fade: true);
        }
        else if (showDebug)
        {
            Debug.LogWarning("[AudioManager] Chess mode music not found in level data, falling back to menu music.");
            PlayMenuMusic();
        }
    }

    /// Play Standoff mode music from level data
    public void PlayStandoffModeMusic(LevelData levelData)
    {
        if (levelData != null && levelData.StandoffModeMusic != null)
        {
            PlayMusic(levelData.StandoffModeMusic, fade: true);
        }
        else if (showDebug)
        {
            Debug.LogWarning("[AudioManager] Standoff mode music not found in level data.");
        }
    }

    /// Stop music
    public void StopMusic(bool fade = true)
    {
        if (fade)
        {
            if (musicFadeCoroutine != null)
            {
                StopCoroutine(musicFadeCoroutine);
            }
            musicFadeCoroutine = StartCoroutine(FadeOutMusic());
        }
        else
        {
            musicSource.Stop();
        }
    }

    /// Pause music
    public void PauseMusic()
    {
        musicSource.Pause();
    }

    /// Resume music
    public void ResumeMusic()
    {
        musicSource.UnPause();
    }

    /// Play sound effect
    public void PlaySFX(AudioClip clip, float volumeMultiplier = 1f)
    {
        if (clip == null) return;

        sfxSource.PlayOneShot(clip, volumeMultiplier);

        if (showDebug)
        {
            Debug.Log($"Playing SFX: {clip.name}");
        }
    }

    /// Play sound effect by name from library
    public void PlaySFX(string soundName, float volumeMultiplier = 1f)
    {
        if (soundLibrary.ContainsKey(soundName))
        {
            PlaySFX(soundLibrary[soundName], volumeMultiplier);
        }
        else if (showDebug)
        {
            Debug.LogWarning($"Sound not found in library: {soundName}");
        }
    }

    /// Play UI button click sound
    public void PlayButtonClick()
    {
        PlaySFX(buttonClickSound);
    }

    /// Play pawn move sound
    public void PlayPawnMove()
    {
        PlaySFX(pawnMoveSound);
    }

    /// Play pawn capture sound
    public void PlayPawnCapture()
    {
        PlaySFX(pawnCaptureSound);
    }

    /// Play shooting sound
    public void PlayShoot()
    {
        PlaySFX(shootSound);
    }

    /// Play player hit sound
    public void PlayPlayerHit()
    {
        PlaySFX(playerHitSound);
    }

    /// Play victory sound
    public void PlayVictory()
    {
        PlaySFX(victorySound);
    }

    /// Play defeat sound
    public void PlayDefeat()
    {
        PlaySFX(defeatSound);
    }

    /// Play transition sound
    public void PlayTransition()
    {
        PlaySFX(transitionSound);
    }

    /// Set master volume
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
    }

    /// Set music volume
    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
    }

    /// Set SFX volume
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
    }

    /// Get master volume
    public float GetMasterVolume() => masterVolume;

    /// Get music volume
    public float GetMusicVolume() => musicVolume;

    /// Get SFX volume
    public float GetSFXVolume() => sfxVolume;

    private void InitializeSoundLibrary()
    {
        soundLibrary.Clear();

        if (buttonClickSound != null) soundLibrary["ButtonClick"] = buttonClickSound;
        if (pawnMoveSound != null) soundLibrary["PawnMove"] = pawnMoveSound;
        if (pawnCaptureSound != null) soundLibrary["PawnCapture"] = pawnCaptureSound;
        if (shootSound != null) soundLibrary["Shoot"] = shootSound;
        if (playerHitSound != null) soundLibrary["PlayerHit"] = playerHitSound;
        if (victorySound != null) soundLibrary["Victory"] = victorySound;
        if (defeatSound != null) soundLibrary["Defeat"] = defeatSound;
        if (transitionSound != null) soundLibrary["Transition"] = transitionSound;
    }

    private void UpdateVolumes()
    {
        if (musicSource != null)
        {
            musicSource.volume = masterVolume * musicVolume;
        }

        if (sfxSource != null)
        {
            sfxSource.volume = masterVolume * sfxVolume;
        }
    }

    private IEnumerator FadeMusic(AudioClip newClip)
    {
        // Fade out current music
        float startVolume = musicSource.volume;
        float elapsed = 0f;

        while (elapsed < musicFadeDuration / 2f)
        {
            elapsed += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / (musicFadeDuration / 2f));
            yield return null;
        }

        // Switch clip
        musicSource.Stop();
        musicSource.clip = newClip;
        musicSource.Play();

        // Fade in new music
        elapsed = 0f;
        float targetVolume = masterVolume * musicVolume;

        while (elapsed < musicFadeDuration / 2f)
        {
            elapsed += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(0f, targetVolume, elapsed / (musicFadeDuration / 2f));
            yield return null;
        }

        musicSource.volume = targetVolume;
        musicFadeCoroutine = null;
    }

    private IEnumerator FadeOutMusic()
    {
        float startVolume = musicSource.volume;
        float elapsed = 0f;

        while (elapsed < musicFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / musicFadeDuration);
            yield return null;
        }

        musicSource.Stop();
        musicSource.volume = startVolume;
        musicFadeCoroutine = null;
    }
}