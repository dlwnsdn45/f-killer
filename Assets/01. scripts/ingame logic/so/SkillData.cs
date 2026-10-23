using System;
using System.Collections.Generic;
using UnityEngine;

public enum SkillNodeType { Start, Stat, Core, Cross, Final, Graduation }

[Serializable]
public sealed class SkillData
{
    [SerializeField] string id;
    [SerializeField] string title;
    [SerializeField, TextArea(2, 4)] string description;
    [SerializeField, Min(0)] int cost;
    [SerializeField] List<string> prerequisiteIds = new List<string>();
    [SerializeField, Tooltip("켜면 선행 스킬을 전부 사야 열리고(그리고), 끄면 하나만 사도 열린다(또는).")] bool requireAllPrerequisites;
    [SerializeField] SkillNodeType type;
    [SerializeField, Tooltip("0~4 = A~E 섹터, -1 = 공용")] int sector = -1;
    [SerializeField, Tooltip("가운데에서 몇 번째 링인지. 링 4부터 최고 학점 조건이 붙는다.")] int ring;
    [SerializeField, Tooltip("스킬 트리 보드 위 중심 좌표")] Vector2 position;
    [SerializeField] Texture2D icon;

    public string Id => id;
    public string Title => title;
    public string Description => description ?? string.Empty;
    public int Cost => cost;
    public IReadOnlyList<string> PrerequisiteIds => prerequisiteIds;
    public bool RequireAllPrerequisites => requireAllPrerequisites;
    public SkillNodeType Type => type;
    public int Sector => sector;
    public int Ring => ring;
    public Vector2 Position => position;
    public Texture2D Icon => icon;
    public bool IsStart => prerequisiteIds == null || prerequisiteIds.Count == 0;
    // 링 4 = C, 링 5 = B, 링 6 = A, 링 7 이상 = A+ (GameManager.GradeNames 인덱스)
    public int RequiredGradeIndex => ring >= 7 ? 9 : ring >= 6 ? 8 : ring >= 5 ? 6 : ring >= 4 ? 4 : 0;
}
