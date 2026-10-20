using System;
using System.Collections.Generic;
using UnityEngine;

public enum SkillPurchaseState { Available, Owned, Locked, InsufficientPoints, Invalid }

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
        public int[] rebirthLevels = new int[4];
        public List<string> purchased = new List<string>();
    }

    readonly HashSet<string> purchased = new HashSet<string>();
    public int Points { get; private set; }
    public int BestGradeIndex { get; private set; }
    public int RebirthCount { get; private set; }
    public int RebirthPoints { get; private set; }
    readonly int[] rebirthLevels = new int[4];
    public static readonly string[] RebirthNames = { "속성 강의", "학위의 무게", "교수의 자비", "기본 탑재 로봇" };
    public static readonly string[] RebirthDescriptions = {
        "F~D+ 승급 필요량 영구 감소: Lv.1 -15%, Lv.2 -30%",
        "과제 파괴 보상 P 영구 증가: 레벨당 +20%",
        "기본 마감 시간 증가: Lv.1 +5초, Lv.2 +10초",
        "일반 스킬 초기화 후에도 레벨당 과제 로봇 1대 기본 작동" };
    public static readonly int[] RebirthMaxLevels = { 2, 5, 2, 3 };
    public int RebirthLevel(int index) => rebirthLevels[index];
    public int RebirthCost(int index) => (index == 3 ? 3 : 2) + rebirthLevels[index];
    public long NextRebirthCost => 10000L * (RebirthCount + 1L);
    public bool CanRebirth => Points >= NextRebirthCost;
    public event Action Changed;

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
            if (data.rebirthLevels != null)
                for (int i = 0; i < Math.Min(rebirthLevels.Length, data.rebirthLevels.Length); i++)
                    rebirthLevels[i] = Mathf.Clamp(data.rebirthLevels[i], 0, RebirthMaxLevels[i]);
            if (data.purchased != null)
                foreach (string id in data.purchased)
                    if (!string.IsNullOrWhiteSpace(id)) purchased.Add(id);
        }
        catch (ArgumentException) { Debug.LogWarning("스킬 저장 데이터를 읽지 못했습니다."); }
    }

    public bool IsOwned(SkillData skill) => skill != null && (skill.IsStart || purchased.Contains(skill.Id));

    public bool IsOwned(SkillTable table, string id) => table != null && IsOwned(table.Find(id));

    public SkillPurchaseState GetState(SkillTable table, string id)
    {
        if (table == null) return SkillPurchaseState.Invalid;
        SkillData skill = table.Find(id);
        if (skill == null || skill.Cost < 0 || string.IsNullOrWhiteSpace(skill.Id)) return SkillPurchaseState.Invalid;
        if (IsOwned(skill)) return SkillPurchaseState.Owned;
        if (!IsOwned(table.Find(skill.PrerequisiteId))) return SkillPurchaseState.Locked;
        return Points < skill.Cost ? SkillPurchaseState.InsufficientPoints : SkillPurchaseState.Available;
    }

    public SkillPurchaseState TryPurchase(SkillTable table, string id)
    {
        SkillPurchaseState state = GetState(table, id);
        if (state != SkillPurchaseState.Available) return state;
        Points -= table.Find(id).Cost;
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
        if (index < 0 || index >= rebirthLevels.Length || rebirthLevels[index] >= RebirthMaxLevels[index]
            || RebirthPoints < RebirthCost(index)) return false;
        RebirthPoints -= RebirthCost(index);
        rebirthLevels[index]++;
        Save();
        Changed?.Invoke();
        return true;
    }

    public bool TryRebirth()
    {
        if (!CanRebirth) return false;
        RebirthCount++;
        RebirthPoints = (int)Math.Min(int.MaxValue, RebirthPoints + 2L * RebirthCount);
        Points = 0;
        BestGradeIndex = 0;
        purchased.Clear();
        Save();
        Changed?.Invoke();
        return true;
    }

    void Save()
    {
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(new SaveData
        {
            points = Points, bestGradeIndex = BestGradeIndex, purchased = new List<string>(purchased),
            rebirthCount = RebirthCount, rebirthPoints = RebirthPoints, rebirthLevels = rebirthLevels
        }));
        PlayerPrefs.Save();
    }
}
