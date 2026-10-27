using UnityEngine;

[DisallowMultipleComponent]
public sealed class AudioManager : MonoBehaviour
{
    // 비워두면 Resources/Sounds의 8비트 사운드를 사용
    [SerializeField] AudioClip assignmentHitSound;
    [SerializeField] AudioClip feverHitSound;
    [SerializeField] AudioClip gradeUpSound;
    [SerializeField] AudioClip roundEndSound;
    [SerializeField] AudioClip buySound;
    [SerializeField] AudioClip bgm;

    [SerializeField] AudioSource audioSource;
    AudioSource bgmSource;
    // 부수는 소리: 공격 주체별 폴더에서 무작위 변주
    AudioClip[] clearSounds, robotClearSounds, toolClearSounds;
    float lastClearAt = -1f;

    void Awake()
    {
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        assignmentHitSound = Load(assignmentHitSound, "hit");
        feverHitSound = Load(feverHitSound, "fever_hit");
        clearSounds = Resources.LoadAll<AudioClip>("Sounds/Clear");
        robotClearSounds = Resources.LoadAll<AudioClip>("Sounds/ClearRobot");
        toolClearSounds = Resources.LoadAll<AudioClip>("Sounds/ClearTool");
        gradeUpSound = Load(gradeUpSound, "gradeup");
        roundEndSound = Load(roundEndSound, "round_end");
        buySound = Load(buySound, "buy");
        bgm = Load(bgm, "bgm");

        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.clip = bgm;
        bgmSource.loop = true;
        bgmSource.spatialBlend = 0f;
        bgmSource.Play();
    }

    void Update()
    {
        // 옵션 변경을 바로 반영
        OptionManager option = OptionManager.Instance();
        bgmSource.volume = option.IsMutedBGM ? 0f : option.BGMVolume * 0.5f;
    }

    static AudioClip Load(AudioClip clip, string name) => clip != null ? clip : Resources.Load<AudioClip>("Sounds/" + name);

    public void PlayAssignmentHit(bool feverActive) => PlayOneShot(feverActive ? feverHitSound : assignmentHitSound);
    public void PlayClear() => PlayClearFrom(clearSounds);
    public void PlayRobotClear() => PlayClearFrom(robotClearSounds);
    public void PlayToolClear() => PlayClearFrom(toolClearSounds);
    public void PlayGradeUp() => PlayOneShot(gradeUpSound);
    public void PlayRoundEnd() => PlayOneShot(roundEndSound);
    public void PlayBuy() => PlayOneShot(buySound);

    void PlayClearFrom(AudioClip[] clips)
    {
        // 한꺼번에 여러 장이 부서지면 소리가 뭉개지므로 0.04초에 한 번만
        if (clips.Length == 0 || Time.unscaledTime - lastClearAt < 0.04f) return;
        lastClearAt = Time.unscaledTime;
        PlayOneShot(clips[Random.Range(0, clips.Length)]);
    }

    void PlayOneShot(AudioClip clip)
    {
        if (OptionManager.Instance().IsMutedSFX)
        {
            return;
        }

        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip, OptionManager.Instance().SFXVolume);
    }
}
