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
    [SerializeField] private AudioSource countSource;      // looping source for win-amount count-up ticks

    [Header("UI")]
    [SerializeField] private AudioClip clipButton;           // any button press except bet +/- and Info/Back
    [SerializeField] private AudioClip clipInfoButton;       // Info button (opens paytable) AND Back-to-Game button
    [SerializeField] private AudioClip clipBetButton;        // bet +/- buttons

    [Header("Spin")]
    [SerializeField] private AudioClip clipSpinLoop;
    [SerializeField] private AudioClip clipReelStop;
    [SerializeField] private AudioClip clipPinballIconAppearsInReel;   // a reel lands with a Pinball (id 11) icon showing; once per reel, not once per icon

    [Header("Win Count-up")]
    [SerializeField] private AudioClip clipCountLoop;       // loops while a win amount counts up
    [SerializeField] private AudioClip clipCountStop;       // single play when the count-up finishes

    [Header("Pinball Bonus")]
    [SerializeField] private AudioClip clipBonusBgMusic;             // loops for the whole bonus feature
    [SerializeField] private AudioClip clipThreePinballsFlash;       // 3 Pinball symbols flash on the winning line, right before scrolling into the bonus
    [SerializeField] private AudioClip clipShootBall;                // Shoot Ball button press
    [SerializeField] private AudioClip clipBallEnteredInCircle;      // ball exits the outer ring and enters the inner-circle routing
    [SerializeField] private AudioClip clipBallHitting;              // ball bounces off a white peg in the inner circle (any of the ~5 bouncePegCircles)
    [SerializeField] private AudioClip clipBallHittedTheAmount;      // ball reaches its prize (UFO/marble) and it starts flashing
    [SerializeField] private AudioClip clipBallHitAmountSuccess;     // the prize (UFO/marble) finishes flashing
    [SerializeField] private AudioClip clipBonusTotalWinSting;       // once, as the "BONUS COMPLETE / TOTAL WIN" overlay appears, before scrolling back
    [SerializeField] private AudioClip clipBigWin; // "big win.mp3" — win panel + coin celebration after scrolling back up to the base game

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
        if (countSource) countSource.volume = v;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void PlayOneShot(AudioSource source, AudioClip clip)
    {
        if (!_sfxEnabled || source == null || clip == null) return;
        source.PlayOneShot(clip);
    }

    private void StopSource(AudioSource source)
    {
        if (source == null) return;
        source.Stop();
        source.loop = false;
    }

    // ── UI ────────────────────────────────────────────────────────────────────

    internal void PlayButton() => PlayOneShot(sfxSource, clipButton);
    internal void PlayInfoButton() => PlayOneShot(sfxSource, clipInfoButton);
    internal void PlayBetButton() => PlayOneShot(sfxSource, clipBetButton);

    // ── Spin ──────────────────────────────────────────────────────────────────

    internal void PlaySpinLoop()
    {
        if (spinSource == null || clipSpinLoop == null) return;
        spinSource.clip = clipSpinLoop;
        spinSource.loop = true;
        spinSource.volume = _sfxEnabled ? 1f : 0f;
        spinSource.Play();
    }

    internal void StopSpinLoop() => StopSource(spinSource);

    internal void PlayReelStop() => PlayOneShot(overlapSource, clipReelStop);
    internal void PlayPinballIconAppearsInReel() => PlayOneShot(overlapSource, clipPinballIconAppearsInReel);

    // ── Win Count-up ──────────────────────────────────────────────────────────

    internal void PlayCountLoop()
    {
        if (countSource == null || clipCountLoop == null) return;
        countSource.clip = clipCountLoop;
        countSource.loop = true;
        countSource.volume = _sfxEnabled ? 1f : 0f;
        countSource.Play();
    }

    internal void StopCountLoop() => StopSource(countSource);

    internal void PlayCountStop() => PlayOneShot(sfxSource, clipCountStop);

    // ── Pinball Bonus ─────────────────────────────────────────────────────────

    internal void PlayBonusBgMusic()
    {
        if (bgMusicSource == null || clipBonusBgMusic == null) return;
        if (bgMusicSource.isPlaying && bgMusicSource.clip == clipBonusBgMusic) return;
        bgMusicSource.clip = clipBonusBgMusic;
        bgMusicSource.loop = true;
        bgMusicSource.volume = _musicEnabled ? 1f : 0f;
        bgMusicSource.Play();
    }

    internal void StopBonusBgMusic() => StopSource(bgMusicSource);

    internal void PlayThreePinballsFlash() => PlayOneShot(sfxSource, clipThreePinballsFlash);
    internal void PlayBallEnteredInCircle() => PlayOneShot(sfxSource, clipBallEnteredInCircle);
    internal void PlayBallHitting() => PlayOneShot(sfxSource, clipBallHitting);
    internal void PlayBallHittedTheAmount() => PlayOneShot(sfxSource, clipBallHittedTheAmount);
    internal void PlayBallHitAmountSuccess() => PlayOneShot(sfxSource, clipBallHitAmountSuccess);
    internal void PlayBonusTotalWinSting() => PlayOneShot(sfxSource, clipBonusTotalWinSting);
    internal void PlayBigWin() => PlayOneShot(sfxSource, clipBigWin);
    internal void PlayShootBall() => PlayOneShot(sfxSource, clipShootBall);

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
