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
    [Header("학점별 과제 체력·포인트 배율 (F, F+, D, D+, C, C+, B, B+, A, A+)")]
    [SerializeField] float[] hpScaleByGrade = { 1f, 1.1f, 1.2f, 1.35f, 1.5f, 1.7f, 1.9f, 2.2f, 2.5f, 2.8f };
    [SerializeField] float[] pointScaleByGrade = { 1f, 1.1f, 1.25f, 1.4f, 1.6f, 1.85f, 2.1f, 2.5f, 2.9f, 3.3f };

    static float ScaleAt(float[] scales, int gradeIndex)
        => scales == null || scales.Length == 0 ? 1f : scales[Mathf.Clamp(gradeIndex, 0, scales.Length - 1)];

    // intervalScale: 스킬로 줄어든 등장 간격 배율 (1 = 기본)
    public float GetInterval(int gradeIndex, float intervalScale)
    {
        float gradeInterval = gradeIndex <= 1 ? fPlusOrLowerInterval
            : gradeIndex <= 3 ? dToDPlusInterval
            : gradeIndex <= 5 ? cToCPlusInterval
            : bPlusAndAboveInterval;
        return Mathf.Max(0.05f, gradeInterval * Mathf.Clamp01(intervalScale));
    }

    public void Spawn(int gradeIndex, int extraCount, float goldChance, float diamondChance,
        Action<AssignmentBase> registerAssignment)
    {
        if (registerAssignment == null) return;

        // 학점에 따라 기본 생성 과제 수를 계산하고 동시 등장 스킬 보너스를 더한다
        int count = (gradeIndex <= 3 ? 1
            : gradeIndex <= 5 ? UnityEngine.Random.Range(1, 3)
            : UnityEngine.Random.Range(2, 4)) + extraCount;
        for (int i = 0; i < count; i++)
            SpawnOne(gradeIndex, goldChance, diamondChance, registerAssignment);
    }

    public void SpawnOne(int gradeIndex, float goldChance, float diamondChance, Action<AssignmentBase> registerAssignment)
    {
        // 현재 학점에서 출제 가능한 과목 중 하나를 선택
        AssignmentData data = GetSpawnObject(gradeIndex);
        if (data == null || registerAssignment == null) return;
        // 다이아를 먼저 굴리고, 아니면 황금을 굴린다
        float roll = UnityEngine.Random.value;
        AssignmentRarity rarity = roll < diamondChance ? AssignmentRarity.Diamond
            : roll < diamondChance + goldChance ? AssignmentRarity.Golden : AssignmentRarity.Normal;
        var assignment = new AssignmentBase();
        assignment.Initialize(data, rarity, ScaleAt(hpScaleByGrade, gradeIndex), ScaleAt(pointScaleByGrade, gradeIndex));
        registerAssignment(assignment);
    }

    // 졸업 작품 스킬(C20)이 마지막 5초에 내보내는 특수 카드
    public AssignmentBase CreateGradWork(int gradeIndex)
    {
        SpawnEntry entry = assignmentTable.Find(item => item != null && item.data != null
            && item.data.Kind == AssignmentKind.GraduationProject);
        if (entry == null) return null;
        var assignment = new AssignmentBase();
        assignment.Initialize(entry.data, AssignmentRarity.GradWork,
            ScaleAt(hpScaleByGrade, gradeIndex), ScaleAt(pointScaleByGrade, gradeIndex));
        return assignment;
    }

    public AssignmentBase CreateTutorialAssignment()
    {
        AssignmentData data = GetSpawnObject(0);
        if (data == null) return null;
        var assignment = new AssignmentBase();
        assignment.Initialize(data);
        return assignment;
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
