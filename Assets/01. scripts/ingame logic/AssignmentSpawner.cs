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

    public float GetInterval(int gradeIndex, Func<string, bool> hasSkill)
    {
        float reduction = hasSkill == null ? 0f
            : (hasSkill("skill-1") ? 0.03f : 0f)
                + (hasSkill("skill-2") ? 0.05f : 0f)
                + (hasSkill("skill-4") ? 0.08f : 0f)
                + (hasSkill("skill-5") ? 0.10f : 0f)
                + (hasSkill("skill-7") ? 0.15f : 0f)
                + (hasSkill("skill-8") ? 0.20f : 0f);
        float gradeInterval = gradeIndex <= 1 ? 1.5f
            : gradeIndex <= 3 ? 1.2f
            : gradeIndex <= 5 ? 1f
            : 0.5f;
        return Mathf.Max(0.05f, gradeInterval * (1f - Mathf.Clamp01(reduction)));
    }

    public void Spawn(int gradeIndex, Func<string, bool> hasSkill,
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
                switch (data.Kind)
                {
                    case AssignmentKind.Regular:
                        goldChance = hasSkill("skill-30") ? 0.10f : hasSkill("skill-29") ? 0.05f : 0f;
                        break;
                    case AssignmentKind.TeamProject:
                        goldChance = hasSkill("skill-31") ? 0.15f : 0f;
                        break;
                    case AssignmentKind.SemesterProject:
                        goldChance = hasSkill("skill-32") ? 0.20f : 0f;
                        break;
                    case AssignmentKind.GraduationProject:
                        goldChance = hasSkill("skill-33") ? 0.25f : 0f;
                        break;
                }
            }

            // 골드 여부를 정하고 과제를 생성해 게임에 등록
            bool isGolden = goldChance > 0f && UnityEngine.Random.value < goldChance;
            var assignment = new AssignmentBase();
            assignment.Initialize(data, isGolden);
            registerAssignment(assignment);
        }
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
