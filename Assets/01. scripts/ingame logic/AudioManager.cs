using UnityEngine;

[DisallowMultipleComponent]
public sealed class AudioManager : MonoBehaviour
{
    [SerializeField] AudioClip missClickWarning;
    [SerializeField] AudioClip assignmentHitSound;
    [SerializeField] AudioClip feverHitSound;

    [SerializeField] AudioSource audioSource;
    AudioClip generatedWarningClip;
    AudioClip generatedHitClip;
    AudioClip generatedFeverHitClip;

    void Awake()
    {
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        if (missClickWarning == null) generatedWarningClip = CreateWarningClip();
        if (assignmentHitSound == null) generatedHitClip = CreateToneClip("Assignment Hit", 430f, 0.075f, 0.24f);
        if (feverHitSound == null) generatedFeverHitClip = CreateToneClip("Fever Hit", 980f, 0.095f, 0.30f);
    }

    public void PlayMissClickWarning()
    {
        PlayOneShot(missClickWarning != null ? missClickWarning : generatedWarningClip);
    }

    public void PlayAssignmentHit(bool feverActive)
    {
        AudioClip clip = feverActive
            ? (feverHitSound != null ? feverHitSound : generatedFeverHitClip)
            : (assignmentHitSound != null ? assignmentHitSound : generatedHitClip);
        PlayOneShot(clip);
    }

    void PlayOneShot(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    void OnDestroy()
    {
        Destroy(generatedWarningClip);
        Destroy(generatedHitClip);
        Destroy(generatedFeverHitClip);
    }

    static AudioClip CreateWarningClip()
    {
        const int sampleRate = 44100;
        const float duration = 0.20f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        var samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)sampleRate;
            float frequency = time < 0.09f ? 980f : 680f;
            float envelope = Mathf.Min(1f, time * 80f) * Mathf.Min(1f, (duration - time) * 35f);
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * time) * envelope * 0.35f;
        }
        AudioClip clip = AudioClip.Create("Overload Miss Warning", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    static AudioClip CreateToneClip(string clipName, float frequency, float duration, float volume)
    {
        const int sampleRate = 44100;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        var samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)sampleRate;
            float envelope = Mathf.Min(1f, time * 100f) * Mathf.Min(1f, (duration - time) * 45f);
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * time) * envelope * volume;
        }
        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
