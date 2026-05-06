using System.Collections.Generic;
using UnityEngine;

// Runtime one-shot audio helper. Clips live in Assets/Resources/Sounds/, loaded by file name
// (no extension) on first play and cached. PlayAt is positional, Play2D is non-spatial.
public static class SoundFx
{
    // Resource paths (relative to Assets/Resources/Sounds/, no extension).
    public const string PlayerHurt = "freesound_community-male_hurt7-48124";
    public const string CoinPickup = "chieuk-coin-257878";
    public const string ItemPickup = "item-pick-up-38258";
    public const string SwingWhoosh = "jofae-swing-whoosh-110410";
    public const string SwordSlash = "sword-slash-01-266296";
    public const string HealSpell = "heal-spell";
    public const string IceSpell = "ice-spell-impact-448563";
    public const string FireballCast = "fireball-cast";
    public const string FireballImpact = "fireball-impact";
    public const string PlayerDeath = "death";
    public const string StageOneMusic = "stage1-music";
    public const string StageTwoMusic = "stage2_music";
    public const string StageThreeMusic = "stage3_music";
    public const string BossMusic = "boss_music";
    public const string MenuMusic = "menu-music";

    private static readonly Dictionary<string, AudioClip> cache = new();
    private static AudioSource oneShot2D;
    private static AudioSource musicSource;
    private static string currentMusicClip;
    private static float currentMusicBaseVolume = 0.45f;
    private static bool musicCallbackHooked;

    public static void PlayAt(string clipName, Vector3 position)
    {
        AudioClip clip = Load(clipName);
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, position, ScaleSfx());
    }

    public static void Play2D(string clipName)
    {
        AudioClip clip = Load(clipName);
        if (clip == null) return;
        EnsureOneShot2D();
        oneShot2D.PlayOneShot(clip, ScaleSfx());
    }

    // All SFX play at one uniform level set by the master slider.
    private static float ScaleSfx() => Mathf.Clamp01(GameSettings.SfxVolume);

    // 3D one-shot that seeks into the clip before playing, to skip past a long charge-up.
    public static void PlayAtWithOffset(string clipName, Vector3 position, float startTimeSeconds)
    {
        AudioClip clip = Load(clipName);
        if (clip == null) return;
        GameObject go = new GameObject($"OneShot {clipName}");
        go.transform.position = position;
        AudioSource src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = ScaleSfx();
        src.spatialBlend = 1f;
        src.playOnAwake = false;
        src.time = Mathf.Clamp(startTimeSeconds, 0f, Mathf.Max(0f, clip.length - 0.05f));
        src.Play();
        Object.Destroy(go, (clip.length - src.time) + 0.1f);
    }

    // Spatial one-shot with linear fall-off. PlayClipAtPoint's logarithmic rolloff is too generous for combat cues.
    public static void PlayAtSpatial(string clipName, Vector3 position, float minDistance = 2f, float maxDistance = 30f)
    {
        AudioClip clip = Load(clipName);
        if (clip == null) return;
        GameObject go = new GameObject($"OneShot {clipName}");
        go.transform.position = position;
        AudioSource src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = ScaleSfx();
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = Mathf.Max(0.1f, minDistance);
        src.maxDistance = Mathf.Max(src.minDistance + 0.1f, maxDistance);
        src.playOnAwake = false;
        src.Play();
        Object.Destroy(go, clip.length + 0.1f);
    }

    // Background music: one looping 2D AudioSource that survives scene loads. Calling PlayMusic with
    // the same clip is a no-op. Use StopMusic() to stop.
    public static void PlayMusic(string clipName, float volume = 0.45f)
    {
        AudioClip clip = Load(clipName);
        if (clip == null) return;
        EnsureMusicSource();
        currentMusicBaseVolume = Mathf.Clamp01(volume);
        if (currentMusicClip == clipName && musicSource.isPlaying) { ApplyMusicVolume(); return; }
        musicSource.clip = clip;
        musicSource.loop = true;
        ApplyMusicVolume();
        musicSource.Play();
        currentMusicClip = clipName;
    }

    public static void StopMusic()
    {
        if (musicSource == null) return;
        musicSource.Stop();
        currentMusicClip = null;
    }

    private static void EnsureMusicSource()
    {
        if (musicSource != null) return;
        GameObject go = new GameObject("SoundFxMusic");
        Object.DontDestroyOnLoad(go);
        musicSource = go.AddComponent<AudioSource>();
        musicSource.spatialBlend = 0f;
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        if (!musicCallbackHooked)
        {
            // Slider changes update the playing track immediately.
            GameSettings.OnMusicVolumeChanged += ApplyMusicVolume;
            musicCallbackHooked = true;
        }
    }

    private static void ApplyMusicVolume()
    {
        if (musicSource == null) return;
        musicSource.volume = currentMusicBaseVolume * Mathf.Clamp01(GameSettings.MusicVolume);
    }

    private static AudioClip Load(string clipName)
    {
        if (string.IsNullOrEmpty(clipName)) return null;
        if (cache.TryGetValue(clipName, out AudioClip cached)) return cached;
        AudioClip clip = Resources.Load<AudioClip>($"Sounds/{clipName}");
        if (clip == null) Debug.LogWarning($"No AudioClip at 'Resources/Sounds/{clipName}'.");
        cache[clipName] = clip;
        return clip;
    }

    private static void EnsureOneShot2D()
    {
        if (oneShot2D != null) return;
        GameObject go = new GameObject("SoundFx2D");
        Object.DontDestroyOnLoad(go);
        oneShot2D = go.AddComponent<AudioSource>();
        oneShot2D.spatialBlend = 0f;
        oneShot2D.playOnAwake = false;
    }
}
