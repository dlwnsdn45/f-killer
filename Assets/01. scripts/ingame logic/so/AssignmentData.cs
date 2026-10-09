using UnityEngine;

public enum AssignmentKind
{
    Regular,
    TeamProject,
    SemesterProject,
    GraduationProject,
    Contest,
    Bug,
    DrinkingParty,
    Sleep
}

public enum AcademicGrade
{
    F,
    FPlus,
    D,
    DPlus,
    C,
    CPlus,
    B,
    BPlus,
    A,
    APlus,
    Graduation
}

[CreateAssetMenu(fileName = "AssignmentData", menuName = "F Killer/Assignment Data")]
public sealed class AssignmentData : ScriptableObject
{
    [SerializeField] string title = "과제";
    [SerializeField] Texture2D icon;
    [SerializeField] AssignmentKind kind;
    [SerializeField] AcademicGrade requiredGrade = AcademicGrade.F;
    [SerializeField, Min(1)] int maxHp = 3;
    [SerializeField, Min(1)] int points = 3;
    [SerializeField, Min(0f)] float movementSpeed = 90f;

    public string Title => title;
    public Texture2D Icon => icon;
    public AssignmentKind Kind => kind;
    public AcademicGrade RequiredGrade => requiredGrade;
    public int RequiredGradeIndex => (int)requiredGrade;
    public int MaxHp => Mathf.Max(1, maxHp);
    public int Points => Mathf.Max(1, points);
    public float MovementSpeed => Mathf.Max(0f, movementSpeed);
    public const int GoldHpMultiplier = 2;
    public const int GoldPointMultiplier = 5;
}
