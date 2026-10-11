using System;
using System.Globalization;
using UnityEngine;

[Serializable]
public sealed class SkillData
{
    [SerializeField] string id;
    [SerializeField] string title;
    [SerializeField, TextArea(2, 4)] string description;
    [SerializeField, Min(0)] int cost;
    [SerializeField] string prerequisiteId;
    [SerializeField, Min(1)] int maxLevel = 1;
    [SerializeField, Range(0f, 100f), Tooltip("스킬 효과에 사용되는 백분율 값입니다.")] float effectPercent;

    public string Id => id;
    public string Title => title;
    public string Description => (description ?? string.Empty)
        .Replace("{percent}", EffectPercent.ToString("0.##", CultureInfo.InvariantCulture));
    public int Cost => cost;
    public string PrerequisiteId => prerequisiteId;
    public int MaxLevel => maxLevel;
    public float EffectPercent => Mathf.Clamp(effectPercent, 0f, 100f);
    public bool IsStart => string.IsNullOrEmpty(prerequisiteId);
}
