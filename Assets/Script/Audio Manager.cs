using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages all audio including music and sound effects
/// </summary>
public class AudioManager : MonoBehaviour
{
    #region Singleton

    public static AudioManager Instance { get; private set; }

    #endregion

    #region Inspector Fields

    [Header("Audio Sources")]
    [Tooltip("Audio source for music")]
    [SerializeField] private AudioSource musicSource;

    [Tooltip("Audio source for sound effects")]
    [SerializeField] private AudioSource sfxSource;

    [Header("Volume Settings")]
    [Tooltip("Master volume (0-1)")]
    [SerializeField][Range(0f, 1f)] private float masterVolume = 1f;

    [Tooltip("Music volume (0-1)")]
    [SerializeField][Range(0f, 1f)] private float musicVolume = 0.7f;

    [Tooltip("Sound effects volume (0-1)")]
    [SerializeField][Range(0f, 1f)] private float sfxVolume = 0.8f;

    [Header("Music Settings")]
    [Tooltip("Fade duration when changing music")]
    [SerializeField] private float musicFadeDuration = 1f;

    [Tooltip("Loop music by default")]
    [SerializeField] private bool loopMusic = true;

    [Header("Sound Effects Library")]
    [Tooltip("Button click sound")]
    [SerializeField] private AudioClip buttonClickSound;

    [Tooltip("Pawn move sound")]
    [SerializeField] private AudioClip pawnMoveSound;

    [Tooltip("Pawn capture sound")]
    [SerializeField] private AudioClip pawnCaptureSound;

    [Tooltip("Shooting sound")]
    [SerializeField] private AudioClip shootSound;

    [Tooltip("Player hit sound")]
    [SerializeField] private AudioClip playerHitSound;

    [Tooltip("Victory sound")]
    [SerializeField] private AudioClip victorySound;

    [Tooltip("Defeat sound")]
    [SerializeField] private AudioClip defeatSound;

    [Tooltip("Transition sound")]
    [SerializeField] private AudioClip transitionSound;

    [Header("Debug")]
    [Tooltip("Show debug information")]
    [SerializeField] private bool showDebug = false;

    #endregion

    #region Private Fields

    private Coroutine musicFadeCoroutine;
    private Dictionary<string, AudioClip> soundLibrary = new Dictionary<string, AudioClip>();

    #endregion

    #region Unity Lifecycle

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

    #endregion

    #region Public Methods - Music

    /// <summary>
    /// Play music clip
    /// </summary>
    public void PlayMusic(AudioClip clip, bool fade = true)
    {
        if (clip == null) return;

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

    /// <summary>
    /// Stop music
    /// </summary>
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

    /// <summary>
    /// Pause music
    /// </summary>
    public void PauseMusic()
    {
        musicSource.Pause();
    }

    /// <summary>
    /// Resume music
    /// </summary>
    public void ResumeMusic()
    {
        musicSource.UnPause();
    }

    #endregion

    #region Public Methods - Sound Effects

    /// <summary>
    /// Play sound effect
    /// </summary>
    public void PlaySFX(AudioClip clip, float volumeMultiplier = 1f)
    {
        if (clip == null) return;

        sfxSource.PlayOneShot(clip, volumeMultiplier);

        if (showDebug)
        {
            Debug.Log($"Playing SFX: {clip.name}");
        }
    }

    /// <summary>
    /// Play sound effect by name from library
    /// </summary>
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

    /// <summary>
    /// Play UI button click sound
    /// </summary>
    public void PlayButtonClick()
    {
        PlaySFX(buttonClickSound);
    }

    /// <summary>
    /// Play pawn move sound
    /// </summary>
    public void PlayPawnMove()
    {
        PlaySFX(pawnMoveSound);
    }

    /// <summary>
    /// Play pawn capture sound
    /// </summary>
    public void PlayPawnCapture()
    {
        PlaySFX(pawnCaptureSound);
    }

    /// <summary>
    /// Play shooting sound
    /// </summary>
    public void PlayShoot()
    {
        PlaySFX(shootSound);
    }

    /// <summary>
    /// Play player hit sound
    /// </summary>
    public void PlayPlayerHit()
    {
        PlaySFX(playerHitSound);
    }

    /// <summary>
    /// Play victory sound
    /// </summary>
    public void PlayVictory()
    {
        PlaySFX(victorySound);
    }

    /// <summary>
    /// Play defeat sound
    /// </summary>
    public void PlayDefeat()
    {
        PlaySFX(defeatSound);
    }

    /// <summary>
    /// Play transition sound
    /// </summary>
    public void PlayTransition()
    {
        PlaySFX(transitionSound);
    }

    #endregion

    #region Public Methods - Volume Control

    /// <summary>
    /// Set master volume
    /// </summary>
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
    }

    /// <summary>
    /// Set music volume
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
    }

    /// <summary>
    /// Set SFX volume
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
    }

    /// <summary>
    /// Get master volume
    /// </summary>
    public float GetMasterVolume() => masterVolume;

    /// <summary>
    /// Get music volume
    /// </summary>
    public float GetMusicVolume() => musicVolume;

    /// <summary>
    /// Get SFX volume
    /// </summary>
    public float GetSFXVolume() => sfxVolume;

    #endregion

    #region Private Methods

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

    #endregion
}
