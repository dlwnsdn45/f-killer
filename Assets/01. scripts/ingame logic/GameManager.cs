using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class GameManager : MonoBehaviour
{
    [Serializable]
    public sealed class SpawnEntry
    {
        public AssignmentData data;
        [Min(0f)] public float weight = 1f;

        public bool IsValid => data != null && weight > 0f && !float.IsInfinity(weight);
    }

    const float RoundDurationSeconds = 15f;
    static readonly string[] GradeNames = { "F", "F+", "D", "D+", "C", "C+", "B", "B+", "A", "A+", "졸업" };
    static readonly int[] GradeThresholds = { 0, 15, 40, 150, 320, 500, 700, 1000, 1800, 2500 };
    [SerializeField] GameUIController gameUI;
    [Header("생성")]
    [SerializeField] List<SpawnEntry> assignmentTable = new List<SpawnEntry>();
    [SerializeField, Min(0.01f)] float spawnIntervalSeconds = 1f;
    [Header("과부하")]
    [SerializeField, Min(0f)] float missClickPenaltySeconds = 0.5f;
    [SerializeField] AudioClip missClickWarning;
    [SerializeField] AudioClip assignmentHitSound;
    [SerializeField] AudioClip feverHitSound;

    bool isPlaying;
    bool hasStarted;
    bool progressChangedSubscribed;
    float elapsedSpawnSeconds;
    float elapsedRobotSeconds;
    int comboCount;
    double comboExpiresAt;
    double feverExpiresAt;
    bool feverWasActive;
    double deadline;
    int totalPoints;
    int overloadBlocksRemaining;
    bool sleepRescueUsed;
    AudioSource warningAudioSource;
    AudioClip generatedWarningClip;
    AudioClip generatedHitClip;
    AudioClip generatedFeverHitClip;
    SkillProgress skillProgress;
    public SkillProgress SkillProgress => skillProgress ?? (skillProgress = new SkillProgress());
    readonly List<AssignmentBase> activeAssignments = new List<AssignmentBase>();

    bool HasSkill(string id) => SkillProgress.IsOwned(gameUI != null ? gameUI.SkillTable : null, id);

    float RoundDuration => RoundDurationSeconds
        + (HasSkill("skill-19") ? 3f : 0f)
        + (HasSkill("skill-21") ? 5f : 0f)
        + (HasSkill("skill-22") ? 7f : 0f)
        + (HasSkill("skill-24") ? 10f : 0f);

    float SpawnInterval
    {
        get
        {
            float reduction = (HasSkill("skill-1") ? 0.03f : 0f)
                + (HasSkill("skill-2") ? 0.05f : 0f)
                + (HasSkill("skill-4") ? 0.08f : 0f)
                + (HasSkill("skill-5") ? 0.10f : 0f)
                + (HasSkill("skill-7") ? 0.15f : 0f)
                + (HasSkill("skill-8") ? 0.20f : 0f);
            float gradeInterval = GradeSpawnInterval(CurrentGradeIndex);
            return Mathf.Max(0.05f, gradeInterval * (1f - Mathf.Clamp01(reduction)));
        }
    }

    int CurrentGradeIndex => GetGradeIndex(totalPoints);

    static int GetGradeIndex(int points)
    {
        int gradeIndex = 0;
        for (int i = 1; i < GradeThresholds.Length; i++)
            if (points >= GradeThresholds[i]) gradeIndex = i;
        return gradeIndex;
    }

    static float GradeSpawnInterval(int gradeIndex)
    {
        if (gradeIndex <= 1) return 1.5f;
        if (gradeIndex <= 3) return 1.2f;
        if (gradeIndex <= 5) return 1f;
        return 0.5f;
    }

    static bool IsAvailableAtGrade(AssignmentKind kind, int gradeIndex)
    {
        if (kind == AssignmentKind.Regular || kind == AssignmentKind.TeamProject) return true;
        if (kind == AssignmentKind.Bug) return gradeIndex >= 1;
        if (kind == AssignmentKind.SemesterProject) return gradeIndex >= 2;
        if (kind == AssignmentKind.DrinkingParty) return gradeIndex >= 3;
        if (kind == AssignmentKind.GraduationProject) return gradeIndex >= 4;
        if (kind == AssignmentKind.Sleep) return gradeIndex >= 5;
        return kind == AssignmentKind.Contest && gradeIndex >= 6;
    }

    static int GradeSpawnCount(int gradeIndex)
    {
        if (gradeIndex <= 3) return 1;
        if (gradeIndex <= 5) return UnityEngine.Random.Range(1, 3);
        return UnityEngine.Random.Range(2, 4);
    }

    void UpdateAcademicGrade()
    {
        int gradeIndex = CurrentGradeIndex;
        string nextGrade = gradeIndex + 1 < GradeNames.Length ? GradeNames[gradeIndex + 1] : "-";
        gameUI.UpdateAcademicGrade(GradeNames[gradeIndex], nextGrade);
    }

    void Start()
    {
        gameUI.BindUI();
        SubscribeProgressChanged();
        if (gameUI.IsBound) StartGame();
    }

    void OnDestroy()
    {
        if (progressChangedSubscribed && skillProgress != null)
            skillProgress.Changed -= HandleProgressChanged;
    }

    void SubscribeProgressChanged()
    {
        if (progressChangedSubscribed) return;
        SkillProgress.Changed += HandleProgressChanged;
        progressChangedSubscribed = true;
        HandleProgressChanged();
    }

    void HandleProgressChanged() => gameUI?.UpdatePoints(SkillProgress.Points);

    void Awake()
    {
        warningAudioSource = GetComponent<AudioSource>();
        if (warningAudioSource == null) warningAudioSource = gameObject.AddComponent<AudioSource>();
        warningAudioSource.playOnAwake = false;
        warningAudioSource.spatialBlend = 0f;
        if (missClickWarning == null) generatedWarningClip = CreateWarningClip();
        if (assignmentHitSound == null) generatedHitClip = CreateToneClip("Assignment Hit", 430f, 0.075f, 0.24f);
        if (feverHitSound == null) generatedFeverHitClip = CreateToneClip("Fever Hit", 980f, 0.095f, 0.30f);
    }

    void Update()
    {
        if (!hasStarted && gameUI.IsBound) StartGame();
        if (!isPlaying) return;
        UpdateTimer();
        if (!isPlaying) return;
        UpdateComboAndFever();

        elapsedSpawnSeconds += Time.unscaledDeltaTime;
        float interval = SpawnInterval;
        while (elapsedSpawnSeconds >= interval)
        {
            elapsedSpawnSeconds -= interval;
            int spawnCount = GradeSpawnCount(CurrentGradeIndex)
                + (HasSkill("skill-3") ? 1 : 0)
                + (HasSkill("skill-6") ? 2 : 0);
            for (int i = 0; i < spawnCount; i++) Spawn();
        }

        UpdateRobots();
        if (!isPlaying) return;
        if (!HasSkill("skill-39"))
            foreach (AssignmentBase assignment in activeAssignments)
                if (assignment.Data.Kind == AssignmentKind.Bug)
                    gameUI.MoveBug(assignment, Time.unscaledDeltaTime);
    }

    public void StartGame()
    {
        SubscribeProgressChanged();
        isPlaying = false;
        ClearAssignments();
        totalPoints = 0;
        elapsedSpawnSeconds = 0f;
        elapsedRobotSeconds = 0f;
        comboCount = 0;
        comboExpiresAt = 0;
        feverExpiresAt = 0;
        feverWasActive = false;
        gameUI.SetFeverVisual(false);
        gameUI.UpdateComboState(0, 0f, HasSkill("skill-26"), false, 0f);
        overloadBlocksRemaining = (HasSkill("skill-20") ? 3 : 0) + (HasSkill("skill-23") ? 5 : 0);
        sleepRescueUsed = false;
        deadline = Time.unscaledTimeAsDouble + RoundDuration;
        isPlaying = true;
        hasStarted = true;
        gameUI.ShowGame();
        gameUI.UpdatePoints(SkillProgress.Points);
        UpdateAcademicGrade();
        gameUI.UpdateTimer(RoundDuration);
    }

    void UpdateTimer()
    {
        float remainingSeconds = Mathf.Max(0f, (float)(deadline - Time.unscaledTimeAsDouble));
        gameUI.UpdateTimer(remainingSeconds);
        if (remainingSeconds <= 0f) EndGame();
    }

    void Spawn()
    {
        float totalWeight = 0f;
        int gradeIndex = CurrentGradeIndex;
        foreach (SpawnEntry entry in assignmentTable)
            if (entry != null && entry.IsValid && IsAvailableAtGrade(entry.data.Kind, gradeIndex))
                totalWeight += entry.weight;
        if (totalWeight <= 0f || float.IsInfinity(totalWeight)) return;

        float roll = UnityEngine.Random.value * totalWeight;
        AssignmentData selected = null;
        foreach (SpawnEntry entry in assignmentTable)
        {
            if (entry == null || !entry.IsValid || !IsAvailableAtGrade(entry.data.Kind, gradeIndex)) continue;
            selected = entry.data;
            roll -= entry.weight;
            if (roll < 0f) break;
        }
        bool isGolden = selected.GoldChance > 0f && UnityEngine.Random.value < selected.GoldChance;
        var assignment = new AssignmentBase();
        assignment.Initialize(selected, isGolden);
        RegisterAssignment(assignment);
    }

    public void RegisterAssignment(AssignmentBase assignment)
    {
        if (!isPlaying || assignment == null || assignment.Data == null || assignment.IsCompleted
            || activeAssignments.Contains(assignment)) return;
        activeAssignments.Add(assignment);
        gameUI.AddAssignment(assignment);
    }

    public void HandleAssignmentClicked(AssignmentBase assignment)
    {
        if (!isPlaying) return;
        UpdateTimer();
        if (!isPlaying || assignment == null || !activeAssignments.Contains(assignment)) return;
        int damage = 1 + (HasSkill("skill-9") ? 1 : 0)
            + (HasSkill("skill-10") ? 2 : 0)
            + (HasSkill("skill-11") ? 3 : 0);
        bool feverActive = Time.unscaledTimeAsDouble < feverExpiresAt;
        if (feverActive) damage *= 2;
        int criticalChance = (HasSkill("skill-12") ? 10 : 0) + (HasSkill("skill-14") ? 20 : 0);
        bool critical = criticalChance > 0 && UnityEngine.Random.Range(0, 100) < criticalChance;
        if (critical)
        {
            damage *= 3;
        }
        PlayAssignmentHit(feverActive);
        if (feverActive) gameUI.PlayFeverHitEffect(assignment);

        var nearby = new List<AssignmentBase>();
        if (HasSkill("skill-13"))
            foreach (AssignmentBase other in activeAssignments)
                if (other != assignment && gameUI.AreAssignmentsNear(assignment, other, 180f)) nearby.Add(other);
        DamageAssignment(assignment, damage, true);
        foreach (AssignmentBase other in nearby) DamageAssignment(other, 1);
    }

    public void HandleMissClick(Vector2 position)
    {
        if (!isPlaying) return;
        PlayMissClickWarning();
        if (overloadBlocksRemaining > 0)
        {
            overloadBlocksRemaining--;
            gameUI.ShowOverloadFeedback(position, true, overloadBlocksRemaining);
            return;
        }

        deadline -= Mathf.Max(0f, missClickPenaltySeconds);
        UpdateTimer();
        gameUI.ShowOverloadFeedback(position, false, overloadBlocksRemaining);
    }

    void PlayMissClickWarning()
    {
        if (warningAudioSource == null) return;
        AudioClip clip = missClickWarning != null ? missClickWarning : generatedWarningClip;
        if (clip != null) warningAudioSource.PlayOneShot(clip);
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

    void PlayAssignmentHit(bool feverActive)
    {
        if (warningAudioSource == null) return;
        AudioClip clip = feverActive
            ? (feverHitSound != null ? feverHitSound : generatedFeverHitClip)
            : (assignmentHitSound != null ? assignmentHitSound : generatedHitClip);
        if (clip != null) warningAudioSource.PlayOneShot(clip);
    }

    void DamageAssignment(AssignmentBase assignment, int damage, bool manualHit = false)
    {
        if (!isPlaying || assignment == null || !activeAssignments.Contains(assignment)) return;
        if (!manualHit && assignment.Data.Kind == AssignmentKind.Sleep) return;
        assignment.ApplyClick(damage);
        if (assignment.IsCompleted) CompleteAssignment(assignment, manualHit);
        else gameUI.RefreshAssignment(assignment);
    }

    void UpdateRobots()
    {
        int robotCount = 1 + (HasSkill("skill-38") ? 2 : 0);
        if (!HasSkill("skill-34") || activeAssignments.Count == 0) return;
        float interval = Mathf.Max(0.25f, 3f - (HasSkill("skill-35") ? 0.5f : 0f));
        elapsedRobotSeconds += Time.unscaledDeltaTime;
        while (isPlaying && elapsedRobotSeconds >= interval && activeAssignments.Count > 0)
        {
            elapsedRobotSeconds -= interval;
            int damage = 1 + (HasSkill("skill-36") ? 1 : 0) + (HasSkill("skill-37") ? 2 : 0);
            for (int i = 0; isPlaying && i < robotCount && activeAssignments.Count > 0; i++)
            {
                AssignmentBase target = activeAssignments[UnityEngine.Random.Range(0, activeAssignments.Count)];
                gameUI.PlayRobotAttackEffect(target);
                DamageAssignment(target, damage);
            }
        }
    }

    void CompleteAssignment(AssignmentBase assignment, bool manualHit)
    {
        if (!assignment.IsCompleted || !activeAssignments.Remove(assignment)) return;
        if (manualHit)
        {
            RegisterComboBreak();
            if (HasSkill("skill-26")) gameUI.ShowComboFeedback(assignment, comboCount);
        }
        float multiplier = (HasSkill("skill-15") ? 1.25f : 1f)
            * (HasSkill("skill-16") ? 1.5f : 1f)
            * (HasSkill("skill-17") ? 2f : 1f);
        if (HasSkill("skill-26"))
        {
            float comboBonus = Mathf.Min(5, comboCount / 25) * 0.05f;
            if (HasSkill("skill-27")) comboBonus *= 2f;
            multiplier *= 1f + comboBonus;
        }
        int reward = Mathf.Max(1, Mathf.RoundToInt(assignment.Points * multiplier));
        totalPoints += reward;
        SkillProgress.AddPoints(reward);
        UpdateAcademicGrade();
        gameUI.RemoveAssignment(assignment);

        if (manualHit && assignment.Data.Kind == AssignmentKind.DrinkingParty)
        {
            double now = Time.unscaledTimeAsDouble;
            double remaining = Math.Max(0d, deadline - now);
            deadline = now + remaining * 0.5d;
            UpdateTimer();
        }
        else if (manualHit && assignment.Data.Kind == AssignmentKind.Sleep)
        {
            if (HasSkill("skill-43") && !sleepRescueUsed) sleepRescueUsed = true;
            else EndGame();
        }
    }

    void RegisterComboBreak()
    {
        if (!HasSkill("skill-26")) return;
        double now = Time.unscaledTimeAsDouble;
        comboCount = comboCount > 0 && now <= comboExpiresAt ? comboCount + 1 : 1;
        comboExpiresAt = now + 1.3 + (HasSkill("skill-27") ? 2.0 : 0.0);
        if (HasSkill("skill-28") && comboCount >= 25 && comboCount % 25 == 0)
            feverExpiresAt = now + 2.0;
    }

    void UpdateComboAndFever()
    {
        double now = Time.unscaledTimeAsDouble;
        if (comboCount > 0 && now >= comboExpiresAt) comboCount = 0;
        bool feverActive = now < feverExpiresAt;
        if (feverActive != feverWasActive)
        {
            feverWasActive = feverActive;
            gameUI.SetFeverVisual(feverActive);
        }
        gameUI.UpdateComboState(comboCount,
            comboCount > 0 ? Mathf.Max(0f, (float)(comboExpiresAt - now)) : 0f,
            HasSkill("skill-26"), feverActive,
            feverActive ? Mathf.Max(0f, (float)(feverExpiresAt - now)) : 0f);
    }

    public void EndGame()
    {
        if (!isPlaying) return;
        isPlaying = false;
        feverWasActive = false;
        gameUI.SetFeverVisual(false);
        gameUI.UpdateComboState(0, 0f, HasSkill("skill-26"), false, 0f);
        gameUI.UpdateTimer(0f);
        int finalGradeIndex = CurrentGradeIndex;
        string finalGrade = totalPoints >= 2500 ? "졸업" : GradeNames[finalGradeIndex];
        gameUI.ShowResult(totalPoints, finalGrade);
    }

    void ClearAssignments()
    {
        activeAssignments.Clear();
        gameUI.ClearAssignments();
    }
}
