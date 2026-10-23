using System;

public enum AssignmentRarity { Normal, Golden, Diamond, GradWork }

// 공유 설정은 data에서 읽고, 클릭으로 변하는 상태만 개별 보관한다.
public class AssignmentBase
{
    AssignmentData data;
    int currentHp;
    bool isCompleted;
    AssignmentRarity rarity;
    // 등장 시점 학점에 따른 보정 배율
    float hpScale = 1f, pointScale = 1f;

    public AssignmentData Data => data;
    public int CurrentHp => currentHp;
    public AssignmentRarity Rarity => rarity;
    public int MaxHp => data == null ? 1 : Math.Max(1, (int)Math.Round(hpScale * data.MaxHp
        * (rarity == AssignmentRarity.Golden ? AssignmentData.GoldHpMultiplier
        : rarity == AssignmentRarity.Diamond ? AssignmentData.DiamondHpMultiplier : 1)));
    public int Points => data == null ? 1 : Math.Max(1, (int)Math.Round(pointScale * data.Points
        * (rarity == AssignmentRarity.Golden ? AssignmentData.GoldPointMultiplier
        : rarity == AssignmentRarity.Diamond ? AssignmentData.DiamondPointMultiplier
        : rarity == AssignmentRarity.GradWork ? AssignmentData.GradWorkPointMultiplier : 1)));
    public bool IsGolden => rarity == AssignmentRarity.Golden;
    // 황금·다이아·졸업 작품이 아닌 카드. Delete 키, Ctrl+Z, 공모전 수상, 졸업 프로젝트의 대상.
    public bool IsNormal => rarity == AssignmentRarity.Normal;
    public bool IsCompleted => isCompleted;
    public float HpRatio => (float)currentHp / MaxHp;
    // 하이라이트(개발 툴 명중) 효과가 끝나는 시각. Time.unscaledTimeAsDouble 기준.
    public double HighlightedUntil;

    public void Initialize(AssignmentData data, AssignmentRarity rarity = AssignmentRarity.Normal,
        float hpScale = 1f, float pointScale = 1f)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        this.data = data;
        this.rarity = rarity;
        this.hpScale = hpScale;
        this.pointScale = pointScale;
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
