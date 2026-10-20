using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AssignmentSpawner : MonoBehaviour
{
    [Serializable]
    public sealed class SpawnEntry
    {
        public AssignmentData data;
        [Min(0f)] public float weight = 1f;

        public bool IsValid => data != null && weight > 0f && !float.IsInfinity(weight);
    }

    [SerializeField] List<SpawnEntry> assignmentTable = new List<SpawnEntry>();
    [Header("학점별 과제 생성 간격")]
    [SerializeField, Min(0.05f)] float fPlusOrLowerInterval = 1.5f;
    [SerializeField, Min(0.05f)] float dToDPlusInterval = 1.2f;
    [SerializeField, Min(0.05f)] float cToCPlusInterval = 1f;
    [SerializeField, Min(0.05f)] float bPlusAndAboveInterval = 0.5f;

    public float GetInterval(int gradeIndex, Func<string, bool> hasSkill, SkillTable skillTable)
    {
        float reduction = hasSkill == null ? 0f
            : GetOwnedSkillRate(skillTable, hasSkill, "skill-1")
                + GetOwnedSkillRate(skillTable, hasSkill, "skill-2")
                + GetOwnedSkillRate(skillTable, hasSkill, "skill-4")
                + GetOwnedSkillRate(skillTable, hasSkill, "skill-5")
                + GetOwnedSkillRate(skillTable, hasSkill, "skill-7")
                + GetOwnedSkillRate(skillTable, hasSkill, "skill-8");
        float gradeInterval = gradeIndex <= 1 ? fPlusOrLowerInterval
            : gradeIndex <= 3 ? dToDPlusInterval
            : gradeIndex <= 5 ? cToCPlusInterval
            : bPlusAndAboveInterval;
        return Mathf.Max(0.05f, gradeInterval * (1f - Mathf.Clamp01(reduction)));
    }

    public void Spawn(int gradeIndex, Func<string, bool> hasSkill, SkillTable skillTable,
        Action<AssignmentBase> registerAssignment)
    {
        if (registerAssignment == null) return;

        // 학점에 따라 기본 생성 과제 수를 계산
        int count = gradeIndex <= 3 ? 1
            : gradeIndex <= 5 ? UnityEngine.Random.Range(1, 3)
            : UnityEngine.Random.Range(2, 4);
        // 추가 과제 스킬 보너스를 생성 수에 반영
        if (hasSkill != null)
            count += (hasSkill("skill-3") ? 1 : 0) + (hasSkill("skill-6") ? 2 : 0);

        for (int i = 0; i < count; i++)
        {
            // 현재 학점에서 출제 가능한 과목 중 하나를 선택
            AssignmentData data = GetSpawnObject(gradeIndex);
            if (data == null) return;

            // 과목 종류와 보유 스킬에 따라 골드 확률을 계산
            float goldChance = 0f;
            if (hasSkill != null)
            {
                string goldSkillId = null;
                switch (data.Kind)
                {
                    case AssignmentKind.Regular:
                        goldSkillId = hasSkill("skill-30") ? "skill-30"
                            : hasSkill("skill-29") ? "skill-29" : null;
                        break;
                    case AssignmentKind.TeamProject:
                        goldSkillId = "skill-31";
                        break;
                    case AssignmentKind.SemesterProject:
                        goldSkillId = "skill-32";
                        break;
                    case AssignmentKind.GraduationProject:
                        goldSkillId = "skill-33";
                        break;
                }
                goldChance = GetOwnedSkillRate(skillTable, hasSkill, goldSkillId);
            }

            // 골드 여부를 정하고 과제를 생성해 게임에 등록
            bool isGolden = goldChance > 0f && UnityEngine.Random.value < goldChance;
            var assignment = new AssignmentBase();
            assignment.Initialize(data, isGolden);
            registerAssignment(assignment);
        }
    }

    public AssignmentBase CreateTutorialAssignment()
    {
        AssignmentData data = GetSpawnObject(0);
        if (data == null) return null;
        var assignment = new AssignmentBase();
        assignment.Initialize(data);
        return assignment;
    }

    static float GetOwnedSkillRate(SkillTable skillTable, Func<string, bool> hasSkill, string skillId)
    {
        if (skillTable == null || hasSkill == null || string.IsNullOrEmpty(skillId) || !hasSkill(skillId)) return 0f;
        SkillData skill = skillTable.Find(skillId);
        return skill == null ? 0f : skill.EffectPercent / 100f;
    }

    AssignmentData GetSpawnObject(int gradeIndex)
    {
        float totalWeight = 0f;
        AssignmentData selected = null;
        foreach (SpawnEntry entry in assignmentTable)
        {
            if (entry == null || !entry.IsValid) continue;

            if (gradeIndex < entry.data.RequiredGradeIndex) continue;

            totalWeight += entry.weight;
            if (float.IsInfinity(totalWeight)) return null;
            if (selected == null || UnityEngine.Random.value * totalWeight < entry.weight)
                selected = entry.data;
        }

        if (totalWeight <= 0f) return null;
        return selected;
    }
}
