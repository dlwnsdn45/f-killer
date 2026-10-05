using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SkillTable", menuName = "F Killer/Skill Table")]
public sealed class SkillTable: ScriptableObject
{
    [SerializeField] List<SkillData> skills = new List<SkillData>();
    public IReadOnlyList<SkillData> Skills => skills;

    public SkillData Find(string id) => skills.Find(skill => skill != null && skill.Id == id);

    void OnValidate()
    {
        var ids = new HashSet<string>();
        foreach (SkillData skill in skills)
        {
            if (skill == null || string.IsNullOrWhiteSpace(skill.Id) || !ids.Add(skill.Id))
                Debug.LogError("스킬 ID가 비어 있거나 중복되었습니다.", this);
        }
        foreach (SkillData skill in skills)
        {
            if (skill == null) continue;
            var visited = new HashSet<string> { skill.Id };
            SkillData current = skill;
            while (!current.IsStart)
            {
                current = Find(current.PrerequisiteId);
                if (current == null || !visited.Add(current.Id))
                {
                    Debug.LogError($"스킬 {skill.Id}의 선행 조건이 누락되었거나 순환합니다.", this);
                    break;
                }
            }
        }
    }
}