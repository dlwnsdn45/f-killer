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

[CreateAssetMenu(fileName = "AssignmentData", menuName = "F Killer/Assignment Data")]
public sealed class AssignmentData : ScriptableObject
{
    [SerializeField] string title = "과제";
    [SerializeField] Texture2D icon;
    [SerializeField] AssignmentKind kind;
    [SerializeField, Min(1)] int maxHp = 3;
    [SerializeField, Min(1)] int points = 3;
    [SerializeField, Min(0f)] float movementSpeed = 90f;
    [Tooltip("황금 과제로 등장할 확률")]
    [SerializeField, Range(0f, 1f)] float goldChance = 0.15f;

    public string Title => title;
    public Texture2D Icon => icon;
    public AssignmentKind Kind => kind;
    public int MaxHp => Mathf.Max(1, maxHp);
    public int Points => Mathf.Max(1, points);
    public float MovementSpeed => Mathf.Max(0f, movementSpeed);
    public float GoldChance => Mathf.Clamp01(goldChance);
    public const int GoldHpMultiplier = 2;
    public const int GoldPointMultiplier = 5;
}
