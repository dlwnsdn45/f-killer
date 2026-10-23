using System.Collections.Generic;
using UnityEngine;

// B 섹터 개발 툴과 E 섹터 로봇.
public sealed partial class GameManager
{
    // 0 픽셀 브러시, 1 커서 분신, 2 Delete 키, 3 Ctrl+Z
    static readonly string[] ToolSkillIds = { "B01", "B03", "B05", "B13" };
    static readonly float[] ToolIntervals = { 3f, 0.5f, 6f, 8f };
    // 속도 스킬을 다 겹쳐도 이보다 자주 발동하지 않는다 (실제 초)
    static readonly float[] ToolMinIntervals = { 1.5f, 0.35f, 3f, 4f };
    const int CloneTool = 1;
    const float BrushSweepSeconds = 0.6f;
    // 지나가는 중인 브러시. 붓끝이 지나간 구간에 닿은 카드를 매 프레임 판정한다 (카드당 한 번).
    readonly List<(Vector2 from, Vector2 to, double start, float damage, HashSet<AssignmentBase> hit)> brushSweeps
        = new List<(Vector2, Vector2, double, float, HashSet<AssignmentBase>)>();
    // 남은 대기시간(기본 간격 기준 초). ToolIntervalScale로 나눠 흐르게 한다.
    readonly float[] toolCooldowns = new float[4];
    int firingTool = -1;
    int cloneAttackCount;
    float buildTimer, infiniteTimer, robotTimer;
    double lastRobotAttackAt;
    bool robotKilled;

    // 무한 자동화: 15초마다 5초 동안
    bool InfiniteAutomationActive => HasSkill("B20") && infiniteTimer % 15f >= 10f;
    float ToolIntervalScale => (1f - (HasSkill("B02") ? 0.15f : 0f) - (HasSkill("B09") ? 0.15f : 0f)
            - (HasSkill("B15") ? 0.2f : 0f))
        / (HasSkill("B19") ? 1.25f : 1f)
        * (InfiniteAutomationActive ? 0.5f : 1f)
        * (HasSkill("BC01") && InLastFive ? 0.5f : 1f);
    float ToolScale(int tool) => Mathf.Max(ToolIntervalScale, ToolMinIntervals[tool] / ToolIntervals[tool]);
    float ToolDamage => ClickDamage * (HasSkill("B04") ? 1.5f : 1f) * (HasSkill("B08") ? 1.5f : 1f)
        * (InfiniteAutomationActive ? 2f : 1f);
    float CloneDamage => ToolDamage * (HasSkill("B10") ? 2f : 1f);
    int CloneCount => HasSkill("B03") ? 1 + (HasSkill("B06") ? 1 : 0) + (HasSkill("B14") ? 2 : 0) : 0;

    int RobotCount => (HasSkill("E01")
        ? 1 + (HasSkill("E04") ? 1 : 0) + (HasSkill("E11") ? 1 : 0) + (HasSkill("E15") ? 2 : 0) + (HasSkill("E20") ? 3 : 0)
        : 0);
    float RobotDamage => 1f + (HasSkill("E03") ? 0.5f : 0f) + (HasSkill("E06") ? 1f : 0f) + (HasSkill("E14") ? 1.5f : 0f)
        + (HasSkill("E16") ? 2.5f : 0f);
    float RobotInterval
    {
        get
        {
            float interval = 5f * Mathf.Max(0.15f, 1f - (HasSkill("E02") ? 0.2f : 0f) - (HasSkill("E08") ? 0.2f : 0f)
                - (HasSkill("E10") ? 0.2f : 0f) - (HasSkill("E18") ? 0.25f : 0f));
            if (HasSkill("E20")) interval /= 2f;
            // 과열: 남은 시간이 줄수록 빨라져 마지막 5초에 ×2
            if (HasSkill("E13")) interval /= 1f + Mathf.Clamp01((roundDuration - Remaining) / Mathf.Max(1f, roundDuration - 5f));
            return Mathf.Max(0.5f, interval);
        }
    }

    void ResetToolState()
    {
        for (int i = 0; i < toolCooldowns.Length; i++) toolCooldowns[i] = ToolIntervals[i];
        firingTool = -1;
        cloneAttackCount = 0;
        brushSweeps.Clear();
        buildTimer = infiniteTimer = robotTimer = 0f;
        lastRobotAttackAt = double.NegativeInfinity;
    }

    // 임시: 개발 툴(B 섹터) 효과 끄기. 다시 켤 때 true로.
    const bool ToolsEnabled = true;

    void UpdateTools(float deltaTime)
    {
        if (!ToolsEnabled) return;
        if (HasSkill("B20")) infiniteTimer += deltaTime;
        double now = Time.unscaledTimeAsDouble;
        for (int i = brushSweeps.Count - 1; i >= 0 && isPlaying; i--)
        {
            var sweep = brushSweeps[i];
            float t = Mathf.Clamp01((float)((now - sweep.start) / BrushSweepSeconds));
            Vector2 head = Vector2.Lerp(sweep.from, sweep.to, t);
            firingTool = 0;
            foreach (AssignmentBase target in activeAssignments.ToArray())
            {
                if (!isPlaying || !activeAssignments.Contains(target) || sweep.hit.Contains(target)
                    || !gameUI.CardTouchesSegment(target, sweep.from, head)) continue;
                sweep.hit.Add(target);
                DamageAssignment(target, sweep.damage, HitSource.Tool);
            }
            firingTool = -1;
            if (t >= 1f && i < brushSweeps.Count) brushSweeps.RemoveAt(i);
        }
        for (int i = 0; i < toolCooldowns.Length && isPlaying; i++)
        {
            if (!HasSkill(ToolSkillIds[i])) continue;
            toolCooldowns[i] -= deltaTime / ToolScale(i);
            if (toolCooldowns[i] > 0f) continue;
            toolCooldowns[i] = Mathf.Max(0f, toolCooldowns[i] + ToolIntervals[i]);
            FireTool(i);
        }
        // 빌드 성공: 8초마다 모든 개발 툴 동시 발동
        if (HasSkill("B17") && (buildTimer += deltaTime) >= 8f)
        {
            buildTimer -= 8f;
            gameUI.ShowBuildBanner();
            for (int i = 0; i < toolCooldowns.Length; i++)
                if (HasSkill(ToolSkillIds[i])) FireTool(i);
        }
    }

    void FireTool(int tool)
    {
        if (!isPlaying) return;
        firingTool = tool;
        if (tool == 0) FireBrush();
        else if (tool == 1) FireClones();
        else if (tool == 2) FireDeleteKey();
        else FireUndo();
        firingTool = -1;
        // 툴 연계: 다른 개발 툴 하나의 남은 대기시간 -1초.
        // 커서 분신은 초당 몇 번씩 공격해서 연계를 걸면 나머지 툴이 쉬지 않고 돌기 때문에 주고받기 모두 제외한다.
        if (!HasSkill("B16") || tool == CloneTool) return;
        var others = new List<int>();
        for (int i = 0; i < ToolSkillIds.Length; i++)
            if (i != tool && i != CloneTool && HasSkill(ToolSkillIds[i])) others.Add(i);
        if (others.Count == 0) return;
        int target = others[Random.Range(0, others.Count)];
        toolCooldowns[target] -= 1f / ToolScale(target);
    }

    void FireBrush()
    {
        Rect field = gameUI.FieldRect;
        // 레이어 3장: 가로·세로·대각선
        int lines = HasSkill("B07") ? 3 : 1;
        for (int line = 0; line < lines; line++)
        {
            Vector2 from, to;
            if (line == 0)
            {
                float y = Random.Range(field.yMin, field.yMax);
                from = new Vector2(field.xMin, y);
                to = new Vector2(field.xMax, y);
            }
            else if (line == 1)
            {
                float x = Random.Range(field.xMin, field.xMax);
                from = new Vector2(x, field.yMin);
                to = new Vector2(x, field.yMax);
            }
            else
            {
                bool down = Random.value < 0.5f;
                from = down ? field.min : new Vector2(field.xMin, field.yMax);
                to = down ? field.max : new Vector2(field.xMax, field.yMin);
            }
            gameUI.PlayBrush(from, to, BrushSweepSeconds);
            brushSweeps.Add((from, to, Time.unscaledTimeAsDouble, ToolDamage * 3f, new HashSet<AssignmentBase>()));
        }
    }

    void FireClones()
    {
        for (int i = 0; i < CloneCount && activeAssignments.Count > 0; i++)
        {
            AssignmentBase target = activeAssignments[Random.Range(0, activeAssignments.Count)];
            gameUI.PlayCardEffect(target, "obj-cursor", 2, 32f, 32f);
            // 분신 폭주: 10번째 공격마다 주변 카드 3장에도 적용
            List<AssignmentBase> splash = HasSkill("B18") && ++cloneAttackCount % 10 == 0 ? Nearest(target, 3) : null;
            DamageAssignment(target, CloneDamage, HitSource.Tool);
            Spread(splash, CloneDamage, HitSource.Tool);
        }
    }

    void FireDeleteKey()
    {
        List<AssignmentBase> normals = activeAssignments.FindAll(item => item.IsNormal);
        if (normals.Count == 0) return;
        AssignmentBase target = normals[Random.Range(0, normals.Count)];
        gameUI.PlayCardEffect(target, "obj-delete", 3, 60f, 60f);
        DestroyInstantly(target, HitSource.Tool);
    }

    void FireUndo()
    {
        gameUI.PlayUndoWave();
        foreach (AssignmentBase target in activeAssignments.ToArray())
            if (target.IsNormal && target.HpRatio <= 0.3f) DestroyInstantly(target, HitSource.Tool);
    }

    // 임시: 로봇(E 섹터) 공격 끄기. 다시 켤 때 true로.
    const bool RobotsEnabled = true;

    void UpdateRobots(float deltaTime)
    {
        if (!RobotsEnabled || RobotCount == 0 || activeAssignments.Count == 0) return;
        float interval = RobotInterval;
        robotTimer += deltaTime;
        while (isPlaying && robotTimer >= interval && activeAssignments.Count > 0)
        {
            robotTimer -= interval;
            RobotVolley();
        }
    }

    void RobotVolley()
    {
        int robots = RobotCount;
        if (!isPlaying || robots == 0 || activeAssignments.Count == 0) return;
        lastRobotAttackAt = Time.unscaledTimeAsDouble;
        var firstTargets = new List<AssignmentBase>();
        for (int i = 0; i < robots; i++)
        {
            AssignmentBase target = PickRobotTarget(null);
            if (target != null) firstTargets.Add(target);
        }
        // 집중 포화: 모든 로봇이 같은 카드를 노리면 로봇 피해 +50%
        bool focusFire = HasSkill("E19") && firstTargets.Count > 1 && firstTargets.TrueForAll(item => item == firstTargets[0]);
        foreach (AssignmentBase first in firstTargets)
        {
            if (!isPlaying) return;
            AssignmentBase second = HasSkill("E12") ? PickRobotTarget(first) : null;  // 멀티 타겟
            // 레이저 연결: 두 대상 사이에 있는 카드에도 로봇 피해
            if (second != null && HasSkill("E17") && gameUI.TryGetCardCenter(first, out Vector2 a)
                && gameUI.TryGetCardCenter(second, out Vector2 b))
            {
                gameUI.PlayLaser(a, b);
                foreach (AssignmentBase other in activeAssignments.ToArray())
                    if (other != first && other != second && gameUI.IsCardNearSegment(other, a, b, 30f))
                        DamageAssignment(other, RobotDamage, HitSource.Robot);
            }
            RobotHit(first, focusFire);
            if (second != null) RobotHit(second, false);
        }
    }

    void RobotHit(AssignmentBase target, bool focused)
    {
        if (!activeAssignments.Contains(target)) target = PickRobotTarget(null);
        if (target == null) return;
        float damage = RobotDamage * (focused ? 1.5f : 1f);
        if (HasSkill("E07") && RollCritical()) damage *= CritMultiplier;
        gameUI.PlayRobotAttackEffect(target);
        robotKilled = false;
        DamageAssignment(target, damage, HitSource.Robot);
        // AI 조교 군단: 로봇이 카드를 파괴하면 즉시 다른 카드 공격
        if (!robotKilled || !HasSkill("E20")) return;
        AssignmentBase next = PickRobotTarget(null);
        if (next == null) return;
        gameUI.PlayRobotAttackEffect(next);
        DamageAssignment(next, RobotDamage, HitSource.Robot);
    }

    AssignmentBase PickRobotTarget(AssignmentBase exclude)
    {
        var candidates = activeAssignments.FindAll(item => item != exclude);
        if (candidates.Count == 0) return null;
        // 황금 장학금: 황금 과제 우선
        if (HasSkill("DE01"))
        {
            List<AssignmentBase> golden = candidates.FindAll(item => item.IsGolden);
            if (golden.Count > 0) candidates = golden;
        }
        // 마무리 담당: 체력이 가장 낮은 카드 우선
        if (HasSkill("E09"))
        {
            AssignmentBase lowest = candidates[0];
            foreach (AssignmentBase item in candidates)
                if (item.CurrentHp < lowest.CurrentHp) lowest = item;
            return lowest;
        }
        return candidates[Random.Range(0, candidates.Count)];
    }
}
