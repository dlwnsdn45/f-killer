using System;
using System.Collections.Generic;
using UnityEngine;

public enum SkillPurchaseState { Available, Owned, Locked, InsufficientPoints, Invalid, GradeLocked }

// One persistent wallet shared across rounds. SkillTable remains read-only.
public sealed class SkillProgress
{
    const string SaveKey = "FKiller.SkillProgress.v1";
    [Serializable] sealed class SaveData
    {
        public int points;
        public int bestGradeIndex;
        public int rebirthCount;
        public int rebirthPoints;
        public int[] rebirthLevels = new int[5];
        public List<string> purchased = new List<string>();
        public int compoundRounds;
    }

    readonly HashSet<string> purchased = new HashSet<string>();
    public int Points { get; private set; }
    public int BestGradeIndex { get; private set; }
    public int RebirthCount { get; private set; }
    public int RebirthPoints { get; private set; }
<<<<<<< Updated upstream
    readonly int[] rebirthLevels = new int[5];
    public static readonly string[] RebirthNames = { "속성 강의", "학위의 무게", "교수의 자비", "기본 탑재 로봇", "면재권" };
=======
    // 복리 성장(D19): 이번 생에서 끝낸 라운드 수. 환생하면 초기화.
    public int CompoundRounds { get; private set; }
    readonly int[] rebirthLevels = new int[4];
    public static readonly string[] RebirthNames = { "속성 강의", "학위의 무게", "교수의 자비", "기본 탑재 로봇" };
>>>>>>> Stashed changes
    public static readonly string[] RebirthDescriptions = {
        "F~D+ 승급 필요량 영구 감소: Lv.1 -15%, Lv.2 -30%",
        "과제 파괴 보상 P 영구 증가: 레벨당 +20%",
        "기본 마감 시간 증가: Lv.1 +5초, Lv.2 +10초",
        "일반 스킬 초기화 후에도 레벨당 과제 로봇 1대 기본 작동",
        "공타 마감 차감 무시 확률: Lv.1 30%, Lv.2 50%" };
    public static readonly int[] RebirthMaxLevels = { 2, 5, 2, 3, 2 };
    public int RebirthLevel(int index) => rebirthLevels[index];
    public int RebirthCost(int index) => (index == 3 ? 3 : 2) + rebirthLevels[index] * (index == 4 ? 2 : 1);
    public long NextRebirthCost => 50000L * (RebirthCount + 1L);
    public bool CanRebirth => Points >= NextRebirthCost;
    public event Action Changed;

    // shortcut: 실행할 때마다 저장(포인트·스킬·환생)을 지운다. 이어하기가 필요해지면 이 메서드를 삭제.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ResetOnLaunch() => PlayerPrefs.DeleteKey(SaveKey);

    public SkillProgress()
    {
        try
        {
            var data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey, "{}"));
            if (data == null) return;
            Points = Math.Max(0, data.points);
            BestGradeIndex = Math.Max(0, data.bestGradeIndex);
            RebirthCount = Math.Max(0, data.rebirthCount);
            RebirthPoints = Math.Max(0, data.rebirthPoints);
            CompoundRounds = Math.Max(0, data.compoundRounds);
            if (data.rebirthLevels != null)
                for (int i = 0; i < Math.Min(5, data.rebirthLevels.Length); i++)
                    rebirthLevels[i] = Mathf.Clamp(data.rebirthLevels[i], 0, RebirthMaxLevels[i]);
            if (data.purchased != null)
                foreach (string id in data.purchased)
                    if (!string.IsNullOrWhiteSpace(id)) purchased.Add(id);
        }
        catch (ArgumentException) { Debug.LogWarning("스킬 저장 데이터를 읽지 못했습니다."); }
    }

    public bool IsOwned(SkillData skill) => skill != null && (skill.IsStart || purchased.Contains(skill.Id));

    public bool IsOwned(SkillTable table, string id) => table != null && IsOwned(table.Find(id));

    // 또는: 하나만 사도 열림, 그리고: 전부 사야 열림.
    public bool PrerequisitesMet(SkillTable table, SkillData skill)
    {
        if (skill.IsStart) return true;
        foreach (string id in skill.PrerequisiteIds)
            if (IsOwned(table, id) != skill.RequireAllPrerequisites) return !skill.RequireAllPrerequisites;
        return skill.RequireAllPrerequisites;
    }

    // 산 노드이거나 산 노드와 이어진 노드만 보인다. 나머지는 실루엣.
    public bool IsVisible(SkillTable table, SkillData skill)
    {
        if (IsOwned(skill)) return true;
        foreach (string id in skill.PrerequisiteIds)
            if (IsOwned(table, id)) return true;
        return false;
    }

    // 수강료 할인(D02 -10%, D14 -15%)
    public int CostOf(SkillTable table, SkillData skill)
        => (int)Math.Ceiling(skill.Cost * (1.0 - (IsOwned(table, "D02") ? 0.10 : 0) - (IsOwned(table, "D14") ? 0.15 : 0)));

    public SkillPurchaseState GetState(SkillTable table, string id)
    {
        if (table == null) return SkillPurchaseState.Invalid;
        SkillData skill = table.Find(id);
        if (skill == null || skill.Cost < 0 || string.IsNullOrWhiteSpace(skill.Id)) return SkillPurchaseState.Invalid;
        if (IsOwned(skill)) return SkillPurchaseState.Owned;
        if (!PrerequisitesMet(table, skill)) return SkillPurchaseState.Locked;
        if (BestGradeIndex < skill.RequiredGradeIndex) return SkillPurchaseState.GradeLocked;
        return Points < CostOf(table, skill) ? SkillPurchaseState.InsufficientPoints : SkillPurchaseState.Available;
    }

    public SkillPurchaseState TryPurchase(SkillTable table, string id)
    {
        SkillPurchaseState state = GetState(table, id);
        if (state != SkillPurchaseState.Available) return state;
        Points -= CostOf(table, table.Find(id));
        purchased.Add(id);
        Save();
        Changed?.Invoke();
        return SkillPurchaseState.Available;
    }

    public void AddPoints(int amount)
    {
        if (amount <= 0) return;
        Points = (int)Math.Min(int.MaxValue, (long)Points + amount);
        Save();
        Changed?.Invoke();
    }

    public void RecordBestGrade(int gradeIndex)
    {
        gradeIndex = Math.Max(0, gradeIndex);
        if (gradeIndex <= BestGradeIndex) return;
        BestGradeIndex = gradeIndex;
        Save();
    }

    public bool TryPurchaseRebirth(int index)
    {
        if (index < 0 || index >= 5 || rebirthLevels[index] >= RebirthMaxLevels[index]
            || RebirthPoints < RebirthCost(index)) return false;
        RebirthPoints -= RebirthCost(index);
        rebirthLevels[index]++;
        Save();
        Changed?.Invoke();
        return true;
    }

    // 환생 장학금(D06 +1, D13 +2, D18 +3), 전액 장학금(D20 RP ×1.5)
    public long NextRebirthReward(SkillTable table)
    {
        long reward = 2L * (RebirthCount + 1) + (IsOwned(table, "D06") ? 1 : 0)
            + (IsOwned(table, "D13") ? 2 : 0) + (IsOwned(table, "D18") ? 3 : 0);
        return IsOwned(table, "D20") ? (long)Math.Ceiling(reward * 1.5) : reward;
    }

    public bool TryRebirth(SkillTable table)
    {
        if (!CanRebirth) return false;
        long reward = NextRebirthReward(table);
        int carriedPoints = IsOwned(table, "D20") ? Points / 5 : 0;  // 전액 장학금: 보유 P 20% 이월
        bool prerequisiteCourse = IsOwned(table, "D12");
        RebirthCount++;
        RebirthPoints = (int)Math.Min(int.MaxValue, RebirthPoints + reward);
        Points = carriedPoints;
        BestGradeIndex = 0;
        CompoundRounds = 0;
        purchased.Clear();
        // 선이수: 모든 섹터의 링 1 노드를 산 상태로 시작
        if (prerequisiteCourse) foreach (string id in new[] { "A01", "B01", "C01", "D01", "E01" }) purchased.Add(id);
        Save();
        Changed?.Invoke();
        return true;
    }

<<<<<<< Updated upstream
=======
    // 테스트용(F3): 스킬트리 전체 구매.
    public void UnlockAllForTest(SkillTable table)
    {
        if (table == null) return;
        foreach (SkillData skill in table.Skills)
            if (skill != null && !string.IsNullOrWhiteSpace(skill.Id)) purchased.Add(skill.Id);
        Save();
        Changed?.Invoke();
    }

    public void AddCompoundRound()
    {
        if (CompoundRounds >= 10) return;
        CompoundRounds++;
        Save();
    }

>>>>>>> Stashed changes
    void Save()
    {
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(new SaveData
        {
            points = Points, bestGradeIndex = BestGradeIndex, purchased = new List<string>(purchased),
<<<<<<< Updated upstream
            rebirthCount = RebirthCount, rebirthPoints = RebirthPoints, rebirthLevels = rebirthLevels
=======
            tutorialCompleted = TutorialCompleted, tutorialRewardGranted = tutorialRewardGranted,
            rebirthCount = RebirthCount, rebirthPoints = RebirthPoints, rebirthLevels = rebirthLevels,
            compoundRounds = CompoundRounds
>>>>>>> Stashed changes
        }));
        PlayerPrefs.Save();
    }
}
