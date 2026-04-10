using UnityEngine;

public class HitVfxShrink : MonoBehaviour
{
    private float holdDuration;
    private float shrinkDuration;
    private float minScaleFraction;
    private Vector3 startScale;
    private float elapsed;
    private bool configured;
    private bool emissionStopped;
    private float lastScaleFactor = 1f;
    private ParticleSystem[] systems;
    private ParticleSystem.Particle[] buffer = new ParticleSystem.Particle[256];

    public void Configure(float holdSeconds, float shrinkSeconds, float minScaleFraction = 0.05f)
    {
        this.holdDuration = Mathf.Max(0f, holdSeconds);
        this.shrinkDuration = Mathf.Max(0.001f, shrinkSeconds);
        this.minScaleFraction = Mathf.Clamp(minScaleFraction, 0f, 1f);
        this.startScale = transform.localScale;
        this.elapsed = 0f;
        this.lastScaleFactor = 1f;
        this.emissionStopped = false;
        this.systems = GetComponentsInChildren<ParticleSystem>(true);
        this.configured = true;
    }

    private void Update()
    {
        if (!configured) return;
        elapsed += Time.deltaTime;

        if (elapsed <= holdDuration) return;

        float shrinkT = (elapsed - holdDuration) / shrinkDuration;
        if (shrinkT >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        float scaleFactor = Mathf.Lerp(1f, minScaleFraction, shrinkT);
        transform.localScale = startScale * scaleFactor;

        if (!emissionStopped)
        {
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] == null) continue;
                ParticleSystem.EmissionModule emission = systems[i].emission;
                emission.enabled = false;
            }
            emissionStopped = true;
        }

        float deltaFactor = lastScaleFactor > 0.0001f ? scaleFactor / lastScaleFactor : 0f;
        lastScaleFactor = scaleFactor;

        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem ps = systems[i];
            if (ps == null) continue;
            int maxCount = ps.main.maxParticles;
            if (buffer.Length < maxCount) buffer = new ParticleSystem.Particle[maxCount];
            int count = ps.GetParticles(buffer);
            for (int j = 0; j < count; j++)
            {
                buffer[j].startSize *= deltaFactor;
            }
            ps.SetParticles(buffer, count);
        }
    }
}
