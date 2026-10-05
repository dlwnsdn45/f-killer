using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class GameUIController : MonoBehaviour
{
    [Header("씬 참조")]
    [SerializeField] PanelRenderer panelRenderer;
    [SerializeField] GameManager gameManager;
    [SerializeField] SkillTable skillTable;
    VisualElement root;
    VisualElement assignmentLayer;
    Label scoreValue;
    Label timerValue;
    Label gradeValue;
    Label nextGradeValue;
    VisualElement resultOverlay;
    VisualElement timerPanel;
    VisualElement feverOutline;
    VisualElement feverInnerOutline;
    Label feverCountdown;
    IVisualElementScheduledItem feverPulseTask;
    bool feverPulseBright;
    Label finalPointsValue;
    Button restartButton;
    Action restartClickedCallback;
    bool isBound;
    bool reloadRegistered;
    int uiVersion = -1;
    SkillTreeUI skillTree;
    public bool IsBound => isBound;
    public SkillTable SkillTable => skillTable;

    readonly Dictionary<AssignmentBase, AssignmentCardBinding> assignmentCards
        = new Dictionary<AssignmentBase, AssignmentCardBinding>();

    sealed class AssignmentCardBinding
    {
        public Button card;
        public Label title;
        public Image icon;
        public VisualElement progress;
        public Action clickedCallback;
        public float moveDirection;
    }

    void OnEnable()
    {
        panelRenderer.RegisterUIReloadCallback(HandleUIReload);
        reloadRegistered = true;
        BindUI();
    }

    void HandleUIReload(PanelRenderer renderer, VisualElement rootElement, int version)
    {
        if (isBound && uiVersion == version && root == rootElement) return;
        uiVersion = version;
        bool wasBound = isBound;
        UnbindUI();
        root = rootElement;
        BindUI();
        // UI 재생성 시 사라진 카드와 게임 상태를 함께 초기화한다.
        if (wasBound && gameManager != null) gameManager.StartGame();
    }

    void OnDisable()
    {
        if (gameManager != null && isBound) gameManager.EndGame();
        if (panelRenderer != null && reloadRegistered)
            panelRenderer.UnregisterUIReloadCallback(HandleUIReload);
        reloadRegistered = false;
        UnbindUI();
        root = null;
    }

    public void BindUI()
    {
        if (isBound) return;
        if (root == null || gameManager == null) return;
        assignmentLayer = root.Q<VisualElement>("assignment-layer");
        scoreValue = root.Q<Label>("score-value");
        timerValue = root.Q<Label>("timer-value");
        gradeValue = root.Q<Label>("grade-value");
        nextGradeValue = root.Q<Label>("next-grade-value");
        timerPanel = root.Q<VisualElement>("timer-panel");
        resultOverlay = root.Q<VisualElement>("result-overlay");
        finalPointsValue = root.Q<Label>("final-points");
        restartButton = root.Q<Button>("restart-button");
        if (assignmentLayer == null || scoreValue == null || timerValue == null ||
            resultOverlay == null || finalPointsValue == null || restartButton == null)
        {
            Debug.LogError("IngameUI의 필수 요소가 누락되었습니다.", this);
            return;
        }
        CreateComboHud();
        assignmentLayer.Clear();
        restartClickedCallback = gameManager.StartGame;
        restartButton.clicked += restartClickedCallback;
        assignmentLayer.RegisterCallback<ClickEvent>(HandlePlayfieldClick);
        if (skillTable != null) skillTree = new SkillTreeUI(root, skillTable, gameManager.SkillProgress);
        else Debug.LogError("GameUIController에 SkillTable을 연결해주세요.", this);
        isBound = true;
    }

    public void UnbindUI()
    {
        skillTree?.Dispose();
        skillTree = null;
        if (restartButton != null && restartClickedCallback != null)
            restartButton.clicked -= restartClickedCallback;
        assignmentLayer?.UnregisterCallback<ClickEvent>(HandlePlayfieldClick);
        feverOutline?.RemoveFromHierarchy();
        feverInnerOutline?.RemoveFromHierarchy();
        feverCountdown?.RemoveFromHierarchy();
        root?.RemoveFromClassList("fever-active");
        feverPulseTask?.Pause();
        feverPulseTask = null;
        feverOutline = feverInnerOutline = null;
        feverCountdown = null;
        restartClickedCallback = null;
        ClearAssignments();
        isBound = false;
    }

    public void ShowGame()
    {
        if (!isBound) return;
        resultOverlay.style.display = DisplayStyle.None;
        skillTree?.Hide();
        assignmentLayer.SetEnabled(true);
    }

    public void UpdateTimer(float remainingSeconds)
    {
        if (!isBound) return;
        float seconds = Mathf.Max(0f, remainingSeconds);
        timerValue.text = $"{(int)(seconds / 60):00}:{seconds % 60:00.0}";
        timerValue.EnableInClassList("timer-warning", seconds <= 3f);
    }

    void HandlePlayfieldClick(ClickEvent evt)
    {
        if (evt.target != assignmentLayer) return;
        gameManager.HandleMissClick(assignmentLayer.WorldToLocal(evt.position));
    }

    public void ShowOverloadFeedback(Vector2 position, bool blocked, int remainingBlocks)
    {
        if (!isBound) return;
        if (timerPanel != null)
        {
            timerPanel.AddToClassList("overload-flash");
            timerPanel.style.right = 14;
            timerPanel.schedule.Execute(() => timerPanel.style.right = 6).StartingIn(35);
            timerPanel.schedule.Execute(() => timerPanel.style.right = 14).StartingIn(70);
            timerPanel.schedule.Execute(() => timerPanel.style.right = 6).StartingIn(105);
            timerPanel.schedule.Execute(() => timerPanel.style.right = 10).StartingIn(140);
            timerPanel.schedule.Execute(() => timerPanel.RemoveFromClassList("overload-flash")).StartingIn(220);
        }

        var popup = new Label(blocked ? $"방어! 남은 횟수 {remainingBlocks}" : "-0.5s");
        popup.AddToClassList("overload-popup");
        popup.pickingMode = PickingMode.Ignore;
        Vector2 localPosition = assignmentLayer.WorldToLocal(position);
        popup.style.left = Mathf.Clamp(localPosition.x - 25, 8, Mathf.Max(8, assignmentLayer.resolvedStyle.width - 110));
        popup.style.top = Mathf.Clamp(localPosition.y - 28, 96, Mathf.Max(96, assignmentLayer.resolvedStyle.height - 40));
        assignmentLayer.Add(popup);
        popup.schedule.Execute(() => popup.RemoveFromHierarchy()).StartingIn(700);
    }

    void CreateComboHud()
    {
        feverOutline = new VisualElement { name = "fever-outline", pickingMode = PickingMode.Ignore };
        feverOutline.AddToClassList("fever-outline");
        feverInnerOutline = new VisualElement { name = "fever-inner-outline", pickingMode = PickingMode.Ignore };
        feverInnerOutline.AddToClassList("fever-inner-outline");
        feverCountdown = new Label("FEVER 2.0s") { name = "fever-countdown", pickingMode = PickingMode.Ignore };
        feverCountdown.AddToClassList("fever-banner");
        feverCountdown.style.display = DisplayStyle.None;
        root.Add(feverOutline);
        root.Add(feverInnerOutline);
        root.Add(feverCountdown);
    }

    public void UpdateComboState(int count, float remaining, bool unlocked, bool feverActive, float feverRemaining)
    {
        if (!isBound) return;
        if (feverActive)
        {
            feverCountdown.text = $"FEVER  {feverRemaining:0.0}s  ·  클릭 피해 ×2";
            feverCountdown.style.display = DisplayStyle.Flex;
        }
        else feverCountdown.style.display = DisplayStyle.None;
    }

    public void ShowComboFeedback(AssignmentBase assignment, int count)
    {
        if (!isBound || assignment == null || !assignmentCards.TryGetValue(assignment, out AssignmentCardBinding binding)) return;
        Vector2 localPosition = assignmentLayer.WorldToLocal(binding.card.worldBound.center);
        var popup = new Label($"콤보 {count}") { pickingMode = PickingMode.Ignore };
        popup.AddToClassList("combo-popup");
        popup.style.left = Mathf.Clamp(localPosition.x - 36f, 8f, Mathf.Max(8f, assignmentLayer.resolvedStyle.width - 90f));
        popup.style.top = Mathf.Clamp(localPosition.y - 42f, 90f, Mathf.Max(90f, assignmentLayer.resolvedStyle.height - 35f));
        assignmentLayer.Add(popup);
        popup.schedule.Execute(() => popup.RemoveFromHierarchy()).StartingIn(700);
    }

    public void SetFeverVisual(bool active)
    {
        if (root == null) return;
        root.EnableInClassList("fever-active", active);
        if (feverCountdown != null) feverCountdown.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
        if (!active)
        {
            feverPulseTask?.Pause();
            feverPulseTask = null;
            if (feverOutline != null) feverOutline.style.opacity = 1f;
            if (feverInnerOutline != null) feverInnerOutline.style.opacity = 1f;
            return;
        }

        if (feverPulseTask != null) return;
        feverPulseBright = false;
        feverPulseTask = feverOutline.schedule.Execute(() =>
        {
            feverPulseBright = !feverPulseBright;
            feverOutline.style.opacity = feverPulseBright ? 1f : 0.55f;
            feverInnerOutline.style.opacity = feverPulseBright ? 0.5f : 1f;
        }).Every(100);
    }

    public void PlayFeverHitEffect(AssignmentBase assignment)
    {
        if (!assignmentCards.TryGetValue(assignment, out AssignmentCardBinding binding)) return;
        binding.card.AddToClassList("fever-hit");
        binding.card.schedule.Execute(() => binding.card.RemoveFromClassList("fever-hit")).StartingIn(120);
    }

    public void PlayRobotAttackEffect(AssignmentBase assignment)
    {
        if (!assignmentCards.TryGetValue(assignment, out AssignmentCardBinding binding)) return;
        binding.card.AddToClassList("robot-hit");
        binding.card.schedule.Execute(() => binding.card.RemoveFromClassList("robot-hit")).StartingIn(180);

        Vector2 center = assignmentLayer.WorldToLocal(binding.card.worldBound.center);
        var ring = new VisualElement { pickingMode = PickingMode.Ignore };
        ring.AddToClassList("robot-hit-ring");
        ring.style.left = center.x - 14f;
        ring.style.top = center.y - 14f;
        assignmentLayer.Add(ring);
        ring.schedule.Execute(() =>
        {
            ring.style.left = center.x - 26f;
            ring.style.top = center.y - 26f;
            ring.style.width = 52f;
            ring.style.height = 52f;
            ring.style.opacity = 0.2f;
        }).StartingIn(20);
        ring.schedule.Execute(() => ring.RemoveFromHierarchy()).StartingIn(210);

    }

    public void UpdatePoints(int totalPoints)
    {
        if (isBound) scoreValue.text = $"총 포인트 {totalPoints}";
    }

    public void UpdateAcademicGrade(string grade, string nextGrade)
    {
        if (!isBound) return;
        if (gradeValue != null) gradeValue.text = grade;
        if (nextGradeValue != null) nextGradeValue.text = nextGrade;
    }

    public void AddAssignment(AssignmentBase assignment)
    {
        if (!isBound || assignment == null || assignment.Data == null || assignmentCards.ContainsKey(assignment)) return;
        var binding = new AssignmentCardBinding();
        binding.card = new Button();
        binding.card.AddToClassList("assignment-card");
        if (assignment.Data.Kind == AssignmentKind.Bug) binding.card.AddToClassList("bug");
        else if (assignment.Data.Kind == AssignmentKind.DrinkingParty) binding.card.AddToClassList("drinking-party");
        else if (assignment.Data.Kind == AssignmentKind.Sleep) binding.card.AddToClassList("sleep");
        if (assignment.IsGolden) binding.card.AddToClassList("golden");
        bool hasIcon = assignment.Data.Icon != null;
        if (hasIcon)
        {
            binding.icon = new Image
            {
                image = assignment.Data.Icon,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            binding.icon.AddToClassList("assignment-icon");
            binding.card.Add(binding.icon);
        }
        else
        {
            var paper = new VisualElement { pickingMode = PickingMode.Ignore };
            paper.AddToClassList("paper");
            for (int i = 0; i < 3; i++)
            {
                var line = new VisualElement { pickingMode = PickingMode.Ignore };
                line.AddToClassList("paper-line");
                if (i == 2) line.AddToClassList("short");
                paper.Add(line);
            }
            binding.card.Add(paper);
        }
        binding.title = new Label(assignment.IsGolden ? $"황금 {assignment.Data.Title}" : assignment.Data.Title) { pickingMode = PickingMode.Ignore };
        binding.title.AddToClassList("assignment-title");
        binding.card.Add(binding.title);
        var background = new VisualElement { pickingMode = PickingMode.Ignore };
        background.AddToClassList("hp-background");
        binding.progress = new VisualElement { pickingMode = PickingMode.Ignore };
        binding.progress.AddToClassList("card-progress");
        background.Add(binding.progress);
        binding.card.Add(background);
        // HUD 아래, 화면 내부에 카드 전체가 들어오도록 배치한다.
        float width = assignmentLayer.resolvedStyle.width;
        float height = assignmentLayer.resolvedStyle.height;
        if (float.IsNaN(width)) width = 1920f;
        if (float.IsNaN(height)) height = 1080f;
        const float cardWidth = 94f;
        const float cardHeight = 96f;
        binding.card.style.left = UnityEngine.Random.Range(0f, Mathf.Max(0f, width - cardWidth));
        binding.moveDirection = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        float top = Mathf.Min(110f, Mathf.Max(0f, height - cardHeight));
        binding.card.style.top = UnityEngine.Random.Range(top, Mathf.Max(top, height - cardHeight));
        binding.clickedCallback = () => gameManager.HandleAssignmentClicked(assignment);
        binding.card.clicked += binding.clickedCallback;
        assignmentCards.Add(assignment, binding);
        assignmentLayer.Add(binding.card);
        RefreshAssignment(assignment);
    }

    public void RefreshAssignment(AssignmentBase assignment)
    {
        if (assignmentCards.TryGetValue(assignment, out AssignmentCardBinding binding))
            binding.progress.style.width = Length.Percent(100f * assignment.CurrentHp / assignment.MaxHp);
    }

    public void MoveBug(AssignmentBase assignment, float deltaTime)
    {
        if (assignment == null || assignment.Data == null || assignment.Data.Kind != AssignmentKind.Bug
            || !assignmentCards.TryGetValue(assignment, out AssignmentCardBinding binding)) return;

        float cardWidth = binding.card.resolvedStyle.width;
        float layerWidth = assignmentLayer.resolvedStyle.width;
        if (float.IsNaN(cardWidth) || cardWidth <= 0f) cardWidth = 84f;
        if (float.IsNaN(layerWidth) || layerWidth <= cardWidth) return;
        float maxLeft = layerWidth - cardWidth;
        float left = binding.card.style.left.value.value;
        if (float.IsNaN(left)) left = binding.card.resolvedStyle.left;
        left += binding.moveDirection * assignment.Data.MovementSpeed * deltaTime;
        if (left < 0f)
        {
            left = -left;
            binding.moveDirection = 1f;
        }
        else if (left > maxLeft)
        {
            left = maxLeft - (left - maxLeft);
            binding.moveDirection = -1f;
        }
        binding.card.style.left = Mathf.Clamp(left, 0f, maxLeft);
    }

    public bool AreAssignmentsNear(AssignmentBase first, AssignmentBase second, float radius)
    {
        if (first == null || second == null || !assignmentCards.TryGetValue(first, out AssignmentCardBinding firstCard)
            || !assignmentCards.TryGetValue(second, out AssignmentCardBinding secondCard)) return false;
        return Vector2.Distance(firstCard.card.worldBound.center, secondCard.card.worldBound.center) <= radius;
    }

    public void RemoveAssignment(AssignmentBase assignment)
    {
        if (!assignmentCards.TryGetValue(assignment, out AssignmentCardBinding binding)) return;
        binding.card.clicked -= binding.clickedCallback;
        binding.card.RemoveFromHierarchy();
        assignmentCards.Remove(assignment);
    }

    public void ClearAssignments()
    {
        foreach (AssignmentCardBinding binding in assignmentCards.Values)
        {
            binding.card.clicked -= binding.clickedCallback;
            binding.card.RemoveFromHierarchy();
        }
        assignmentCards.Clear();
    }

    public void ShowResult(int finalPoints, string finalGrade)
    {
        if (!isBound) return;
        assignmentLayer.SetEnabled(false);
        finalPointsValue.text = $"최종 {finalPoints}점 · {finalGrade}";
        resultOverlay.style.display = DisplayStyle.Flex;
    }
}

