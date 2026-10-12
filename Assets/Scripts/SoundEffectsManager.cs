using UnityEngine;

/// <summary>
/// Genera y reproduce efectos de sonido procedurales de alta calidad para el nivel.
/// Garantiza audio inmersivo sin requerir archivos de audio externos.
/// </summary>
public class SoundEffectsManager : MonoBehaviour
{
    public static SoundEffectsManager Instance { get; private set; }

    private AudioSource audioSource;

    private AudioClip starSound;
    private AudioClip checkpointSound;
    private AudioClip hurtSound;
    private AudioClip enemyDefeatSound;
    private AudioClip victorySound;
    private AudioClip laserHumSound;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D UI sound

        GenerateClips();
    }

    private void GenerateClips()
    {
        starSound = CreateStarPickupClip();
        checkpointSound = CreateCheckpointClip();
        hurtSound = CreateHurtClip();
        enemyDefeatSound = CreateDefeatClip();
        victorySound = CreateVictoryClip();
    }

    public void PlayStar() => PlayClip(starSound, 0.9f);
    public void PlayCheckpoint() => PlayClip(checkpointSound, 0.85f);
    public void PlayHurt() => PlayClip(hurtSound, 1.0f);
    public void PlayEnemyDefeat() => PlayClip(enemyDefeatSound, 0.9f);
    public void PlayVictory() => PlayClip(victorySound, 1.0f);

    private void PlayClip(AudioClip clip, float volume)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip, volume);
        }
    }

    private AudioClip CreateStarPickupClip()
    {
        int sampleRate = 44100;
        float duration = 0.45f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        // Arpegio brillante: C6, E6, G6, C7
        float[] freqs = { 1046.50f, 1318.51f, 1567.98f, 2093.00f };
        float noteDuration = duration / freqs.Length;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            int noteIdx = Mathf.Clamp((int)(t / noteDuration), 0, freqs.Length - 1);
            float noteT = t - noteIdx * noteDuration;
            float freq = freqs[noteIdx];

            // Sine wave with bell envelope
            float env = Mathf.Exp(-noteT * 12f);
            float wave = Mathf.Sin(2f * Mathf.PI * freq * t) + 0.35f * Mathf.Sin(4f * Mathf.PI * freq * t);
            samples[i] = wave * env * 0.35f;
        }

        AudioClip clip = AudioClip.Create("SFX_Star", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateCheckpointClip()
    {
        int sampleRate = 44100;
        float duration = 0.7f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 4.5f);
            // Resonant chord: E5 + B5
            float wave = 0.6f * Mathf.Sin(2f * Mathf.PI * 659.25f * t)
                       + 0.4f * Mathf.Sin(2f * Mathf.PI * 987.77f * t);
            samples[i] = wave * env * 0.4f;
        }

        AudioClip clip = AudioClip.Create("SFX_Checkpoint", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateHurtClip()
    {
        int sampleRate = 44100;
        float duration = 0.3f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 14f);
            // Pitch slide down for punch impact
            float freq = Mathf.Lerp(220f, 70f, t / duration);
            float wave = Mathf.Sin(2f * Mathf.PI * freq * t);
            // Add distortion noise
            float noise = (Random.value * 2f - 1f) * 0.25f;
            samples[i] = (wave + noise) * env * 0.5f;
        }

        AudioClip clip = AudioClip.Create("SFX_Hurt", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateDefeatClip()
    {
        int sampleRate = 44100;
        float duration = 0.5f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 7f);
            // Deflagration impact noise + low sweep
            float noise = (Random.value * 2f - 1f);
            float lowSweep = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(180f, 40f, t) * t);
            samples[i] = (noise * 0.6f + lowSweep * 0.4f) * env * 0.55f;
        }

        AudioClip clip = AudioClip.Create("SFX_Defeat", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateVictoryClip()
    {
        int sampleRate = 44100;
        float duration = 1.6f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        // Fanfarria triunfal: C5, E5, G5, C6 (sostenido)
        float[] chordTimes = { 0.0f, 0.25f, 0.5f, 0.75f };
        float[] chordFreqs = { 523.25f, 659.25f, 783.99f, 1046.50f };

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float totalSample = 0f;

            for (int note = 0; note < chordTimes.Length; note++)
            {
                if (t >= chordTimes[note])
                {
                    float noteT = t - chordTimes[note];
                    float env = Mathf.Clamp01(1f - noteT / 1.0f) * Mathf.Exp(-noteT * 1.5f);
                    float freq = chordFreqs[note];
                    float wave = Mathf.Sin(2f * Mathf.PI * freq * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * freq * t);
                    totalSample += wave * env * 0.25f;
                }
            }

            samples[i] = Mathf.Clamp(totalSample, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("SFX_Victory", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
