using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SkillTable", menuName = "F Killer/Skill Table")]
public sealed class SkillTable: ScriptableObject
{
    [SerializeField] List<SkillData> skills = new List<SkillData>();
    Dictionary<string, SkillData> lookup;

    // 효과 판정이 매 프레임 여러 번 호출하므로 id 조회는 사전으로 한다.
    public SkillData Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (lookup == null)
        {
            lookup = new Dictionary<string, SkillData>();
            foreach (SkillData skill in skills)
                if (skill != null && !string.IsNullOrEmpty(skill.Id)) lookup[skill.Id] = skill;
        }
        return lookup.TryGetValue(id, out SkillData found) ? found : null;
    }
    public IReadOnlyList<SkillData> Skills => skills;

    void OnValidate()
    {
        lookup = null;
        var ids = new HashSet<string>();
        foreach (SkillData skill in skills)
        {
            if (skill == null || string.IsNullOrWhiteSpace(skill.Id) || !ids.Add(skill.Id))
                Debug.LogError("스킬 ID가 비어 있거나 중복되었습니다.", this);
        }
        foreach (SkillData skill in skills)
        {
            if (skill == null) continue;
            foreach (string prerequisite in skill.PrerequisiteIds)
                if (Find(prerequisite) == null) Debug.LogError($"스킬 {skill.Id}의 선행 스킬 {prerequisite}이 없습니다.", this);
            if (ReachesSelf(skill, skill, new HashSet<string>())) Debug.LogError($"스킬 {skill.Id}의 선행 조건이 순환합니다.", this);
        }
    }

    bool ReachesSelf(SkillData origin, SkillData current, HashSet<string> visited)
    {
        foreach (string id in current.PrerequisiteIds)
        {
            SkillData parent = Find(id);
            if (parent == null || !visited.Add(id)) continue;
            if (parent == origin || ReachesSelf(origin, parent, visited)) return true;
        }
        return false;
    }
}
