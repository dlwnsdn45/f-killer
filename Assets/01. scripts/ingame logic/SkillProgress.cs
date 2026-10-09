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
        public List<string> purchased = new List<string>();
    }

    readonly HashSet<string> purchased = new HashSet<string>();
    public int Points { get; private set; }
    public int BestGradeIndex { get; private set; }
    public event Action Changed;

    public SkillProgress()
    {
        try
        {
            var data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey, "{}"));
            if (data == null) return;
            Points = Math.Max(0, data.points);
            BestGradeIndex = Math.Max(0, data.bestGradeIndex);
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

    public void Reset()
    {
        Points = 0;
        BestGradeIndex = 0;
        purchased.Clear();
        Save();
        Changed?.Invoke();
    }

    void Save()
    {
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(new SaveData
        {
            points = Points, bestGradeIndex = BestGradeIndex, purchased = new List<string>(purchased)
        }));
        PlayerPrefs.Save();
    }
}
