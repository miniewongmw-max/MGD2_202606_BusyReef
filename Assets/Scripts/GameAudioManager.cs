using UnityEngine;

public enum GameSfx
{
    Button,
    Move,
    Pearl,
    Obstacle,
    PowerUp,
    GameOver,
    Crab,
    Pufferfish,
    Jellyfish,
    Squid,
    Shark,
    ProtectedHit
}

/// <summary>Assign clips on the Game Audio object in the Gameplay scene.</summary>
public class GameAudioManager : MonoBehaviour
{
    private const string BgmVolumeKey = "Fishfish.Audio.BGM";
    private const string SfxVolumeKey = "Fishfish.Audio.SFX";
    private const string MutedKey = "Fishfish.Audio.Muted";

    public static GameAudioManager Instance { get; private set; }

    [Header("Background Music")]
    public AudioClip backgroundMusic;

    [Header("Sound Effects")]
    public AudioClip buttonClick;
    public AudioClip playerMove;
    public AudioClip pearlCollected;
    public AudioClip obstacleHit;
    public AudioClip powerUpCollected;
    public AudioClip gameOver;

    [Header("Animal Encounters")]
    public AudioClip crab;
    public AudioClip pufferfish;
    public AudioClip jellyfish;
    public AudioClip squid;
    public AudioClip shark;
    [Tooltip("Played when Bubble Shield or Invincibility destroys an obstacle.")]
    public AudioClip protectedHit;

    [Header("Audio Sources")]
    public AudioSource musicSource;
    public AudioSource sfxSource;

    private AudioClip sharkPlaybackSource;
    private AudioClip sharkPlaybackClip;

    public static float BgmVolume => PlayerPrefs.GetFloat(BgmVolumeKey, 0.7f);
    public static float SfxVolume => PlayerPrefs.GetFloat(SfxVolumeKey, 0.8f);
    public static bool Muted => PlayerPrefs.GetInt(MutedKey, 0) == 1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // A manager from an earlier scene can exist without any clips.
            // Transfer this scene's assignments before discarding its duplicate.
            Instance.CopyAssignedClipsFrom(this);
            Instance.ApplyVolumes();
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureSources();
        ApplyVolumes();
    }

    private void CopyAssignedClipsFrom(GameAudioManager other)
    {
        if (other.backgroundMusic != null) backgroundMusic = other.backgroundMusic;
        if (other.buttonClick != null) buttonClick = other.buttonClick;
        if (other.playerMove != null) playerMove = other.playerMove;
        if (other.pearlCollected != null) pearlCollected = other.pearlCollected;
        if (other.obstacleHit != null) obstacleHit = other.obstacleHit;
        if (other.powerUpCollected != null) powerUpCollected = other.powerUpCollected;
        if (other.gameOver != null) gameOver = other.gameOver;
        if (other.crab != null) crab = other.crab;
        if (other.pufferfish != null) pufferfish = other.pufferfish;
        if (other.jellyfish != null) jellyfish = other.jellyfish;
        if (other.squid != null) squid = other.squid;
        if (other.shark != null) shark = other.shark;
        if (other.protectedHit != null) protectedHit = other.protectedHit;
    }

    public static GameAudioManager EnsureInstance()
    {
        if (Instance != null) return Instance;
        GameAudioManager existing = FindAnyObjectByType<GameAudioManager>();
        if (existing != null) return existing;
        return new GameObject("Game Audio").AddComponent<GameAudioManager>();
    }

    public static void SetBgmVolume(float value)
    {
        PlayerPrefs.SetFloat(BgmVolumeKey, Mathf.Clamp01(value));
        PlayerPrefs.Save();
        EnsureInstance().ApplyVolumes();
    }

    public static void SetSfxVolume(float value)
    {
        PlayerPrefs.SetFloat(SfxVolumeKey, Mathf.Clamp01(value));
        PlayerPrefs.Save();
        EnsureInstance().ApplyVolumes();
    }

    public static void ToggleMute()
    {
        PlayerPrefs.SetInt(MutedKey, Muted ? 0 : 1);
        PlayerPrefs.Save();
        EnsureInstance().ApplyVolumes();
    }

    public static void Play(GameSfx sound)
    {
        GameAudioManager manager = EnsureInstance();
        AudioClip clip = sound switch
        {
            GameSfx.Button => manager.buttonClick,
            GameSfx.Move => manager.playerMove,
            GameSfx.Pearl => manager.pearlCollected,
            GameSfx.Obstacle => manager.obstacleHit,
            GameSfx.PowerUp => manager.powerUpCollected,
            GameSfx.GameOver => manager.gameOver,
            GameSfx.Crab => manager.crab,
            // Use the original obstacle encounter sound rather than the
            // replacement pufferfish clip from GameAudio_2.
            GameSfx.Pufferfish => manager.obstacleHit,
            GameSfx.Jellyfish => manager.jellyfish,
            GameSfx.Squid => manager.squid,
            GameSfx.Shark => manager.GetSharkPlaybackClip(),
            GameSfx.ProtectedHit => manager.protectedHit,
            _ => null
        };
        if (clip != null && !Muted)
            manager.sfxSource.PlayOneShot(clip, sound == GameSfx.Jellyfish ? .35f : 1f);
    }

    private AudioClip GetSharkPlaybackClip()
    {
        if (shark == null) return null;
        if (sharkPlaybackSource == shark && sharkPlaybackClip != null) return sharkPlaybackClip;
        if (sharkPlaybackClip != null && sharkPlaybackClip != sharkPlaybackSource)
            Destroy(sharkPlaybackClip);
        sharkPlaybackSource = shark;
        sharkPlaybackClip = shark;

        // Some supplied MP3 effects contain a quiet lead-in. Trim only that
        // silence so the attack sound starts at the bite cue, not afterward.
        int channels = shark.channels;
        int sampleCount = shark.samples;
        if (channels < 1 || sampleCount < 1) return sharkPlaybackClip;
        float[] samples = new float[sampleCount * channels];
        if (!shark.GetData(samples, 0)) return sharkPlaybackClip;

        int searchFrames = Mathf.Min(sampleCount, Mathf.RoundToInt(shark.frequency * .5f));
        float peak = 0f;
        for (int i = 0; i < searchFrames * channels; i++)
            peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
        float threshold = Mathf.Max(.008f, peak * .025f);
        int windowFrames = Mathf.Max(1, shark.frequency / 100);
        int onsetFrame = 0;
        bool foundOnset = false;
        for (int start = 0; start < searchFrames; start += windowFrames)
        {
            int end = Mathf.Min(start + windowFrames, searchFrames);
            float energy = 0f;
            for (int frame = start; frame < end; frame++)
            for (int channel = 0; channel < channels; channel++)
            {
                float value = samples[frame * channels + channel];
                energy += value * value;
            }
            float rms = Mathf.Sqrt(energy / ((end - start) * channels));
            if (rms < threshold) continue;
            onsetFrame = Mathf.Max(0, start - shark.frequency / 100);
            foundOnset = true;
            break;
        }
        if (!foundOnset || onsetFrame < shark.frequency / 50) return sharkPlaybackClip;

        int remainingFrames = sampleCount - onsetFrame;
        float[] trimmedSamples = new float[remainingFrames * channels];
        System.Array.Copy(samples, onsetFrame * channels, trimmedSamples, 0, trimmedSamples.Length);
        AudioClip trimmed = AudioClip.Create("Shark Bite (Tight Onset)", remainingFrames, channels, shark.frequency, false);
        if (!trimmed.SetData(trimmedSamples, 0))
        {
            Destroy(trimmed);
            return sharkPlaybackClip;
        }
        sharkPlaybackClip = trimmed;
        return sharkPlaybackClip;
    }

    private void EnsureSources()
    {
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
        }
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
        }
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;
    }

    private void ApplyVolumes()
    {
        EnsureSources();
        LoadMissingClips();
        GetSharkPlaybackClip();
        float mute = Muted ? 0f : 1f;
        musicSource.volume = BgmVolume * mute;
        sfxSource.volume = SfxVolume * mute;
        if (backgroundMusic == null) return;
        if (musicSource.clip != backgroundMusic) musicSource.clip = backgroundMusic;
        if (!musicSource.isPlaying) musicSource.Play();
    }

    private void OnDestroy()
    {
        if (sharkPlaybackClip != null && sharkPlaybackClip != sharkPlaybackSource)
            Destroy(sharkPlaybackClip);
        if (Instance == this) Instance = null;
    }

    private void LoadMissingClips()
    {
        // Also works when an already-open scene has not refreshed its serialized
        // clip references, or an audio manager was created before Gameplay loaded.
        if (backgroundMusic == null) backgroundMusic = Resources.Load<AudioClip>("GameAudio/BGM");
        if (buttonClick == null) buttonClick = Resources.Load<AudioClip>("GameAudio/ButtonClick2");
        if (playerMove == null) playerMove = Resources.Load<AudioClip>("GameAudio/PlayerMove2");
        if (pearlCollected == null) pearlCollected = Resources.Load<AudioClip>("GameAudio/PearlCollected");
        if (obstacleHit == null) obstacleHit = Resources.Load<AudioClip>("GameAudio/ObstacleHit");
        if (powerUpCollected == null) powerUpCollected = Resources.Load<AudioClip>("GameAudio/PowerUpCollected");
        if (gameOver == null) gameOver = Resources.Load<AudioClip>("GameAudio/GameOver");
        if (crab == null) crab = Resources.Load<AudioClip>("GameAudio/Crab");
        if (pufferfish == null) pufferfish = Resources.Load<AudioClip>("GameAudio/Pufferfish");
        if (jellyfish == null) jellyfish = Resources.Load<AudioClip>("GameAudio/JellyfishDischarge");
        if (squid == null) squid = Resources.Load<AudioClip>("GameAudio/Squid");
        if (shark == null) shark = Resources.Load<AudioClip>("GameAudio/Shark");
        if (protectedHit == null) protectedHit = Resources.Load<AudioClip>("GameAudio/ProtectedHit");
    }
}
