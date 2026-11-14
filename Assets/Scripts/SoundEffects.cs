using UnityEngine;

/// <summary>
/// Lightweight procedural audio synthesizer that generates arcade sound effects at runtime.
/// No external audio clips required — produces crisp blips, pings, twangs, and impacts!
/// </summary>
public class SoundEffects : MonoBehaviour
{
    public static SoundEffects Instance { get; private set; }

    private AudioSource _audioSource;
    private AudioClip _shootClip;
    private AudioClip _bowDrawClip;
    private AudioClip _bowTwangClip;
    private AudioClip _ricochetClip;
    private AudioClip _hitEnemyClip;
    private AudioClip _hitPlayerClip;
    private AudioClip _dashClip;
    private AudioClip _explodeClip;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f; // 2D sound
        }

        GenerateClips();
    }

    private void GenerateClips()
    {
        _bowDrawClip = CreateBowDrawClip(0.18f, 0.5f);
        _bowTwangClip = CreateBowTwangClip(0.22f, 0.85f);
        _shootClip = _bowTwangClip; // Legacy alias
        _ricochetClip = CreateToneClip(1100f, 850f, 0.06f, 0.7f);   // Metallic ping
        _hitEnemyClip = CreateNoiseClip(0.12f, 0.8f);               // Impact punch
        _hitPlayerClip = CreateToneClip(200f, 80f, 0.18f, 0.9f);    // Heavy thud
        _dashClip = CreateToneClip(300f, 600f, 0.12f, 0.5f);        // Whoosh
        _explodeClip = CreateNoiseClip(0.32f, 0.95f);               // Deep boom
    }

    public static void PlayBowDraw()
    {
        if (Instance != null && Instance._bowDrawClip != null)
        {
            Instance._audioSource.pitch = 1f + Random.Range(-0.06f, 0.06f);
            Instance._audioSource.PlayOneShot(Instance._bowDrawClip, 0.45f);
            Instance._audioSource.pitch = 1f;
        }
    }

    public static void PlayBowRelease(float power = 1f)
    {
        if (Instance != null && Instance._bowTwangClip != null)
        {
            // Pitch scales slightly with draw tension: higher tension = crisper snap
            float pitch = Mathf.Lerp(0.92f, 1.15f, power) + Random.Range(-0.04f, 0.04f);
            float volume = Mathf.Lerp(0.6f, 0.95f, power);
            Instance._audioSource.pitch = pitch;
            Instance._audioSource.PlayOneShot(Instance._bowTwangClip, volume);
            Instance._audioSource.pitch = 1f;
        }
    }

    public static void PlayShoot()
    {
        PlayBowRelease(1f);
    }

    public static void PlayRicochet(float pitchVariance = 0.1f)
    {
        if (Instance != null && Instance._ricochetClip != null)
        {
            Instance._audioSource.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
            Instance._audioSource.PlayOneShot(Instance._ricochetClip, 0.7f);
            Instance._audioSource.pitch = 1f;
        }
    }

    public static void PlayHitEnemy()
    {
        if (Instance != null && Instance._hitEnemyClip != null)
            Instance._audioSource.PlayOneShot(Instance._hitEnemyClip, 0.85f);
    }

    public static void PlayEnemyExplode()
    {
        if (Instance != null && Instance._explodeClip != null)
        {
            Instance._audioSource.pitch = Random.Range(0.85f, 1.15f);
            Instance._audioSource.PlayOneShot(Instance._explodeClip, 0.9f);
            Instance._audioSource.pitch = 1f;
        }
    }

    public static void PlayHitPlayer()
    {
        if (Instance != null && Instance._hitPlayerClip != null)
            Instance._audioSource.PlayOneShot(Instance._hitPlayerClip, 1.0f);
    }

    public static void PlayDash()
    {
        if (Instance != null && Instance._dashClip != null)
            Instance._audioSource.PlayOneShot(Instance._dashClip, 0.7f);
    }

    private AudioClip CreateToneClip(float startFreq, float endFreq, float duration, float volume)
    {
        int sampleRate = 44100;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            float freq = Mathf.Lerp(startFreq, endFreq, t);
            float envelope = 1f - t; // Exponential-ish decay
            envelope = envelope * envelope;
            samples[i] = Mathf.Sin(2f * Mathf.PI * freq * (i / (float)sampleRate)) * envelope * volume;
        }

        AudioClip clip = AudioClip.Create("ToneFX", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateNoiseClip(float duration, float volume)
    {
        int sampleRate = 44100;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            float envelope = (1f - t) * (1f - t);
            samples[i] = (Random.value * 2f - 1f) * envelope * volume;
        }

        AudioClip clip = AudioClip.Create("NoiseFX", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateBowTwangClip(float duration = 0.22f, float volume = 0.85f)
    {
        int sampleRate = 44100;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        float fundamental = 265f; // Resonant bowstring frequency
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float progress = (float)i / sampleCount;

            // Sharp initial transient snap
            float transient = Mathf.Exp(-t * 85f) * (Random.value * 2f - 1f) * 0.35f;

            // Plucked string harmonics with rapid decay
            float h1 = Mathf.Sin(2f * Mathf.PI * fundamental * t) * Mathf.Exp(-t * 16f);
            float h2 = Mathf.Sin(2f * Mathf.PI * (fundamental * 2.01f) * t) * Mathf.Exp(-t * 24f) * 0.45f;
            float h3 = Mathf.Sin(2f * Mathf.PI * (fundamental * 3.03f) * t) * Mathf.Exp(-t * 34f) * 0.25f;

            float wave = (transient + h1 + h2 + h3) * volume;
            samples[i] = Mathf.Clamp(wave, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("BowTwangFX", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateBowDrawClip(float duration = 0.18f, float volume = 0.5f)
    {
        int sampleRate = 44100;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float progress = (float)i / sampleCount;

            // Subtle rising tension string creak
            float freq = Mathf.Lerp(140f, 280f, progress);
            float noise = (Random.value * 2f - 1f) * 0.12f;
            float env = Mathf.Sin(progress * Mathf.PI * 0.95f);

            float wave = (Mathf.Sin(2f * Mathf.PI * freq * t) * 0.75f + noise) * env * volume;
            samples[i] = Mathf.Clamp(wave, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("BowDrawFX", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
