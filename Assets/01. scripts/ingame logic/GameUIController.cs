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
<<<<<<< Updated upstream
    VisualElement timerPanel;
=======
    IVisualElementScheduledItem resultAnimation;
>>>>>>> Stashed changes
    VisualElement feverOutline;
    VisualElement feverInnerOutline;
    Label feverCountdown;
    IVisualElementScheduledItem feverPulseTask;
    bool feverPulseBright;
    Label finalPointsValue;
    Button restartButton;
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
        public VisualElement highlight;
        public int highlightVersion;
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
        resetProgressButton = root.Q<Button>("reset-progress-button");
        resetProgressOverlay = root.Q<VisualElement>("reset-progress-overlay");
        confirmResetProgressButton = root.Q<Button>("confirm-reset-progress-button");
        cancelResetProgressButton = root.Q<Button>("cancel-reset-progress-button");
        if (assignmentLayer == null || scoreValue == null || timerValue == null ||
            resultOverlay == null || finalPointsValue == null || restartButton == null ||
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
        resetProgressButton.clicked += ShowResetProgressConfirmation;
        confirmResetProgressButton.clicked += ResetProgress;
        cancelResetProgressButton.clicked += HideResetProgressConfirmation;
        HideResetProgressConfirmation();
<<<<<<< Updated upstream
        root.RegisterCallback<PointerDownEvent>(PlayClickEffect, TrickleDown.TrickleDown);
=======
>>>>>>> Stashed changes
        if (skillTable != null) skillTree = new SkillTreeUI(root, skillTable, gameManager.SkillProgress);
        else Debug.LogError("GameUIController에 SkillTable을 연결해주세요.", this);
        isBound = true;
    }

    public void UnbindUI()
    {
        skillTree?.Dispose();
        skillTree = null;
        if (restartButton != null) restartButton.clicked -= gameManager.StartGame;
        if (resetProgressButton != null) resetProgressButton.clicked -= ShowResetProgressConfirmation;
        if (confirmResetProgressButton != null) confirmResetProgressButton.clicked -= ResetProgress;
        if (cancelResetProgressButton != null) cancelResetProgressButton.clicked -= HideResetProgressConfirmation;
<<<<<<< Updated upstream
        root?.UnregisterCallback<PointerDownEvent>(PlayClickEffect, TrickleDown.TrickleDown);
        effectLayer?.RemoveFromHierarchy();
        effectLayer = null;
=======
>>>>>>> Stashed changes
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
        root.Q<Label>("rebirth-message").text = $"{progress.RebirthCount + 1}회차 환생 / 비용 {progress.NextRebirthCost:N0} P / 보상 {progress.NextRebirthReward(skillTable):N0} RP\n조건: 보유 {progress.Points:N0} P / 필요 {progress.NextRebirthCost:N0} P";
        confirmResetProgressButton.SetEnabled(progress.CanRebirth);
        if (resetProgressOverlay != null) resetProgressOverlay.style.display = DisplayStyle.Flex;
    }

    void HideResetProgressConfirmation()
    {
        if (resetProgressOverlay != null) resetProgressOverlay.style.display = DisplayStyle.None;
    }

    void ResetProgress()
    {
<<<<<<< Updated upstream
        if (!gameManager.SkillProgress.TryRebirth()) { ShowResetProgressConfirmation(); return; }
=======
        if (!gameManager.SkillProgress.TryRebirth(skillTable)) { ShowResetProgressConfirmation(); return; }
>>>>>>> Stashed changes
        HideResetProgressConfirmation();
        gameManager.StartGame();
    }

    public void ShowGame()
    {
        if (!isBound) return;
<<<<<<< Updated upstream
=======
        tutorial?.Dispose();
        tutorial = null;
        resultAnimation?.Pause();
>>>>>>> Stashed changes
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
            feverCountdown.text = $"폭주 클릭  {remaining:0.0}s  ·  클릭 피해 ×2";
            feverCountdown.style.display = DisplayStyle.Flex;
        }
        else feverCountdown.style.display = DisplayStyle.None;
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
        if (assignment.Rarity == AssignmentRarity.Diamond) binding.card.AddToClassList("diamond");
        if (assignment.Rarity == AssignmentRarity.GradWork) binding.card.AddToClassList("grad-work");
        // 다이아·졸업 작품 카드는 스프라이트에 그림이 들어 있어 과목 아이콘을 얹지 않는다.
        bool specialCard = assignment.Rarity == AssignmentRarity.Diamond || assignment.Rarity == AssignmentRarity.GradWork;
        bool hasIcon = assignment.Data.Icon != null && !specialCard;
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
        else if (!specialCard)
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

    // 스킬 효과 판정은 모두 월드 좌표로 한다.
    public bool TryGetCardCenter(AssignmentBase assignment, out Vector2 center)
    {
        center = default;
        if (assignment == null || !assignmentCards.TryGetValue(assignment, out AssignmentCardBinding binding)) return false;
        center = binding.card.worldBound.center;
        return true;
    }

    public Rect FieldRect => assignmentLayer.LocalToWorld(assignmentLayer.contentRect);

    public bool IsCardNearSegment(AssignmentBase assignment, Vector2 from, Vector2 to, float radius)
        => TryGetCardCenter(assignment, out Vector2 center)
            && Vector2.Distance(Vector2.Lerp(from, to, SegmentProgress(assignment, from, to)), center) <= radius;

    // 브러시 획 두께(48px)의 절반만큼 카드 영역을 넓혀, 선분이 조금이라도 겹치면 닿은 것으로 본다 (Liang–Barsky).
    public bool CardTouchesSegment(AssignmentBase assignment, Vector2 from, Vector2 to)
    {
        if (assignment == null || !assignmentCards.TryGetValue(assignment, out AssignmentCardBinding binding)) return false;
        float pad = effectLayer.LocalToWorld(new Rect(0f, 0f, 24f, 0f)).width;
        Rect r = binding.card.worldBound;
        Vector2 d = to - from;
        float t0 = 0f, t1 = 1f;
        float[] p = { -d.x, d.x, -d.y, d.y };
        float[] q = { from.x - (r.xMin - pad), r.xMax + pad - from.x, from.y - (r.yMin - pad), r.yMax + pad - from.y };
        for (int i = 0; i < 4; i++)
        {
            if (p[i] == 0f)
            {
                if (q[i] < 0f) return false;
                continue;
            }
            float t = q[i] / p[i];
            if (p[i] < 0f) t0 = Mathf.Max(t0, t);
            else t1 = Mathf.Min(t1, t);
            if (t0 > t1) return false;
        }
        return true;
    }

    // 카드가 선분의 어디쯤(0~1)에 있는지
    public float SegmentProgress(AssignmentBase assignment, Vector2 from, Vector2 to)
    {
        if (!TryGetCardCenter(assignment, out Vector2 center)) return 0f;
        Vector2 segment = to - from;
        return segment.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(center - from, segment) / segment.sqrMagnitude) : 0f;
    }

    public void RemoveAssignment(AssignmentBase assignment)
    {
        if (!assignmentCards.TryGetValue(assignment, out AssignmentCardBinding binding)) return;
        PlaySpriteEffect("fx-taskbreak", 6, binding.card.worldBound.center, 96f, 96f);
        binding.card.clicked -= binding.clickedCallback;
        binding.card.RemoveFromHierarchy();
        assignmentCards.Remove(assignment);
    }

    void PlayClickEffect(PointerDownEvent evt) => PlaySpriteEffect("fx-click", 3, evt.position, 96f, 96f);

    public void PlayCardEffect(AssignmentBase assignment, string className, int frameCount, float width, float height)
    {
        if (TryGetCardCenter(assignment, out Vector2 center)) PlaySpriteEffect(className, frameCount, center, width, height);
    }

    public void PlayEffectAt(Vector2 worldCenter, string className, int frameCount, float width, float height)
        => PlaySpriteEffect(className, frameCount, worldCenter, width, height);

    void PlaySpriteEffect(string className, int frameCount, Vector2 worldCenter, float width, float height,
        int frameMs = 60, float angle = 0f)
    {
        if (!isBound) return;
        Vector2 center = effectLayer.WorldToLocal(worldCenter);
        // 프레임 하나 크기의 창(overflow: hidden) 안에서 가로 스트립 전체를 왼쪽으로 밀어 프레임을 넘긴다.
        var fx = new VisualElement { pickingMode = PickingMode.Ignore };
        fx.AddToClassList("sprite-fx");
        fx.style.left = center.x - width / 2f;
        fx.style.top = center.y - height / 2f;
        fx.style.width = width;
        fx.style.height = height;
        if (angle != 0f) fx.style.rotate = new Rotate(new Angle(angle));
        var strip = new VisualElement { pickingMode = PickingMode.Ignore };
        strip.AddToClassList("sprite-fx-strip");
        strip.AddToClassList(className);
        strip.style.width = width * frameCount;
        strip.style.height = height;
        fx.Add(strip);
        effectLayer.Add(fx);
        int frame = 0;
        fx.schedule.Execute(() =>
        {
            if (++frame >= frameCount) { fx.RemoveFromHierarchy(); return; }
            strip.style.left = -frame * width;
        }).StartingIn(frameMs).Every(frameMs).Until(() => frame >= frameCount);
    }

    // 회전한 막대로 두 점(월드 좌표)을 잇는다.
    VisualElement AddLine(Vector2 worldFrom, Vector2 worldTo, float thickness, string className, int lifetimeMs)
    {
        Vector2 a = effectLayer.WorldToLocal(worldFrom), delta = effectLayer.WorldToLocal(worldTo) - a;
        var line = new VisualElement { pickingMode = PickingMode.Ignore };
        line.AddToClassList(className);
        line.style.left = a.x;
        line.style.top = a.y - thickness / 2f;
        line.style.width = delta.magnitude;
        line.style.height = thickness;
        line.style.transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(50));
        line.style.rotate = new Rotate(new Angle(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg));
        effectLayer.Add(line);
        line.schedule.Execute(line.RemoveFromHierarchy).StartingIn(lifetimeMs);
        return line;
    }

    // 픽셀 브러시: seconds 동안 선을 따라 화면을 가로지르며 지나간 자리에 붓 자국을 남긴다.
    public void PlayBrush(Vector2 worldFrom, Vector2 worldTo, float seconds)
    {
        if (!isBound) return;
        Vector2 a = effectLayer.WorldToLocal(worldFrom), b = effectLayer.WorldToLocal(worldTo);
        float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
        var brush = new VisualElement { pickingMode = PickingMode.Ignore };
        brush.AddToClassList("obj-brush");
        effectLayer.Add(brush);
        float start = Time.unscaledTime, lastStroke = float.NegativeInfinity;
        brush.schedule.Execute(() =>
        {
            float now = Time.unscaledTime, t = Mathf.Clamp01((now - start) / seconds);
            Vector2 position = Vector2.Lerp(a, b, t);
            // 붓끝 피벗 (0.3, 0.3)이 선 위에 오도록 (64px 칸, 아래에서 위로 잰 값)
            brush.style.left = position.x - 64f * 0.3f;
            brush.style.top = position.y - 64f * 0.7f;
            brush.EnableInClassList("frame-1", (int)(now * 8f) % 2 == 1);
            if (now - lastStroke >= 0.03f)
            {
                lastStroke = now;
                PlaySpriteEffect("obj-stroke", 3, effectLayer.LocalToWorld(position), 96f, 48f, 120, angle);
            }
            if (t >= 1f) brush.RemoveFromHierarchy();
        }).Every(16).Until(() => brush.parent == null);
    }

    // Ctrl+Z: 파동이 왼쪽에서 오른쪽으로 화면을 가로지른다.
    public void PlayUndoWave()
    {
        if (!isBound) return;
        Rect field = effectLayer.WorldToLocal(FieldRect);
        var wave = new VisualElement { pickingMode = PickingMode.Ignore };
        wave.AddToClassList("obj-undo");
        float width = field.height / 2f;
        wave.style.top = field.yMin;
        wave.style.width = width;
        wave.style.height = field.height;
        effectLayer.Add(wave);
        float t = 0f;
        wave.schedule.Execute(() =>
        {
            wave.style.left = Mathf.Lerp(field.xMin - width, field.xMax, t = Mathf.Min(1f, t + 0.05f));
            if (t >= 1f) wave.RemoveFromHierarchy();
        }).Every(16).Until(() => t >= 1f);
    }

    public void PlayLaser(Vector2 worldFrom, Vector2 worldTo)
    {
        if (isBound) AddLine(worldFrom, worldTo, 4f, "robot-laser", 200);
    }

    // 연쇄 번개: 점들을 차례로 지그재그 번개로 잇는다.
    public void PlayChainLightning(List<Vector2> worldPoints)
    {
        if (!isBound) return;
        for (int i = 1; i < worldPoints.Count; i++)
        {
            Vector2 from = worldPoints[i - 1], to = worldPoints[i];
            Vector2 normal = Vector2.Perpendicular(to - from).normalized * (to - from).magnitude * 0.08f;
            Vector2 previous = from;
            for (int s = 1; s <= 4; s++)
            {
                Vector2 next = s == 4 ? to : Vector2.Lerp(from, to, s / 4f) + normal * UnityEngine.Random.Range(-1f, 1f);
                AddLine(previous, next, 3f, "chain-bolt", 250);
                previous = next;
            }
        }
    }

    // 하이라이트 표시. 다시 맞으면 시간을 새로 센다.
    public void ShowHighlight(AssignmentBase assignment, float seconds)
    {
        if (!assignmentCards.TryGetValue(assignment, out AssignmentCardBinding binding)) return;
        if (binding.highlight == null)
        {
            binding.highlight = new VisualElement { pickingMode = PickingMode.Ignore };
            binding.highlight.AddToClassList("obj-highlight");
            binding.card.Add(binding.highlight);
        }
        int version = ++binding.highlightVersion;
        binding.highlight.schedule.Execute(() =>
        {
            if (version != binding.highlightVersion || binding.highlight == null) return;
            binding.highlight.RemoveFromHierarchy();
            binding.highlight = null;
        }).StartingIn((long)(seconds * 1000));
    }

    public void ShowBuildBanner()
    {
        if (!isBound) return;
        var banner = new VisualElement { pickingMode = PickingMode.Ignore };
        banner.AddToClassList("obj-build-banner");
        effectLayer.Add(banner);
        banner.schedule.Execute(banner.RemoveFromHierarchy).StartingIn(900);
    }

    // 작품 제출: 2초 동안 타이머 옆에 정지 표시
    public void PlayTimerStop()
    {
        VisualElement timer = root?.Q("timer-panel");
        if (timer != null) PlaySpriteEffect("fx-timer-stop", 2, timer.worldBound.center + new Vector2(0f, timer.worldBound.height), 48f, 48f, 1000);
    }

    public void PlayGraduationBorder()
    {
        if (!isBound) return;
        Rect field = effectLayer.WorldToLocal(FieldRect);
        var border = new VisualElement { pickingMode = PickingMode.Ignore };
        border.AddToClassList("grad-border");
        border.style.left = field.xMin;
        border.style.top = field.yMin;
        border.style.width = field.width;
        border.style.height = field.height;
        effectLayer.Add(border);
        border.schedule.Execute(border.RemoveFromHierarchy).StartingIn(700);
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
<<<<<<< Updated upstream
=======

        // 클릭하던 손으로 재시작을 바로 누르지 않도록: 화면이 먼저 어두워지고,
        // 패널이 아래에서 올라와 살짝 넘쳤다가 제자리로 온다. 그동안 버튼은 눌리지 않는다.
        const float DimSeconds = 0.3f, RiseSeconds = 0.45f, BackOvershoot = 1.70158f;
        VisualElement panel = resultOverlay.Q(className: "result-panel");
        List<Button> buttons = panel.Query<Button>().ToList();
        buttons.ForEach(button => button.pickingMode = PickingMode.Ignore);
        float drop = resultOverlay.parent.resolvedStyle.height;
        if (float.IsNaN(drop) || drop <= 0f) drop = 1080f;
        float start = Time.unscaledTime;
        resultAnimation?.Pause();
        resultOverlay.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        panel.style.translate = new Translate(0, drop);
        resultAnimation = resultOverlay.schedule.Execute(() =>
        {
            float elapsed = Time.unscaledTime - start;
            resultOverlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.72f * Mathf.Clamp01(elapsed / DimSeconds));
            float k = Mathf.Clamp01((elapsed - DimSeconds) / RiseSeconds) - 1f;
            float eased = 1f + (BackOvershoot + 1f) * k * k * k + BackOvershoot * k * k;  // easeOutBack
            panel.style.translate = new Translate(0, (1f - eased) * drop);
            if (elapsed < DimSeconds + RiseSeconds) return;
            resultAnimation.Pause();
            buttons.ForEach(button => button.pickingMode = PickingMode.Position);
            tutorial?.OnRoundEnded();
        }).Every(16);
>>>>>>> Stashed changes
    }
}
