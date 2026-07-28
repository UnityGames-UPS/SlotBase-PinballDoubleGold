using UnityEngine;

public class AudioManager : MonoBehaviour
{
    private const string PrefKeyMusic = "audio_music_enabled";
    private const string PrefKeySFX = "audio_sfx_enabled";

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgMusicSource;
    [SerializeField] private AudioSource spinSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource overlapSource;
    [SerializeField] private AudioSource ballTickSource;   // looping source for the pinball ball-travel tick

    [Header("BG Music")]
    [SerializeField] private AudioClip clipBg;

    [Header("UI")]
    [SerializeField] private AudioClip clipButton;
    [SerializeField] private AudioClip clipUIClick;
    [SerializeField] private AudioClip clipGameStarted;

    [Header("Spin")]
    [SerializeField] private AudioClip clipSpinLoop;
    [SerializeField] private AudioClip clipReelStop;

    [Header("Symbols")]
    [SerializeField] private AudioClip clipNormalIcon;

    [Header("Pinball Bonus")]
    [SerializeField] private AudioClip clipBonusBg;         // bonus-game background music (loop)
    [SerializeField] private AudioClip clipBonusScatter;    // 3 Pinball trigger symbols land/flash
    [SerializeField] private AudioClip clipBonusAnimation;  // bonus intro flourish
    [SerializeField] private AudioClip clipBallTick;        // ball travelling the ring (loop)
    [SerializeField] private AudioClip clipBallStop;        // ball lands/stops on a prize
    [SerializeField] private AudioClip clipBonusComplete;   // bonus-complete sting
    [SerializeField] private AudioClip clipBigWin;          // big-win celebration (pinball bonus win)

    private bool _musicEnabled = true;
    private bool _sfxEnabled = true;

    internal bool MusicEnabled => _musicEnabled;
    internal bool SfxEnabled => _sfxEnabled;

    private void Awake()
    {
        _musicEnabled = PlayerPrefs.GetInt(PrefKeyMusic, 1) == 1;
        _sfxEnabled = PlayerPrefs.GetInt(PrefKeySFX, 1) == 1;
        ApplyMusicVolume();
        ApplySfxVolume();
    }

    private void Start()
    {
        PlayBgMusic();
    }

    // ── Volume Control ────────────────────────────────────────────────────────

    internal void SetMusicEnabled(bool on)
    {
        ClearForceMute();   // an explicit tap proves we have real focus — it must win immediately
        _musicEnabled = on;
        PlayerPrefs.SetInt(PrefKeyMusic, on ? 1 : 0);
        PlayerPrefs.Save();
        ApplyMusicVolume();
    }

    internal void SetSfxEnabled(bool on)
    {
        ClearForceMute();   // an explicit tap proves we have real focus — it must win immediately
        _sfxEnabled = on;
        PlayerPrefs.SetInt(PrefKeySFX, on ? 1 : 0);
        PlayerPrefs.Save();
        ApplySfxVolume();
    }

    private void ApplyMusicVolume()
    {
        if (bgMusicSource) bgMusicSource.volume = _musicEnabled ? 1f : 0f;
    }

    private void ApplySfxVolume()
    {
        float v = _sfxEnabled ? 1f : 0f;
        if (sfxSource) sfxSource.volume = v;
        if (overlapSource) overlapSource.volume = v;
        if (spinSource) spinSource.volume = v;
        if (ballTickSource) ballTickSource.volume = v;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void PlayOneShot(AudioSource source, AudioClip clip)
    {
        if (!_sfxEnabled || source == null || clip == null) return;
        source.PlayOneShot(clip);
    }

    private void PlayLoop(AudioSource source, AudioClip clip)
    {
        if (source == null || clip == null) return;
        source.clip = clip;
        source.loop = true;
        source.Play();
    }

    private void StopSource(AudioSource source)
    {
        if (source == null) return;
        source.Stop();
        source.loop = false;
    }

    // ── BG Music ──────────────────────────────────────────────────────────────

    internal void PlayBgMusic()
    {
        if (bgMusicSource == null || clipBg == null) return;
        if (bgMusicSource.isPlaying && bgMusicSource.clip == clipBg) return;
        bgMusicSource.clip = clipBg;
        bgMusicSource.loop = true;
        bgMusicSource.volume = _musicEnabled ? 1f : 0f;
        bgMusicSource.Play();
    }

    // ── UI ────────────────────────────────────────────────────────────────────

    internal void PlayButton() => PlayOneShot(sfxSource, clipButton);
    internal void PlayUIClick() => PlayOneShot(sfxSource, clipUIClick);
    internal void PlayGameStarted() => PlayOneShot(sfxSource, clipGameStarted);

    // ── Spin ──────────────────────────────────────────────────────────────────

    internal void PlaySpinLoop()
    {
        if (spinSource == null || clipSpinLoop == null) return;
        spinSource.clip = clipSpinLoop;
        spinSource.loop = false;
        spinSource.volume = _sfxEnabled ? 1f : 0f;
        spinSource.Play();
    }

    internal void StopSpinLoop() => StopSource(spinSource);

    internal void PlayReelStop() => PlayOneShot(overlapSource, clipReelStop);

    // ── Symbols ───────────────────────────────────────────────────────────────

    internal void PlayNormalIcon() => PlayOneShot(sfxSource, clipNormalIcon);

    // ── Pinball Bonus ─────────────────────────────────────────────────────────

    internal void PlayBonusBgMusic()
    {
        if (bgMusicSource == null || clipBonusBg == null) return;
        if (bgMusicSource.isPlaying && bgMusicSource.clip == clipBonusBg) return;
        bgMusicSource.clip = clipBonusBg;
        bgMusicSource.loop = true;
        bgMusicSource.volume = _musicEnabled ? 1f : 0f;
        bgMusicSource.Play();
    }

    internal void PlayBonusScatter() => PlayOneShot(sfxSource, clipBonusScatter);
    internal void PlayBonusAnimation() => PlayOneShot(sfxSource, clipBonusAnimation);
    internal void PlayBallStop() => PlayOneShot(overlapSource, clipBallStop);
    internal void PlayBonusComplete() => PlayOneShot(sfxSource, clipBonusComplete);
    internal void PlayBigWin() => PlayOneShot(sfxSource, clipBigWin);

    internal void PlayBallTick()
    {
        if (ballTickSource == null || clipBallTick == null) return;
        if (ballTickSource.isPlaying && ballTickSource.clip == clipBallTick) return;
        ballTickSource.clip = clipBallTick;
        ballTickSource.loop = true;
        ballTickSource.volume = _sfxEnabled ? 1f : 0f;
        ballTickSource.Play();
    }

    internal void StopBallTick() => StopSource(ballTickSource);

    // ── Focus Handling ────────────────────────────────────────────────────────

    private bool isForceMuted = false;

    // Focus-driven mute. Called from BOTH the native path below AND the WebGL
    // OnFocusChanged path (SocketIOManager -> UIManager.SetFocusMute). Never touches
    // the user's setting, which lives in per-source volume / PlayerPrefs.
    internal void SetMuteAll(bool forceMute)
    {
        if (forceMute == isForceMuted) return;   // both paths fire for one blur/focus event
        isForceMuted = forceMute;
        AudioListener.volume = forceMute ? 0f : 1f;
    }

    // A stale forced-mute must never block the user's own sound/music button.
    private void ClearForceMute()
    {
        if (!isForceMuted) return;
        isForceMuted = false;
        AudioListener.volume = 1f;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        SetMuteAll(!hasFocus);
    }

    private void OnApplicationPause(bool isPaused)
    {
        SetMuteAll(isPaused);
    }
}
