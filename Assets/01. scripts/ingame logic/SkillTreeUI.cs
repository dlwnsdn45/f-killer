using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// UXML owns the frame, SkillTable owns definitions and node positions, SkillProgress owns purchases.
public sealed class SkillTreeUI : IDisposable
{
    const float BoardSize = 2000f;
    // 스프라이트 1px = 보드 3px. 확대는 도트가 깨지지 않게 화면 배율이 정수(1~4배)가 되는 값만 쓴다.
    const float PixelScale = 3f;
    static readonly float[] ZoomSteps = { 1f / 3f, 2f / 3f, 1f, 4f / 3f };
    // 스킬트리 스프라이트 가이드의 추천 틴트. 흰 칸에 곱해진다.
    static readonly Color[] SectorColors =
    {
        new Color32(0xEC, 0x80, 0x78, 255),  // A 클릭 공격
        new Color32(0x8A, 0xD6, 0xC8, 255),  // B 개발 툴
        new Color32(0xFC, 0xC4, 0x84, 255),  // C 시간·과제
        new Color32(0xC4, 0xAA, 0xE8, 255),  // D 포인트
        new Color32(0x8C, 0xB0, 0xE8, 255),  // E 로봇
    };
    static readonly Color LockedDim = new Color(0.55f, 0.55f, 0.55f, 1f);
    static readonly Color LockedIcon = new Color(0.45f, 0.45f, 0.5f);
    static readonly Color LockedLine = new Color32(0x3E, 0x3C, 0x52, 255);
    static readonly Color SilhouetteColor = new Color(0.16f, 0.16f, 0.19f);
    readonly List<(VisualElement line, SkillData from, SkillData to)> edges = new List<(VisualElement, SkillData, SkillData)>();
    readonly IVisualElementScheduledItem pulseTask;
    int pulseFrame;
    int zoomStep = 1;
    float zoom => ZoomSteps[zoomStep];
    readonly VisualElement host, overlay, viewport, popup, skillBoard, rebirthBoard;
    VisualElement board;
    readonly Button skillTab, rebirthTab;
    readonly List<Button> rebirthNodes = new List<Button>();
    readonly List<Action> rebirthClicks = new List<Action>();
    bool rebirthMode;
    int selectedRebirth = -1;
    readonly Label points, heading, description, requirement;
    readonly Button opener, close, buy;
    readonly List<Button> nodes = new List<Button>();
    readonly Dictionary<Button, Action> clicks = new Dictionary<Button, Action>();
    readonly SkillTable table;
    readonly SkillProgress progress;
    Button selectedNode;
    Vector2 pan, dragStart, panStart;
    int dragPointer = -1;
    bool centered;
<<<<<<< Updated upstream
=======
    string tutorialSkillToCenter;
    public event Action<string> SkillInspected;
    public event Action<string> SkillPurchased;
    public event Action Opened;

    public void FocusTutorialSkill(string id)
    {
        SwitchTree(false);
        tutorialSkillToCenter = id;
        CenterTutorialSkill();
    }

    void CenterTutorialSkill()
    {
        SkillData skill = table.Find(tutorialSkillToCenter);
        float width = viewport.resolvedStyle.width, height = viewport.resolvedStyle.height;
        if (skill == null || float.IsNaN(width) || float.IsNaN(height) || width <= 0 || height <= 0) return;
        pan = new Vector2(width / 2, height * 0.3f) - skill.Position * zoom;
        ApplyPan();
    }
>>>>>>> Stashed changes

    // 교차·졸업 프로젝트·시작 노드는 흰색(틴트 없음)
    static Color ColorOf(SkillData skill) => skill.Type != SkillNodeType.Cross && skill.Sector >= 0 && skill.Sector < SectorColors.Length
        ? SectorColors[skill.Sector] : Color.white;
    static bool Tinted(SkillData skill) => skill.Type != SkillNodeType.Start && skill.Type != SkillNodeType.Graduation;

    // 연결선을 먼저 깔고 그 위에 노드를 올린다. 졸업 프로젝트는 다섯 최종 노드와 멀리 떨어져 있어 선을 생략한다.
    void BuildSkillBoard()
    {
        board.Clear();
        board.style.width = BoardSize;
        board.style.height = BoardSize;
        board.style.transformOrigin = new TransformOrigin(0, 0);
        foreach (SkillData skill in table.Skills)
        {
            if (skill == null || skill.Type == SkillNodeType.Graduation) continue;
            foreach (string id in skill.PrerequisiteIds)
            {
                SkillData parent = table.Find(id);
                if (parent == null) continue;
                Vector2 delta = skill.Position - parent.Position;
                var line = new VisualElement { pickingMode = PickingMode.Ignore };
                line.AddToClassList("tree-line");
                // 두께 1 도트(3px)
                line.style.left = parent.Position.x;
                line.style.top = parent.Position.y - PixelScale / 2;
                line.style.width = Mathf.Round(delta.magnitude);
                line.style.height = PixelScale;
                line.style.transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(50));
                line.style.rotate = new Rotate(new Angle(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg));
                board.Add(line);
                edges.Add((line, parent, skill));
            }
        }
        foreach (SkillData skill in table.Skills)
        {
            if (skill == null || string.IsNullOrWhiteSpace(skill.Id)) continue;
            var button = new Button { name = skill.Id };
            button.AddToClassList("tree-node");
            button.AddToClassList("node-" + skill.Type.ToString().ToLowerInvariant());
            button.style.left = skill.Position.x;
            button.style.top = skill.Position.y;
            var pulse = new VisualElement { name = "pulse", pickingMode = PickingMode.Ignore };
            pulse.AddToClassList("tree-pulse");
            button.Add(pulse);
            if (skill.Icon != null)
            {
                var icon = new VisualElement { name = "icon", pickingMode = PickingMode.Ignore };
                icon.AddToClassList("tree-icon");
                icon.style.backgroundImage = skill.Icon;
                button.Add(icon);
            }
            if (skill.RequiredGradeIndex > 0)
            {
                var badge = new VisualElement { pickingMode = PickingMode.Ignore };
                badge.AddToClassList("grade-badge");
                badge.AddToClassList("badge-" + skill.RequiredGradeIndex);
                button.Add(badge);
            }
            board.Add(button);
            nodes.Add(button);
            Action clicked = () => Inspect(button);
            clicks.Add(button, clicked);
            button.clicked += clicked;
        }
    }

    string RequirementText(SkillData skill)
    {
        if (skill.IsStart) return "시작점";
        var names = new List<string>();
        foreach (string id in skill.PrerequisiteIds) names.Add($"{table.Find(id)?.Title ?? id}({id})");
        string text = "선행: " + string.Join(skill.RequireAllPrerequisites ? " 그리고 " : " 또는 ", names);
        if (skill.RequiredGradeIndex > 0)
            text += $"\n링 {skill.Ring} · 최고 학점 {GameManager.GradeNames[skill.RequiredGradeIndex]} 이상";
        return text;
    }

    public SkillTreeUI(VisualElement root, SkillTable table, SkillProgress progress)
    {
        this.table = table;
        this.progress = progress;
        host = root.Q("skill-tree-host");
        overlay = host.Q("skill-tree-overlay");
        viewport = host.Q("tree-viewport");
        board = host.Q("skill-tree-board");
        skillBoard = board;
        popup = host.Q("skill-description");
        points = host.Q<Label>("tree-points");
        heading = host.Q<Label>("tree-detail-title");
        description = host.Q<Label>("tree-detail-effect");
        requirement = host.Q<Label>("tree-detail-requirement");
        buy = host.Q<Button>("tree-buy");
        close = host.Q<Button>("tree-close");
        opener = root.Q<Button>("skill-tree-button");
        var tabs = new VisualElement();
        tabs.style.flexDirection = FlexDirection.Row;
        skillTab = new Button(() => SwitchTree(false)) { text = "스킬" };
        rebirthTab = new Button(() => SwitchTree(true)) { text = "환생" };
        tabs.Add(skillTab);
        tabs.Add(rebirthTab);
        host.Q(className: "tree-toolbar").Insert(0, tabs);
        tabs.style.display = DisplayStyle.None;  // 환생 시스템 보류: 스킬/환생 탭 숨김
        rebirthBoard = new VisualElement { name = "rebirth-tree-board" };
        rebirthBoard.AddToClassList("tree-board");
        rebirthBoard.style.display = DisplayStyle.None;
        viewport.Add(rebirthBoard);
        Vector2 center = new Vector2(510, 360);
        Vector2[] positions = { new Vector2(240, 170), new Vector2(190, 510), new Vector2(700, 110), new Vector2(510, 650), new Vector2(840, 350) };
        for (int i = 0; i < 5; i++)
        {
            Vector2 delta = positions[i] - center;
            var edge = new VisualElement { pickingMode = PickingMode.Ignore };
            edge.AddToClassList("tree-line");
            edge.style.left = center.x;
            edge.style.top = center.y;
            edge.style.width = delta.magnitude;
            edge.style.height = 2;
            edge.style.backgroundColor = new Color(0.9f, 0.5f, 0.85f);
            edge.style.transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(50));
            edge.style.rotate = new Rotate(new Angle(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg));
            rebirthBoard.Add(edge);
        }
        var origin = new Label("환생");
        origin.AddToClassList("rebirth-origin");
        origin.style.left = center.x;
        origin.style.top = center.y;
        rebirthBoard.Add(origin);
        for (int i = 0; i < 5; i++)
        {
            int index = i;
            var node = new Button { name = "rebirth-" + i };
            node.AddToClassList("rebirth-node");
            node.style.left = positions[i].x;
            node.style.top = positions[i].y;
            Action click = () => InspectRebirth(index);
            node.clicked += click;
            rebirthClicks.Add(click);
            rebirthNodes.Add(node);
            rebirthBoard.Add(node);
        }
        BuildSkillBoard();
        // 4fps: 구매 가능 고리는 3프레임 루프, 최종·졸업 반짝임은 [완료, 반짝임A, 완료, 반짝임B]
        pulseTask = overlay.schedule.Execute(() =>
        {
            pulseFrame = (pulseFrame + 1) % 12;
            overlay.EnableInClassList("pulse-1", pulseFrame % 3 == 1);
            overlay.EnableInClassList("pulse-2", pulseFrame % 3 == 2);
            overlay.EnableInClassList("shine-a", pulseFrame % 4 == 1);
            overlay.EnableInClassList("shine-b", pulseFrame % 4 == 3);
        }).Every(250);
        opener.clicked += Show;
        close.clicked += Hide;
        buy.clicked += PurchaseSelected;
        progress.Changed += Refresh;
        overlay.RegisterCallback<KeyDownEvent>(OnKeyDown);
        viewport.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
        viewport.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        viewport.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
        viewport.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
        viewport.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
        viewport.RegisterCallback<ClickEvent>(OnBackgroundClick);
        viewport.RegisterCallback<GeometryChangedEvent>(OnViewportGeometry);
        viewport.RegisterCallback<WheelEvent>(OnWheel);
        Refresh();
        Hide();
    }

    SkillData Data(Button node) => table.Find(node.name);

    void SwitchTree(bool rebirth)
    {
        StopDrag();
        rebirthMode = rebirth;
        selectedNode = null;
        selectedRebirth = -1;
        popup.style.display = DisplayStyle.None;
        skillBoard.style.display = rebirth ? DisplayStyle.None : DisplayStyle.Flex;
        rebirthBoard.style.display = rebirth ? DisplayStyle.Flex : DisplayStyle.None;
        board = rebirth ? rebirthBoard : skillBoard;
        ApplyPan();
        Refresh();
    }

    void InspectRebirth(int index)
    {
        selectedRebirth = index;
        heading.text = SkillProgress.RebirthNames[index];
        heading.style.color = new Color(0.9f, 0.5f, 0.85f);
        description.text = SkillProgress.RebirthDescriptions[index];
        int level = progress.RebirthLevel(index);
        bool max = level >= SkillProgress.RebirthMaxLevels[index];
        requirement.text = $"Lv. {level} / {SkillProgress.RebirthMaxLevels[index]} · 영구 유지 · 선행 없음";
        buy.style.display = DisplayStyle.Flex;
        buy.text = max ? "최대 레벨" : $"{progress.RebirthCost(index)} RP 구매";
        buy.SetEnabled(!max && progress.RebirthPoints >= progress.RebirthCost(index));
        popup.style.left = 12;
        popup.style.top = 64;
        popup.style.display = DisplayStyle.Flex;
    }

    void Inspect(Button node)
    {
        if (dragPointer >= 0) return;
        selectedNode = node;
        SkillData skill = Data(node);
        heading.text = skill.Title;
        heading.style.color = ColorOf(skill);
        description.text = skill.Description;
        requirement.text = RequirementText(skill);
        buy.style.display = skill.IsStart ? DisplayStyle.None : DisplayStyle.Flex;
        int cost = progress.CostOf(table, skill);
        buy.text = $"{cost:N0} P 구매";
        SkillPurchaseState state = progress.GetState(table, skill.Id);
        buy.SetEnabled(state == SkillPurchaseState.Available);
        if (state == SkillPurchaseState.Owned) buy.text = "구매 완료";
        else if (state == SkillPurchaseState.Locked) buy.text = "선행 스킬 필요";
        else if (state == SkillPurchaseState.GradeLocked) buy.text = $"최고 학점 {GameManager.GradeNames[skill.RequiredGradeIndex]} 필요";
        else if (state == SkillPurchaseState.InsufficientPoints) buy.text = $"{cost:N0} P · 포인트 부족";
        else if (state == SkillPurchaseState.Invalid) buy.text = "구매 불가";
        float width = overlay.resolvedStyle.width;
        float height = overlay.resolvedStyle.height;
        float popupWidth = Mathf.Min(320, Mathf.Max(180, width - 24));
        popup.style.width = popupWidth;
        Vector2 anchor = overlay.WorldToLocal(node.worldBound.center);
        popup.style.left = Mathf.Clamp(anchor.x - popupWidth / 2, 12, Mathf.Max(12, width - popupWidth - 12));
        popup.style.top = Mathf.Clamp(anchor.y > height / 2 ? anchor.y - 220 : anchor.y + 26, 60, Mathf.Max(60, height - 210));
        popup.style.display = DisplayStyle.Flex;
    }

    void OnPointerDown(PointerDownEvent evt)
    {
        if (evt.button != 0 || dragPointer >= 0) return;
        if (evt.target != viewport && evt.target != board) return;
        dragPointer = evt.pointerId;
        dragStart = evt.position;
        panStart = pan;
        popup.style.display = DisplayStyle.None;
        viewport.CapturePointer(dragPointer);
        evt.StopImmediatePropagation();
    }

    void OnPointerMove(PointerMoveEvent evt)
    {
        if (evt.pointerId != dragPointer) return;
        if ((evt.pressedButtons & 1) == 0) { StopDrag(); return; }
        pan = panStart + (Vector2)evt.position - dragStart;
        ApplyPan();
        evt.StopPropagation();
    }

    void OnPointerUp(PointerUpEvent evt)
    {
        if (evt.pointerId != dragPointer || evt.button != 0) return;
        StopDrag();
        evt.StopImmediatePropagation();
    }

    void OnCaptureOut(PointerCaptureOutEvent evt) { if (evt.pointerId == dragPointer) dragPointer = -1; }
    void OnPointerCancel(PointerCancelEvent evt) { if (evt.pointerId == dragPointer) StopDrag(); }
    void StopDrag()
    {
        int pointer = dragPointer;
        dragPointer = -1;
        if (pointer >= 0 && viewport.HasPointerCapture(pointer)) viewport.ReleasePointer(pointer);
    }

    void ApplyPan()
    {
        pan = new Vector2(Mathf.Round(pan.x), Mathf.Round(pan.y));
        board.style.left = pan.x;
        board.style.top = pan.y;
        skillBoard.style.scale = new Scale(new Vector3(zoom, zoom, 1f));
    }

    void OnViewportGeometry(GeometryChangedEvent evt)
    {
        if (!centered && evt.newRect.width > 0 && evt.newRect.height > 0)
        {
            // 처음 열면 가운데 [입학]이 화면 중앙에 오게 한다.
            SkillData start = table.Find("S0");
            Vector2 origin = start != null ? start.Position : new Vector2(BoardSize, BoardSize) / 2;
            pan = new Vector2(evt.newRect.width, evt.newRect.height) / 2 - origin * zoom;
            ApplyPan();
            centered = true;
        }
        popup.style.display = DisplayStyle.None;
    }

    // 마우스 위치를 기준으로 한 단계씩 확대·축소 (도트 1~4배)
    void OnWheel(WheelEvent evt)
    {
        if (rebirthMode) return;
        int nextStep = Mathf.Clamp(zoomStep + (evt.delta.y > 0 ? -1 : 1), 0, ZoomSteps.Length - 1);
        if (nextStep == zoomStep) { evt.StopPropagation(); return; }
        Vector2 mouse = viewport.WorldToLocal(evt.mousePosition);
        pan = mouse - (mouse - pan) * (ZoomSteps[nextStep] / zoom);
        zoomStep = nextStep;
        popup.style.display = DisplayStyle.None;
        ApplyPan();
        evt.StopPropagation();
    }

    void OnBackgroundClick(ClickEvent evt)
    {
        if (evt.target == viewport || evt.target == board) popup.style.display = DisplayStyle.None;
    }
    void OnKeyDown(KeyDownEvent evt) { if (evt.keyCode == KeyCode.Escape) { Hide(); evt.StopPropagation(); } }
    void PurchaseSelected()
    {
        if (rebirthMode) { if (selectedRebirth >= 0) progress.TryPurchaseRebirth(selectedRebirth); Refresh(); return; }
        if (selectedNode == null) return;
        progress.TryPurchase(table, selectedNode.name);
        Refresh();
    }

    void Refresh()
    {
        points.text = rebirthMode ? $"환생 {progress.RebirthCount}회 {progress.RebirthPoints:N0} RP" : $"{progress.Points:N0} P";
        skillTab.SetEnabled(rebirthMode);
        rebirthTab.SetEnabled(!rebirthMode);
        for (int i = 0; i < rebirthNodes.Count; i++)
        {
            int level = progress.RebirthLevel(i);
            rebirthNodes[i].text = $"{SkillProgress.RebirthNames[i]}\nLv. {level}/{SkillProgress.RebirthMaxLevels[i]}";
            rebirthNodes[i].EnableInClassList("owned", level > 0);
            rebirthNodes[i].EnableInClassList("affordable", level < SkillProgress.RebirthMaxLevels[i] && progress.RebirthPoints >= progress.RebirthCost(i));
        }
        if (rebirthMode && selectedRebirth >= 0 && popup.style.display.value == DisplayStyle.Flex) InspectRebirth(selectedRebirth);
        foreach (Button node in nodes)
        {
            SkillData skill = Data(node);
            SkillPurchaseState state = progress.GetState(table, node.name);
            // 산 노드와 이어진 노드만 보이고 나머지는 아예 숨긴다
            bool visible = progress.IsVisible(table, skill);
            node.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            node.EnableInClassList("owned", state == SkillPurchaseState.Owned);
            node.EnableInClassList("affordable", state == SkillPurchaseState.Available);
            node.EnableInClassList("grade-met", progress.BestGradeIndex >= skill.RequiredGradeIndex);
            node.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;
            // 잠김(보이지만 못 삼)은 섹터 색 ×0.55, 구매 가능·완료는 섹터 색 그대로. 졸업·시작은 풀커러라 스프라이트로만 구분.
            bool locked = state != SkillPurchaseState.Owned && state != SkillPurchaseState.Available;
            Color slot = !visible ? SilhouetteColor : !Tinted(skill) ? Color.white : locked ? ColorOf(skill) * LockedDim : ColorOf(skill);
            if (skill.Type != SkillNodeType.Start) node.style.unityBackgroundImageTintColor = slot;
            node.Q("pulse").style.unityBackgroundImageTintColor = Tinted(skill) ? ColorOf(skill) : Color.white;
            VisualElement icon = node.Q("icon");
            if (icon != null) icon.style.unityBackgroundImageTintColor = !visible ? Color.black : locked ? LockedIcon : Color.white;
        }
        // 선: 잠김 #3E3C52, 구매 가능 쪽으로 이어진 선은 섹터 색 ×0.55, 둘 다 샀으면 섹터 색
        foreach ((VisualElement line, SkillData from, SkillData to) in edges)
        {
            line.style.display = progress.IsVisible(table, from) && progress.IsVisible(table, to) ? DisplayStyle.Flex : DisplayStyle.None;
            line.style.backgroundColor = progress.IsOwned(from) && progress.IsOwned(to) ? ColorOf(to)
                : progress.IsOwned(from) ? ColorOf(to) * LockedDim : LockedLine;
        }
        if (selectedNode != null && popup.style.display.value == DisplayStyle.Flex) Inspect(selectedNode);
    }

    public void Show() { Refresh(); host.style.display = DisplayStyle.Flex; popup.style.display = DisplayStyle.None; overlay.Focus(); }
    public void Hide() { StopDrag(); host.style.display = DisplayStyle.None; opener?.Focus(); }

    public void Dispose()
    {
        StopDrag();
        opener.clicked -= Show;
        close.clicked -= Hide;
        buy.clicked -= PurchaseSelected;
        progress.Changed -= Refresh;
        for (int i = 0; i < rebirthNodes.Count; i++) rebirthNodes[i].clicked -= rebirthClicks[i];
        skillTab.parent.RemoveFromHierarchy();
        rebirthBoard.RemoveFromHierarchy();
        pulseTask?.Pause();
        foreach (Button node in nodes) node.clicked -= clicks[node];
        skillBoard.Clear();
        viewport.UnregisterCallback<WheelEvent>(OnWheel);
        overlay.UnregisterCallback<KeyDownEvent>(OnKeyDown);
        viewport.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
        viewport.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
        viewport.UnregisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
        viewport.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
        viewport.UnregisterCallback<PointerCancelEvent>(OnPointerCancel);
        viewport.UnregisterCallback<ClickEvent>(OnBackgroundClick);
        viewport.UnregisterCallback<GeometryChangedEvent>(OnViewportGeometry);
        host.style.display = DisplayStyle.None;
    }
}
