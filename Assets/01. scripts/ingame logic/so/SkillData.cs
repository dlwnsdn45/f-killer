using System;
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

    public string Id => id;
    public string Title => title;
    public string Description => description;
    public int Cost => cost;
    public string PrerequisiteId => prerequisiteId;
    public int MaxLevel => maxLevel;
    public bool IsStart => string.IsNullOrEmpty(prerequisiteId);
}

