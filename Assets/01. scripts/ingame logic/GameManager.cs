using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AssignmentSpawner), typeof(AudioManager))]
public sealed class GameManager : MonoBehaviour
{
    const float RoundDurationSeconds = 15f;
    static readonly string[] GradeNames = { "F", "F+", "D", "D+", "C", "C+", "B", "B+", "A", "A+", "졸업" };
    static readonly int[] GradeThresholds = { 0, 15, 40, 150, 320, 500, 700, 1000, 1800, 2500 };
    [SerializeField] GameUIController gameUI;
    [SerializeField] AssignmentSpawner assignmentSpawner;
    [SerializeField] AudioManager audioManager;
    [Header("과부하")]
    [SerializeField, Min(0f)] float missClickPenaltySeconds = 0.5f;

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
    SkillProgress skillProgress;
    public SkillProgress SkillProgress => skillProgress ?? (skillProgress = new SkillProgress());
    readonly List<AssignmentBase> activeAssignments = new List<AssignmentBase>();

    bool HasSkill(string id) => SkillProgress.IsOwned(gameUI != null ? gameUI.SkillTable : null, id);

    float RoundDuration => RoundDurationSeconds
        + (HasSkill("skill-19") ? 3f : 0f)
        + (HasSkill("skill-21") ? 5f : 0f)
        + (HasSkill("skill-22") ? 7f : 0f)
        + (HasSkill("skill-24") ? 10f : 0f);

    int CurrentGradeIndex => GetGradeIndex(totalPoints);

    static int GetGradeIndex(int points)
    {
        int gradeIndex = 0;
        for (int i = 1; i < GradeThresholds.Length; i++)
            if (points >= GradeThresholds[i]) gradeIndex = i;
        return gradeIndex;
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
        if (assignmentSpawner == null) assignmentSpawner = GetComponent<AssignmentSpawner>();
        if (audioManager == null) audioManager = GetComponent<AudioManager>();
    }

    void Update()
    {
        if (!hasStarted && gameUI.IsBound) StartGame();
        if (!isPlaying) return;
        UpdateTimer();
        if (!isPlaying) return;
        UpdateComboAndFever();

        if (assignmentSpawner != null)
        {
            elapsedSpawnSeconds += Time.unscaledDeltaTime;
            int spawnGradeIndex = SkillProgress.BestGradeIndex;
            float interval = assignmentSpawner.GetInterval(spawnGradeIndex, HasSkill);
            while (elapsedSpawnSeconds >= interval)
            {
                elapsedSpawnSeconds -= interval;
                assignmentSpawner.Spawn(spawnGradeIndex, HasSkill, RegisterAssignment);
            }
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
        gameUI.UpdateFeverState(false, 0f);
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

    public void AddPointsFromButton()
    {
        SkillProgress.AddPoints(1000);
    }

    void UpdateTimer()
    {
        float remainingSeconds = Mathf.Max(0f, (float)(deadline - Time.unscaledTimeAsDouble));
        gameUI.UpdateTimer(remainingSeconds);
        if (remainingSeconds <= 0f) EndGame();
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
        audioManager.PlayAssignmentHit(feverActive);
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
        audioManager.PlayMissClickWarning();
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
        SkillProgress.RecordBestGrade(CurrentGradeIndex);
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
        gameUI.UpdateFeverState(feverActive,
            feverActive ? Mathf.Max(0f, (float)(feverExpiresAt - now)) : 0f);
    }

    public void EndGame()
    {
        if (!isPlaying) return;
        isPlaying = false;
        feverWasActive = false;
        gameUI.SetFeverVisual(false);
        gameUI.UpdateFeverState(false, 0f);
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
