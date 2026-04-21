using UnityEngine;

public static class GameSettings
{
    public const float MinSensitivity = 0.25f;
    public const float MaxSensitivity = 3f;
    public const float DefaultSensitivity = 1f;

    private const string SensitivityKey = "GameSettings.Sensitivity";
    private const string ToggleSprintKey = "GameSettings.ToggleSprint";
    private const string SfxVolumeKey = "GameSettings.SfxVolume";
    private const string MusicVolumeKey = "GameSettings.MusicVolume";

    private static float cachedSensitivity = -1f;
    private static int cachedToggleSprint = -1;
    private static float cachedSfxVolume = -1f;
    private static float cachedMusicVolume = -1f;

    public static System.Action OnMusicVolumeChanged;

    public static float Sensitivity
    {
        get
        {
            if (cachedSensitivity < 0f)
                cachedSensitivity = PlayerPrefs.GetFloat(SensitivityKey, DefaultSensitivity);
            return cachedSensitivity;
        }
        set
        {
            cachedSensitivity = Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
            PlayerPrefs.SetFloat(SensitivityKey, cachedSensitivity);
            PlayerPrefs.Save();
        }
    }

    public static bool ToggleSprint
    {
        get
        {
            if (cachedToggleSprint < 0)
                cachedToggleSprint = PlayerPrefs.GetInt(ToggleSprintKey, 0);
            return cachedToggleSprint != 0;
        }
        set
        {
            cachedToggleSprint = value ? 1 : 0;
            PlayerPrefs.SetInt(ToggleSprintKey, cachedToggleSprint);
            PlayerPrefs.Save();
        }
    }

    public static float SfxVolume
    {
        get
        {
            if (cachedSfxVolume < 0f)
                cachedSfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, 1f));
            return cachedSfxVolume;
        }
        set
        {
            cachedSfxVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SfxVolumeKey, cachedSfxVolume);
            PlayerPrefs.Save();
        }
    }

    public static float MusicVolume
    {
        get
        {
            if (cachedMusicVolume < 0f)
                cachedMusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, 1f));
            return cachedMusicVolume;
        }
        set
        {
            cachedMusicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MusicVolumeKey, cachedMusicVolume);
            PlayerPrefs.Save();
            OnMusicVolumeChanged?.Invoke();
        }
    }
}
