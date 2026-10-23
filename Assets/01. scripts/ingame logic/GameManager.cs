using System;
using System.Collections.Generic;
using UnityEngine;

// 스킬 id는 SkillTable(에프킬라 스킬 트리 엑셀 코드)과 같다. 개발 툴·로봇은 GameManager.Tools.cs.
[RequireComponent(typeof(AssignmentSpawner), typeof(AudioManager))]
public sealed partial class GameManager : MonoBehaviour
{
    const float RoundDurationSeconds = 15f;
    public static readonly string[] GradeNames = { "F", "F+", "D", "D+", "C", "C+", "B", "B+", "A", "A+", "졸업" };
    // 라운드 점수 기준 F~A+ 문턱과 졸업 점수. 밸런스 시뮬레이터로 링 잠금(C/B/A/A+)이 약 13·22·31·38분에 풀리게 맞춤.
    static readonly int[] GradeThresholds = { 0, 50, 100, 300, 1500, 5000, 7500, 50000, 150000, 700000 };
    const int GraduationPoints = 4000000;
    // Click: 직접 클릭과 그 확산, Skill: 클릭 피해 배수로 터지는 연쇄 효과
    enum HitSource { Click, Skill, Tool, Robot }
    [SerializeField] GameUIController gameUI;
    [SerializeField] AssignmentSpawner assignmentSpawner;
    [SerializeField] AudioManager audioManager;
    [Header("과부하")]
    [SerializeField, Min(0f)] float missClickPenaltySeconds = 0.5f;

    bool isPlaying;
    bool hasStarted;
    bool progressChangedSubscribed;
    float elapsedSpawnSeconds;
    float roundDuration;
    double deadline;
    double pausedUntil;
    const float MaxPausePerRound = 10f;
    float pauseGranted;
    bool extensionUsed;
    bool gradWorkSpawned;
    int totalPoints;
<<<<<<< Updated upstream
    int overloadBlocksRemaining;
=======
    int roundEarnedPoints;
    int lastRoundScore = -1;
    int clickCount;
    int quotaCount, quotaPoints;
    AssignmentBase focusTarget;
    int focusStacks;
    double focusLastClick;
    readonly Queue<double> recentClicks = new Queue<double>();
    double frenzyUntil, frenzyReadyAt;
    bool frenzyWasActive;
    double finalSubmitReadyAt;
    float batchTimer, graduationTimer;
>>>>>>> Stashed changes
    SkillProgress skillProgress;
    public SkillProgress SkillProgress => skillProgress ?? (skillProgress = new SkillProgress());
    readonly List<AssignmentBase> activeAssignments = new List<AssignmentBase>();

    bool HasSkill(string id) => SkillProgress.IsOwned(gameUI != null ? gameUI.SkillTable : null, id);
    float Remaining => Mathf.Max(0f, (float)(deadline - Time.unscaledTimeAsDouble));
    bool InLastFive => isPlaying && Remaining <= 5f;

    float RoundDuration => RoundDurationSeconds
        + (HasSkill("C01") ? 2f : 0f)
        + (HasSkill("C04") ? 3f : 0f)
        + (HasSkill("C08") ? 5f : 0f)
        + (HasSkill("C14") ? 5f : 0f)
        + (HasSkill("C18") ? 10f : 0f);

    int ClickDamage => 1 + (HasSkill("A01") ? 1 : 0) + (HasSkill("A02") ? 2 : 0) + (HasSkill("A04") ? 3 : 0)
        + (HasSkill("A08") ? 5 : 0) + (HasSkill("A11") ? 8 : 0);
    int CritChance => (HasSkill("A03") ? 5 : 0) + (HasSkill("A10") ? 10 : 0) + (HasSkill("A18") ? 15 : 0);
    float CritMultiplier => 2f + (HasSkill("A06") ? 0.5f : 0f) + (HasSkill("A14") ? 1f : 0f);
    bool RollCritical() => CritChance > 0 && UnityEngine.Random.Range(0, 100) < CritChance;

    // 합연산: 곱연산이면 후반 수입이 폭주한다 (전부 사면 ×14 → +350%)
    float PointMultiplier => 1f + (HasSkill("D01") ? 0.25f : 0f) + (HasSkill("D03") ? 0.25f : 0f)
        + (HasSkill("D04") ? 0.5f : 0f) + (HasSkill("D08") ? 0.5f : 0f)
        + (HasSkill("D11") ? 1f : 0f) + (HasSkill("D16") ? 1f : 0f)
        // 가산점(D06 2%, D13 3%, D18 5%): 현재 학점 단계마다
        + CurrentGradeIndex * ((HasSkill("D06") ? 0.02f : 0f) + (HasSkill("D13") ? 0.03f : 0f) + (HasSkill("D18") ? 0.05f : 0f));

    float SpawnIntervalScale => (1f - (HasSkill("C02") ? 0.10f : 0f) - (HasSkill("C06") ? 0.15f : 0f))
        * (HasSkill("C10") && InLastFive ? 0.5f : 1f);
    int ExtraSpawnCount => (HasSkill("C03") ? 1 : 0) + (HasSkill("C11") ? 2 : 0);
    float GoldChance => HasSkill("C05") ? 0.1f + (HasSkill("C15") ? 0.1f : 0f) : 0f;
    float DiamondChance => HasSkill("C16") ? 0.03f : 0f;

    int CurrentGradeIndex => GetGradeIndex(totalPoints);

    int GetGradeIndex(int points)
    {
        int gradeIndex = 0;
        for (int i = 1; i < GradeThresholds.Length; i++)
            if (points >= GradeThresholds[i]) gradeIndex = i;
        if (points >= GraduationPoints) gradeIndex = 10;
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
        // 테스트: F3 → 스킬트리 전부 해금
        if (UnityEngine.InputSystem.Keyboard.current?.f3Key.wasPressedThisFrame == true)
            SkillProgress.UnlockAllForTest(gameUI.SkillTable);
        if (!isPlaying) return;
        float deltaTime = Time.unscaledDeltaTime;
        double now = Time.unscaledTimeAsDouble;
        if (now < pausedUntil) deadline += deltaTime;  // 작품 제출: 마감 타이머 정지
        UpdateTimer();
        if (!isPlaying) return;
        UpdateFrenzy(now);

        if (assignmentSpawner != null)
        {
            elapsedSpawnSeconds += deltaTime;
            int spawnGradeIndex = CurrentGradeIndex;  // 이번 판 학점 기준 (최고 학점이면 다음 판 시작부터 강한 과제가 나옴)
            float interval = assignmentSpawner.GetInterval(spawnGradeIndex, SpawnIntervalScale);
            while (elapsedSpawnSeconds >= interval)
            {
                elapsedSpawnSeconds -= interval;
                assignmentSpawner.Spawn(spawnGradeIndex, ExtraSpawnCount, GoldChance, DiamondChance, RegisterAssignment);
            }
            if (HasSkill("C20") && !gradWorkSpawned && Remaining <= 5f)
            {
                gradWorkSpawned = true;
                RegisterAssignment(assignmentSpawner.CreateGradWork(CurrentGradeIndex));
            }
        }

        UpdateTimedSkills(deltaTime);
        UpdateTools(deltaTime);
        UpdateRobots(deltaTime);
        if (!isPlaying) return;
        foreach (AssignmentBase assignment in activeAssignments)
            if (assignment.Data.Kind == AssignmentKind.Bug)
                gameUI.MoveBug(assignment, deltaTime);
    }

    public void StartGame()
    {
        SubscribeProgressChanged();
        isPlaying = false;
        ClearAssignments();
        // 선이수(D12): 최고 학점보다 두 단계 낮은 학점의 기준 점수에서 시작 (지갑 P는 늘지 않음)
        totalPoints = HasSkill("D12") ? GradeThresholds[Mathf.Clamp(SkillProgress.BestGradeIndex - 2, 0, GradeThresholds.Length - 1)] : 0;
        roundEarnedPoints = 0;
        elapsedSpawnSeconds = 0f;
        clickCount = 0;
        quotaCount = quotaPoints = 0;
        focusTarget = null;
        focusStacks = 0;
        recentClicks.Clear();
        frenzyUntil = frenzyReadyAt = 0;
        frenzyWasActive = false;
        finalSubmitReadyAt = 0;
        batchTimer = graduationTimer = 0f;
        pausedUntil = 0;
        pauseGranted = 0f;
        extensionUsed = false;
        gradWorkSpawned = false;
        ResetToolState();
        gameUI.SetFeverVisual(false);
        gameUI.UpdateFeverState(false, 0f);
<<<<<<< Updated upstream
        overloadBlocksRemaining = (HasSkill("skill-20") ? 3 : 0) + (HasSkill("skill-23") ? 5 : 0);
        deadline = Time.unscaledTimeAsDouble + RoundDuration;
=======
        roundDuration = RoundDuration;
        deadline = Time.unscaledTimeAsDouble + roundDuration;
>>>>>>> Stashed changes
        isPlaying = true;
        hasStarted = true;
        gameUI.ShowGame();
        gameUI.UpdatePoints(SkillProgress.Points);
        UpdateAcademicGrade();
<<<<<<< Updated upstream
        gameUI.UpdateTimer(RoundDuration);
=======
        gameUI.UpdateTimer(roundDuration);
        if (!SkillProgress.TutorialCompleted)
        {
            isPlaying = false;
            gameUI.BeginTutorial(() =>
            {
                deadline = Time.unscaledTimeAsDouble + roundDuration;
                isPlaying = true;
                gameUI.UpdateTimer(roundDuration);
            });
        }
>>>>>>> Stashed changes
    }

    void UpdateTimer()
    {
        float remainingSeconds = Remaining;
        gameUI.UpdateTimer(remainingSeconds);
        if (remainingSeconds > 0f) return;
        // 연장 신청: 시간이 0이 되면 라운드당 1회 +10초
        if (HasSkill("C13") && !extensionUsed)
        {
            extensionUsed = true;
            deadline = Time.unscaledTimeAsDouble + 10.0;
            return;
        }
        EndGame();
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
        double now = Time.unscaledTimeAsDouble;
        clickCount++;
        RegisterFrenzyClick(now);
        bool frenzyActive = now < frenzyUntil;

        float damage = ClickDamage;
        if (HasSkill("A07"))
        {
            // 집중: 같은 카드를 1초 안에 다시 누르면 10%씩, 최대 +50%
            focusStacks = focusTarget == assignment && now - focusLastClick <= 1.0 ? Mathf.Min(5, focusStacks + 1) : 0;
            focusTarget = assignment;
            focusLastClick = now;
            damage *= 1f + 0.1f * focusStacks;
        }
<<<<<<< Updated upstream
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
        int immunity = SkillProgress.RebirthLevel(4);
        if (immunity > 0 && UnityEngine.Random.value < (immunity == 1 ? 0.3f : 0.5f))
        {
            gameUI.ShowOverloadFeedback(position, true, overloadBlocksRemaining);
            return;
        }
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
        assignment.ApplyClick(damage);
        if (assignment.IsCompleted) CompleteAssignment(assignment, manualHit);
        else gameUI.RefreshAssignment(assignment);
    }

    void UpdateRobots()
    {
        int robotCount = SkillProgress.RebirthLevel(3) + (HasSkill("skill-34") ? 1 : 0) + (HasSkill("skill-38") ? 2 : 0);
        if (robotCount == 0 || activeAssignments.Count == 0) return;
        float interval = Mathf.Max(0.25f, 3f - (HasSkill("skill-35") ? 0.5f : 0f));
        elapsedRobotSeconds += Time.unscaledDeltaTime;
        while (isPlaying && elapsedRobotSeconds >= interval && activeAssignments.Count > 0)
=======
        if (HasSkill("A09") && assignment.HpRatio <= 0.3f) damage *= 2f;
        // 벼락치기: 남은 시간이 줄수록 증가, 마지막 1초에 +200%
        if (HasSkill("C09")) damage *= 1f + 2f * Mathf.Clamp01((roundDuration - Remaining) / Mathf.Max(1f, roundDuration - 1f));
        if (frenzyActive) damage *= 2f;
        bool critical = (HasSkill("EA01") && now - lastRobotAttackAt <= 0.5) || RollCritical();
        if (critical) damage *= CritMultiplier;
        bool finalSubmit = HasSkill("A20") && now >= finalSubmitReadyAt;
        if (finalSubmit)
>>>>>>> Stashed changes
        {
            damage *= 10f;
            finalSubmitReadyAt = now + 10.0;
        }
        audioManager.PlayAssignmentHit(frenzyActive);
        if (frenzyActive)
        {
            gameUI.PlayFeverHitEffect(assignment);
            gameUI.PlayCardEffect(assignment, "fx-frenzy", 2, 48f, 48f);
        }

        // 확산 대상은 카드가 사라지기 전에 정한다.
        gameUI.TryGetCardCenter(assignment, out Vector2 clickedCenter);
        List<AssignmentBase> multitask = HasSkill("A05") ? Nearest(assignment, 2) : null;
        List<AssignmentBase> area = HasSkill("A15") ? Nearest(assignment, 3) : null;
        List<AssignmentBase> chain = critical && HasSkill("A13") ? Nearest(assignment, 3) : null;
        List<AssignmentBase> submitSplash = finalSubmit ? Nearest(assignment, 5) : null;
        // 올나이트: 남은 시간 20% 이하면 클릭 1회가 2회
        int hits = HasSkill("A17") && Remaining <= roundDuration * 0.2f ? 2 : 1;
        bool destroyed = false;
        for (int i = 0; i < hits && !destroyed; i++) destroyed = DamageAssignment(assignment, damage, HitSource.Click);
        if (finalSubmit) gameUI.PlayEffectAt(clickedCenter, "fx-final-submit", 5, 120f, 120f);
        Spread(multitask, damage * 0.3f, HitSource.Click);
        Spread(area, damage * 0.5f, HitSource.Click);
        if (chain != null)
        {
            var boltPoints = new List<Vector2> { clickedCenter };
            foreach (AssignmentBase target in chain)
            {
                if (gameUI.TryGetCardCenter(target, out Vector2 center)) boltPoints.Add(center);
                gameUI.PlayCardEffect(target, "fx-chain", 3, 48f, 48f);
            }
            gameUI.PlayChainLightning(boltPoints);
            Spread(chain, damage * 0.7f, HitSource.Skill);
        }
        if (finalSubmit && destroyed) Spread(submitSplash, ClickDamage * 3f, HitSource.Skill);
        // 더블 클릭: 커서 분신이 같은 카드를 한 번 더 클릭
        if (HasSkill("AB01") && !destroyed)
        {
            gameUI.PlayCardEffect(assignment, "obj-cursor", 2, 32f, 32f);
            DamageAssignment(assignment, CloneDamage, HitSource.Tool);
        }
        if (HasSkill("D05") && clickCount % 10 == 0) GainPoints((CurrentGradeIndex + 1) * 10);
        if (HasSkill("E05") && clickCount % 5 == 0) RobotVolley();
    }

    void RegisterFrenzyClick(double now)
    {
        if (!HasSkill("A19")) return;
        // 폭주 클릭: 3초 안에 15번 클릭하면 5초 동안 클릭 피해 ×2, 재발동 대기 15초
        recentClicks.Enqueue(now);
        while (recentClicks.Count > 0 && now - recentClicks.Peek() > 3.0) recentClicks.Dequeue();
        if (recentClicks.Count < 15 || now < frenzyReadyAt) return;
        frenzyUntil = now + 5.0;
        frenzyReadyAt = now + 15.0;
        recentClicks.Clear();
    }

    void UpdateFrenzy(double now)
    {
        bool active = now < frenzyUntil;
        if (active != frenzyWasActive)
        {
            frenzyWasActive = active;
            gameUI.SetFeverVisual(active);
        }
        gameUI.UpdateFeverState(active, active ? (float)(frenzyUntil - now) : 0f);
    }

    void UpdateTimedSkills(float deltaTime)
    {
        // 일괄 처리: 5초마다 모든 카드에 클릭 피해 ×3
        if (HasSkill("A16") && (batchTimer += deltaTime) >= 5f)
        {
            batchTimer -= 5f;
            foreach (AssignmentBase target in activeAssignments.ToArray())
            {
                gameUI.PlayCardEffect(target, "fx-click", 3, 96f, 96f);
                DamageAssignment(target, ClickDamage * 3f, HitSource.Skill);
            }
        }
        // 졸업 프로젝트: 20초마다 모든 일반 카드에 클릭 ×10, 툴 ×5, 로봇 ×5
        if (HasSkill("ABCDE01") && (graduationTimer += deltaTime) >= 20f)
        {
            graduationTimer -= 20f;
            gameUI.PlayGraduationBorder();
            foreach (AssignmentBase target in activeAssignments.ToArray())
            {
                if (!target.IsNormal) continue;
                if (!DamageAssignment(target, ClickDamage * 10f, HitSource.Skill)
                    && !DamageAssignment(target, ToolDamage * 5f, HitSource.Tool))
                    DamageAssignment(target, RobotDamage * 5f, HitSource.Robot);
            }
        }
    }

    // 주변 카드: 화면 위치가 가까운 순서로 count장
    List<AssignmentBase> Nearest(AssignmentBase from, int count)
    {
        var result = new List<AssignmentBase>();
        if (!gameUI.TryGetCardCenter(from, out Vector2 origin)) return result;
        var sorted = new List<(float distance, AssignmentBase card)>();
        foreach (AssignmentBase other in activeAssignments)
            if (other != from && gameUI.TryGetCardCenter(other, out Vector2 center))
                sorted.Add(((center - origin).sqrMagnitude, other));
        sorted.Sort((x, y) => x.distance.CompareTo(y.distance));
        for (int i = 0; i < Mathf.Min(count, sorted.Count); i++) result.Add(sorted[i].card);
        return result;
    }

    List<AssignmentBase> RandomCards(int count)
    {
        var pool = new List<AssignmentBase>(activeAssignments);
        var result = new List<AssignmentBase>();
        while (result.Count < count && pool.Count > 0)
        {
            int index = UnityEngine.Random.Range(0, pool.Count);
            result.Add(pool[index]);
            pool.RemoveAt(index);
        }
        return result;
    }

    void Spread(List<AssignmentBase> targets, float damage, HitSource source)
    {
        if (targets == null) return;
        foreach (AssignmentBase target in targets) DamageAssignment(target, damage, source);
    }

    // 카드를 파괴했으면 true
    bool DamageAssignment(AssignmentBase assignment, float damage, HitSource source)
    {
        if (!isPlaying || assignment == null || !activeAssignments.Contains(assignment)) return false;
        double now = Time.unscaledTimeAsDouble;
        // 하이라이트: 개발 툴에 맞은 카드는 3초 동안 받는 클릭 피해 +50%
        if ((source == HitSource.Click || source == HitSource.Skill) && now < assignment.HighlightedUntil) damage *= 1.5f;
        if (HasSkill("C10") && InLastFive) damage *= 2f;  // 데드라인: 마지막 5초 모든 피해 ×2
        assignment.ApplyClick(Mathf.Max(1, Mathf.RoundToInt(damage)));
        if (assignment.IsCompleted)
        {
            CompleteAssignment(assignment, source);
            return true;
        }
        if (source == HitSource.Tool && HasSkill("B12"))
        {
            assignment.HighlightedUntil = now + 3.0;
            gameUI.ShowHighlight(assignment, 3f);
        }
        gameUI.RefreshAssignment(assignment);
        return false;
    }

    void DestroyInstantly(AssignmentBase assignment, HitSource source)
    {
        if (!isPlaying || assignment == null || !activeAssignments.Contains(assignment)) return;
        assignment.ApplyClick(assignment.CurrentHp);
        CompleteAssignment(assignment, source);
    }

    void CompleteAssignment(AssignmentBase assignment, HitSource source)
    {
        if (!assignment.IsCompleted || !activeAssignments.Contains(assignment)) return;
        List<AssignmentBase> groupSplash = HasSkill("C07") && assignment.Data.Kind == AssignmentKind.TeamProject
            ? Nearest(assignment, 3) : null;
        activeAssignments.Remove(assignment);

        float multiplier = PointMultiplier * (HasSkill("D20") ? 1.5f : 1f);  // 전액 장학금: 최종 ×1.5
        if (HasSkill("D19")) multiplier *= 1f + 0.05f * SkillProgress.CompoundRounds;
        if (HasSkill("CD01") && InLastFive) multiplier *= 3f;
        if (HasSkill("DE01") && assignment.IsGolden) multiplier *= 3f;
        if (HasSkill("D09") && RollCritical()) multiplier *= 2f;
        if (HasSkill("D17") && UnityEngine.Random.value < 0.05f)
        {
            multiplier *= 10f;
            gameUI.PlayCardEffect(assignment, "fx-perfect", 3, 72f, 48f);
        }
        int reward = Mathf.Max(1, Mathf.RoundToInt(assignment.Points * multiplier));
        GainPoints(reward);
        if (HasSkill("D10") && ++quotaCount >= 10)
        {
            GainPoints((quotaPoints + reward) / 2);
            quotaCount = quotaPoints = 0;
        }
        else if (HasSkill("D10")) quotaPoints += reward;
        gameUI.RemoveAssignment(assignment);
<<<<<<< Updated upstream
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
=======

        if (assignment.Rarity == AssignmentRarity.GradWork) deadline += 5.0;
        // 작품 제출: 2초 정지. 쌓이면 라운드가 끝나지 않으므로 라운드당 합계 10초까지만.
        if (HasSkill("C12") && assignment.Data.Kind == AssignmentKind.SemesterProject && pauseGranted < MaxPausePerRound)
>>>>>>> Stashed changes
        {
            float pause = Mathf.Min(2f, MaxPausePerRound - pauseGranted);
            pauseGranted += pause;
            pausedUntil = Math.Max(pausedUntil, Time.unscaledTimeAsDouble) + pause;
            gameUI.PlayTimerStop();
        }
        if (source == HitSource.Click && HasSkill("A12")) Spread(RandomCards(3), ClickDamage * 2f, HitSource.Skill);
        Spread(groupSplash, ClickDamage * 3f, HitSource.Skill);
        if (HasSkill("C17") && assignment.Data.Kind == AssignmentKind.Contest)
            foreach (AssignmentBase other in activeAssignments.ToArray())
                if (other.IsNormal) DestroyInstantly(other, HitSource.Skill);
        if (HasSkill("C19") && UnityEngine.Random.value < 0.2f && assignmentSpawner != null)
            assignmentSpawner.SpawnOne(CurrentGradeIndex, GoldChance, DiamondChance, RegisterAssignment);
        // 자동 저장: 개발 툴이 카드를 파괴하면 30% 확률로 그 툴이 곧바로 한 번 더 발동
        // (커서 분신은 이미 0.5초마다 공격하므로 제외)
        if (source == HitSource.Tool && firingTool >= 0 && firingTool != CloneTool && HasSkill("B11") && UnityEngine.Random.value < 0.3f)
            toolCooldowns[firingTool] = 0f;
        if (source == HitSource.Robot) robotKilled = true;
    }

    void GainPoints(int amount)
    {
        if (amount <= 0) return;
        totalPoints += amount;
        roundEarnedPoints += amount;
        SkillProgress.RecordBestGrade(CurrentGradeIndex);
        SkillProgress.AddPoints(amount);
        UpdateAcademicGrade();
    }

    public void EndGame()
    {
        if (!isPlaying) return;
        isPlaying = false;
        frenzyWasActive = false;
        gameUI.SetFeverVisual(false);
        gameUI.UpdateFeverState(false, 0f);
        gameUI.UpdateTimer(0f);
        int finalGradeIndex = CurrentGradeIndex;
        string finalGrade = GradeNames[finalGradeIndex];
        // 라운드 종료 보너스는 학점 판정 뒤 지갑에만 더한다: 성적 향상(D07), 장학금(D15)
        float bonus = 0f;
        if (HasSkill("D07") && lastRoundScore >= 0 && totalPoints > lastRoundScore) bonus += roundEarnedPoints * 0.5f;
        if (HasSkill("D15")) bonus += roundEarnedPoints * 0.2f;
        SkillProgress.AddPoints(Mathf.RoundToInt(bonus));
        lastRoundScore = totalPoints;
        if (HasSkill("D19")) SkillProgress.AddCompoundRound();
        // 졸업 학점으로 판을 마치면 엔딩
        if (finalGradeIndex == GradeNames.Length - 1)
        {
            SceneLoader.Instance().LoadScene("EndingScene");
            return;
        }
        gameUI.ShowResult(totalPoints, finalGrade);
    }

    void ClearAssignments()
    {
        activeAssignments.Clear();
        gameUI.ClearAssignments();
    }
}
