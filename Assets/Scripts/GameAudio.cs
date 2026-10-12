using UnityEngine;

/// <summary>
/// Generador procedural de efectos de sonido sintetizados en tiempo de ejecución.
/// Proporciona audio feedback completo e inmersivo sin dependencias externas.
/// </summary>
public class GameAudio : MonoBehaviour
{
    public static GameAudio Instance { get; private set; }

    private AudioSource audioSource;
    private AudioClip clipHurt;
    private AudioClip clipEnemyHit;
    private AudioClip clipEnemyDefeat;
    private AudioClip clipStarCollect;
    private AudioClip clipCheckpoint;
    private AudioClip clipVictory;
    private AudioClip clipVacuumLaunch;
    private AudioClip clipGrappleLaunch;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D Sound

        GenerateClips();
    }

    private void GenerateClips()
    {
        clipHurt = CreateHurtTone();
        clipEnemyHit = CreateEnemyHitTone();
        clipEnemyDefeat = CreateExplosionTone();
        clipStarCollect = CreateChimeArpeggio();
        clipCheckpoint = CreateCheckpointTone();
        clipVictory = CreateVictoryFanfare();
        clipVacuumLaunch = CreatePopTone();
        clipGrappleLaunch = CreateWhooshTone();
    }

    public void PlayHurt() { PlayClip(clipHurt, 0.9f); }
    public void PlayEnemyHit() { PlayClip(clipEnemyHit, 0.85f); }
    public void PlayEnemyDefeat() { PlayClip(clipEnemyDefeat, 1f); }
    public void PlayStarCollect() { PlayClip(clipStarCollect, 0.95f); }
    public void PlayCheckpoint() { PlayClip(clipCheckpoint, 0.9f); }
    public void PlayVictory() { PlayClip(clipVictory, 1f); }
    public void PlayVacuumLaunch() { PlayClip(clipVacuumLaunch, 0.8f); }
    public void PlayGrappleLaunch() { PlayClip(clipGrappleLaunch, 0.7f); }

    private void PlayClip(AudioClip clip, float volume = 1f)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip, volume);
        }
    }

    private AudioClip CreateHurtTone()
    {
        int sampleRate = 44100;
        float duration = 0.28f;
        int samples = (int)(sampleRate * duration);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float freq = Mathf.Lerp(220f, 60f, t / duration);
            float env = Mathf.Exp(-9f * t / duration);
            float wave = Mathf.Sin(2f * Mathf.PI * freq * t) + 0.3f * (Random.value * 2f - 1f);
            data[i] = Mathf.Clamp(wave * env * 0.7f, -1f, 1f);
        }
        AudioClip clip = AudioClip.Create("HurtTone", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip CreateEnemyHitTone()
    {
        int sampleRate = 44100;
        float duration = 0.2f;
        int samples = (int)(sampleRate * duration);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float freq = Mathf.Lerp(480f, 180f, t / duration);
            float env = Mathf.Exp(-12f * t / duration);
            float noise = (Random.value * 2f - 1f) * 0.4f;
            float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.6f + noise;
            data[i] = Mathf.Clamp(wave * env, -1f, 1f);
        }
        AudioClip clip = AudioClip.Create("EnemyHitTone", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip CreateExplosionTone()
    {
        int sampleRate = 44100;
        float duration = 0.55f;
        int samples = (int)(sampleRate * duration);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-5f * t / duration);
            float rumble = Mathf.Sin(2f * Mathf.PI * 65f * t) * 0.5f;
            float noise = (Random.value * 2f - 1f) * 0.6f;
            data[i] = Mathf.Clamp((rumble + noise) * env * 0.85f, -1f, 1f);
        }
        AudioClip clip = AudioClip.Create("ExplosionTone", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip CreateChimeArpeggio()
    {
        int sampleRate = 44100;
        float duration = 0.6f;
        int samples = (int)(sampleRate * duration);
        float[] data = new float[samples];
        float[] freqs = { 587.33f, 739.99f, 880.00f, 1174.66f }; // D5, F#5, A5, D6
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float totalWave = 0f;
            for (int note = 0; note < freqs.Length; note++)
            {
                float noteStart = note * 0.1f;
                if (t >= noteStart)
                {
                    float noteT = t - noteStart;
                    float env = Mathf.Exp(-8f * noteT);
                    totalWave += Mathf.Sin(2f * Mathf.PI * freqs[note] * noteT) * env * 0.28f;
                }
            }
            data[i] = Mathf.Clamp(totalWave, -1f, 1f);
        }
        AudioClip clip = AudioClip.Create("StarChime", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip CreateCheckpointTone()
    {
        int sampleRate = 44100;
        float duration = 0.5f;
        int samples = (int)(sampleRate * duration);
        float[] data = new float[samples];
        float f1 = 440f, f2 = 659.25f; // A4, E5
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-6f * t / duration);
            float wave = (Mathf.Sin(2f * Mathf.PI * f1 * t) + Mathf.Sin(2f * Mathf.PI * f2 * t)) * 0.4f * env;
            data[i] = Mathf.Clamp(wave, -1f, 1f);
        }
        AudioClip clip = AudioClip.Create("CheckpointTone", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip CreateVictoryFanfare()
    {
        int sampleRate = 44100;
        float duration = 1.4f;
        int samples = (int)(sampleRate * duration);
        float[] data = new float[samples];
        // Fanfare notes: C5, E5, G5, C6 (triad with finale)
        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.50f };
        float[] noteTimes = { 0.0f, 0.22f, 0.44f, 0.70f };
        float[] noteDurs = { 0.25f, 0.25f, 0.30f, 0.70f };

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float total = 0f;
            for (int n = 0; n < notes.Length; n++)
            {
                if (t >= noteTimes[n] && t < noteTimes[n] + noteDurs[n])
                {
                    float localT = t - noteTimes[n];
                    float env = Mathf.Sin(Mathf.Clamp01(localT / noteDurs[n]) * Mathf.PI);
                    total += Mathf.Sin(2f * Mathf.PI * notes[n] * localT) * env * 0.35f;
                }
            }
            data[i] = Mathf.Clamp(total, -1f, 1f);
        }
        AudioClip clip = AudioClip.Create("VictoryFanfare", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip CreatePopTone()
    {
        int sampleRate = 44100;
        float duration = 0.16f;
        int samples = (int)(sampleRate * duration);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float freq = Mathf.Lerp(600f, 120f, t / duration);
            float env = Mathf.Exp(-16f * t / duration);
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.7f;
        }
        AudioClip clip = AudioClip.Create("PopTone", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip CreateWhooshTone()
    {
        int sampleRate = 44100;
        float duration = 0.35f;
        int samples = (int)(sampleRate * duration);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
            float noise = (Random.value * 2f - 1f);
            data[i] = noise * env * 0.45f;
        }
        AudioClip clip = AudioClip.Create("WhooshTone", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
