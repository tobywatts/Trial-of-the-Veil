using UnityEngine;
using Cinemachine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    [Tooltip("Resting noise amplitude. Shake adds on top and decays back to it.")]
    public float baselineAmplitude = 0.5f;
    [Tooltip("Amplitude added on top of baseline at shake peak.")]
    public float hitShakeAmplitude = 3.5f;
    [Tooltip("Seconds to decay back to baseline.")]
    public float hitShakeDuration = 0.35f;

    private CinemachineBasicMultiChannelPerlin noise;
    private float shakeAmp;
    private float shakeTimer;
    private float shakeTotal;

    private void Awake()
    {
        Instance = this;
        var vcam = FindFirstObjectByType<CinemachineVirtualCamera>();
        if (vcam != null) noise = vcam.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void ShakeOnHit() => Shake(hitShakeAmplitude, hitShakeDuration);

    public void Shake(float amplitude, float duration)
    {
        if (noise == null || duration <= 0f) return;
        shakeAmp = amplitude;
        shakeTotal = duration;
        shakeTimer = duration;
    }

    private void Update()
    {
        if (noise == null) return;
        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.deltaTime;
            float t = Mathf.Max(0f, shakeTimer / shakeTotal);
            noise.m_AmplitudeGain = baselineAmplitude + shakeAmp * t;
        }
        else if (!Mathf.Approximately(noise.m_AmplitudeGain, baselineAmplitude))
        {
            noise.m_AmplitudeGain = baselineAmplitude;
        }
    }
}
