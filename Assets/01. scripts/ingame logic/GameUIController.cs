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
    VisualElement effectLayer;
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
    Button addPointsButton;
    Button unlockAllButton;
    Button resetProgressButton;
    VisualElement resetProgressOverlay;
    Button confirmResetProgressButton;
    Button cancelResetProgressButton;
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
        public VisualElement progress;
        public Action clickedCallback;
        public float teleportTimer;
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
        addPointsButton = root.Q<Button>("add-points-button");
        resetProgressButton = root.Q<Button>("reset-progress-button");
        resetProgressOverlay = root.Q<VisualElement>("reset-progress-overlay");
        confirmResetProgressButton = root.Q<Button>("confirm-reset-progress-button");
        cancelResetProgressButton = root.Q<Button>("cancel-reset-progress-button");
        if (assignmentLayer == null || scoreValue == null || timerValue == null ||
            resultOverlay == null || finalPointsValue == null || restartButton == null || addPointsButton == null ||
            resetProgressButton == null || resetProgressOverlay == null ||
            confirmResetProgressButton == null || cancelResetProgressButton == null)
        {
            Debug.LogError("IngameUI의 필수 요소가 누락되었습니다.", this);
            return;
        }
        CreateFeverHud();
        assignmentLayer.Clear();
        foreach (string side in new[] { "top", "bottom", "left", "right" })
        {
            var outsideDim = new VisualElement { pickingMode = PickingMode.Ignore };
            outsideDim.AddToClassList("outside-dim");
            outsideDim.AddToClassList(side);
            assignmentLayer.Add(outsideDim);
        }
        // 새 카드가 이펙트를 덮지 않도록 카드 레이어 바로 위에 이펙트 전용 레이어를 둔다.
        effectLayer = new VisualElement { name = "effect-layer", pickingMode = PickingMode.Ignore };
        effectLayer.AddToClassList("effect-layer");
        assignmentLayer.parent.Insert(assignmentLayer.parent.IndexOf(assignmentLayer) + 1, effectLayer);
        restartButton.clicked += gameManager.StartGame;
        addPointsButton.clicked += gameManager.AddPointsFromButton;
        unlockAllButton = root.Q<Button>("unlock-all-button");
        if (unlockAllButton != null) unlockAllButton.clicked += gameManager.UnlockAllFromButton;
        resetProgressButton.clicked += ShowResetProgressConfirmation;
        confirmResetProgressButton.clicked += ResetProgress;
        cancelResetProgressButton.clicked += HideResetProgressConfirmation;
        HideResetProgressConfirmation();
        root.RegisterCallback<PointerDownEvent>(PlayClickEffect, TrickleDown.TrickleDown);
        if (skillTable != null) skillTree = new SkillTreeUI(root, skillTable, gameManager.SkillProgress);
        else Debug.LogError("GameUIController에 SkillTable을 연결해주세요.", this);
        isBound = true;
    }

    public void UnbindUI()
    {
        skillTree?.Dispose();
        skillTree = null;
        if (restartButton != null) restartButton.clicked -= gameManager.StartGame;
        if (addPointsButton != null) addPointsButton.clicked -= gameManager.AddPointsFromButton;
        if (unlockAllButton != null) unlockAllButton.clicked -= gameManager.UnlockAllFromButton;
        if (resetProgressButton != null) resetProgressButton.clicked -= ShowResetProgressConfirmation;
        if (confirmResetProgressButton != null) confirmResetProgressButton.clicked -= ResetProgress;
        if (cancelResetProgressButton != null) cancelResetProgressButton.clicked -= HideResetProgressConfirmation;
        root?.UnregisterCallback<PointerDownEvent>(PlayClickEffect, TrickleDown.TrickleDown);
        effectLayer?.RemoveFromHierarchy();
        effectLayer = null;
        feverOutline?.RemoveFromHierarchy();
        feverInnerOutline?.RemoveFromHierarchy();
        feverCountdown?.RemoveFromHierarchy();
        root?.RemoveFromClassList("fever-active");
        feverPulseTask?.Pause();
        feverPulseTask = null;
        feverOutline = feverInnerOutline = null;
        feverCountdown = null;
        ClearAssignments();
        isBound = false;
    }

    void ShowResetProgressConfirmation()
    {
        SkillProgress progress = gameManager.SkillProgress;
        root.Q<Label>("rebirth-message").text = $"{progress.RebirthCount + 1}회차 환생 / 비용 {progress.NextRebirthCost:N0} P / 보상 {2L * (progress.RebirthCount + 1):N0} RP\n조건: 보유 {progress.Points:N0} P / 필요 {progress.NextRebirthCost:N0} P";
        confirmResetProgressButton.SetEnabled(progress.CanRebirth);
        if (resetProgressOverlay != null) resetProgressOverlay.style.display = DisplayStyle.Flex;
    }

    void HideResetProgressConfirmation()
    {
        if (resetProgressOverlay != null) resetProgressOverlay.style.display = DisplayStyle.None;
    }

    void ResetProgress()
    {
        if (!gameManager.SkillProgress.TryRebirth()) { ShowResetProgressConfirmation(); return; }
        HideResetProgressConfirmation();
        gameManager.StartGame();
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

    void CreateFeverHud()
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

    public void UpdateFeverState(bool active, float remaining)
    {
        if (!isBound) return;
        if (active)
        {
            feverCountdown.text = $"FEVER  {remaining:0.0}s  ·  클릭 피해 ×2";
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
        popup.style.top = Mathf.Clamp(localPosition.y - 42f, 0f, Mathf.Max(0f, assignmentLayer.resolvedStyle.height - 35f));
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
        if (assignment.IsGolden) binding.card.AddToClassList("golden");
        bool hasIcon = assignment.Data.Icon != null;
        if (hasIcon)
        {
            var icon = new Image
            {
                image = assignment.Data.Icon,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            icon.AddToClassList("assignment-icon");
            binding.card.Add(icon);
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
        var background = new VisualElement { pickingMode = PickingMode.Ignore };
        background.AddToClassList("hp-background");
        binding.progress = new VisualElement { pickingMode = PickingMode.Ignore };
        binding.progress.AddToClassList("card-progress");
        background.Add(binding.progress);
        binding.card.Add(background);
        // 중앙 스폰 박스(테두리 안쪽) 내부에 카드 전체가 들어오도록 배치한다.
        float width = assignmentLayer.contentRect.width;
        float height = assignmentLayer.contentRect.height;
        if (float.IsNaN(width)) width = 1920f;
        if (float.IsNaN(height)) height = 1080f;
        const float cardWidth = 80f;
        const float cardHeight = 80f;
        binding.card.style.left = UnityEngine.Random.Range(0f, Mathf.Max(0f, width - cardWidth));
        binding.card.style.top = UnityEngine.Random.Range(0f, Mathf.Max(0f, height - cardHeight));
        binding.teleportTimer = UnityEngine.Random.Range(1f, 2f);
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

        binding.teleportTimer -= deltaTime;
        if (binding.teleportTimer > 0f) return;

        float cardWidth = binding.card.resolvedStyle.width;
        float cardHeight = binding.card.resolvedStyle.height;
        float layerWidth = assignmentLayer.contentRect.width;
        float layerHeight = assignmentLayer.contentRect.height;
        if (float.IsNaN(cardWidth) || cardWidth <= 0f) cardWidth = 80f;
        if (float.IsNaN(cardHeight) || cardHeight <= 0f) cardHeight = 80f;
        if (float.IsNaN(layerWidth) || layerWidth <= cardWidth
            || float.IsNaN(layerHeight) || layerHeight <= cardHeight)
        {
            binding.teleportTimer = 0.5f;
            return;
        }

        float maxLeft = layerWidth - cardWidth;
        float maxTop = layerHeight - cardHeight;
        Vector2 currentPosition = new Vector2(binding.card.resolvedStyle.left, binding.card.resolvedStyle.top);
        Vector2 destination = currentPosition;
        for (int attempt = 0; attempt < 4 && Vector2.Distance(currentPosition, destination) < 160f; attempt++)
            destination = new Vector2(UnityEngine.Random.Range(0f, maxLeft), UnityEngine.Random.Range(0f, maxTop));

        binding.card.style.left = destination.x;
        binding.card.style.top = destination.y;
        binding.teleportTimer = UnityEngine.Random.Range(1f, 2f);
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
        PlaySpriteEffect("fx-taskbreak", 6, binding.card.worldBound.center, 96f);
        binding.card.clicked -= binding.clickedCallback;
        binding.card.RemoveFromHierarchy();
        assignmentCards.Remove(assignment);
    }

    void PlayClickEffect(PointerDownEvent evt) => PlaySpriteEffect("fx-click", 3, evt.position, 96f);

    void PlaySpriteEffect(string className, int frameCount, Vector2 worldCenter, float size)
    {
        if (!isBound) return;
        Vector2 center = effectLayer.WorldToLocal(worldCenter);
        // 프레임 하나 크기의 창(overflow: hidden) 안에서 가로 스트립 전체를 왼쪽으로 밀어 프레임을 넘긴다.
        var fx = new VisualElement { pickingMode = PickingMode.Ignore };
        fx.AddToClassList("sprite-fx");
        fx.style.left = center.x - size / 2f;
        fx.style.top = center.y - size / 2f;
        fx.style.width = size;
        fx.style.height = size;
        var strip = new VisualElement { pickingMode = PickingMode.Ignore };
        strip.AddToClassList("sprite-fx-strip");
        strip.AddToClassList(className);
        strip.style.width = size * frameCount;
        strip.style.height = size;
        fx.Add(strip);
        effectLayer.Add(fx);
        int frame = 0;
        fx.schedule.Execute(() =>
        {
            if (++frame >= frameCount) { fx.RemoveFromHierarchy(); return; }
            strip.style.left = -frame * size;
        }).StartingIn(60).Every(60).Until(() => frame >= frameCount);
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
        finalPointsValue.text = $"최종 {finalPoints}점: {finalGrade}";
        resultOverlay.style.display = DisplayStyle.Flex;
    }
}
