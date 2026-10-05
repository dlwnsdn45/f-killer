using System;

// 공유 설정은 data에서 읽고, 클릭으로 변하는 상태만 개별 보관한다.
public class AssignmentBase
{
    AssignmentData data;
    int currentHp;
    bool isCompleted;
    bool isGolden;

    public AssignmentData Data => data;
    public int CurrentHp => currentHp;
    public int MaxHp => data == null ? 1 : data.MaxHp * (isGolden ? AssignmentData.GoldHpMultiplier : 1);
    public int Points => data == null ? 1 : data.Points * (isGolden ? AssignmentData.GoldPointMultiplier : 1);
    public bool IsGolden => isGolden;
    public bool IsCompleted => isCompleted;

    public void Initialize(AssignmentData data, bool isGolden = false)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        this.data = data;
        this.isGolden = isGolden;
        currentHp = MaxHp;
        isCompleted = false;
    }

    public void ApplyClick(int damage = 1)
    {
        if (data == null || isCompleted) return;
        currentHp -= Math.Max(1, damage);
        isCompleted = currentHp <= 0;
    }
}
