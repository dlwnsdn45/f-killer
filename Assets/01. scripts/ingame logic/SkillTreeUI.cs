using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// UXML owns layout, SkillTable owns definitions, SkillProgress owns purchases.
public sealed class SkillTreeUI : IDisposable
{
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
        Button node = nodes.Find(item => item.name == tutorialSkillToCenter);
        float width = viewport.resolvedStyle.width, height = viewport.resolvedStyle.height;
        if (node == null || float.IsNaN(width) || float.IsNaN(height) || width <= 0 || height <= 0) return;
        pan = new Vector2(width / 2 - node.resolvedStyle.left, height * 0.3f - node.resolvedStyle.top);
        ApplyPan();
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
        rebirthBoard = new VisualElement { name = "rebirth-tree-board" };
        rebirthBoard.AddToClassList("tree-board");
        rebirthBoard.style.display = DisplayStyle.None;
        viewport.Add(rebirthBoard);
        Vector2 center = new Vector2(510, 360);
        Vector2[] positions = { new Vector2(240, 170), new Vector2(190, 510), new Vector2(700, 110), new Vector2(510, 650) };
        for (int i = 0; i < SkillProgress.RebirthNames.Length; i++)
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
        for (int i = 0; i < SkillProgress.RebirthNames.Length; i++)
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
        board.Query<Button>(className: "tree-node").ForEach(button =>
        {
            if (table.Find(button.name) == null)
            {
                Debug.LogError($"스킬 데이터가 없습니다: {button.name}");
                button.SetEnabled(false);
                return;
            }
            nodes.Add(button);
            Action clicked = () => Inspect(button);
            clicks.Add(button, clicked);
            button.clicked += clicked;
            button.RegisterCallback<GeometryChangedEvent>(OnNodeGeometry);
        });
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
        heading.style.color = node.resolvedStyle.borderTopColor;
        description.text = skill.Description;
        SkillData parent = table.Find(skill.PrerequisiteId);
        requirement.text = skill.IsStart ? "시작점" : $"Lv. {(progress.IsOwned(skill) ? 1 : 0)} / {skill.MaxLevel} 선행: {parent?.Title ?? "없음"}";
        buy.style.display = parent == null ? DisplayStyle.None : DisplayStyle.Flex;
        buy.text = $"{skill.Cost:N0} P 구매";
        SkillPurchaseState state = progress.GetState(table, skill.Id);
        buy.SetEnabled(state == SkillPurchaseState.Available);
        if (state == SkillPurchaseState.Owned) buy.text = "구매 완료";
        else if (state == SkillPurchaseState.Locked) buy.text = "선행 스킬 필요";
        else if (state == SkillPurchaseState.InsufficientPoints) buy.text = $"{skill.Cost:N0} P · 포인트 부족";
        else if (state == SkillPurchaseState.Invalid) buy.text = "구매 불가";
        float width = overlay.resolvedStyle.width;
        float height = overlay.resolvedStyle.height;
        float popupWidth = Mathf.Min(320, Mathf.Max(180, width - 24));
        popup.style.width = popupWidth;
        Vector2 anchor = overlay.WorldToLocal(node.worldBound.center);
        popup.style.left = Mathf.Clamp(anchor.x - popupWidth / 2, 12, Mathf.Max(12, width - popupWidth - 12));
        popup.style.top = Mathf.Clamp(anchor.y > height / 2 ? anchor.y - 220 : anchor.y + 26, 60, Mathf.Max(60, height - 210));
        popup.style.display = DisplayStyle.Flex;
        SkillInspected?.Invoke(skill.Id);
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

    void ApplyPan() { board.style.left = pan.x; board.style.top = pan.y; }
    void OnViewportGeometry(GeometryChangedEvent evt)
    {
        if (tutorialSkillToCenter != null) CenterTutorialSkill();
        else if (!centered && evt.newRect.width > 0 && evt.newRect.height > 0)
        {
            pan = new Vector2((evt.newRect.width - board.resolvedStyle.width) / 2,
                (evt.newRect.height - board.resolvedStyle.height) / 2);
            ApplyPan();
            centered = true;
        }
        if (selectedNode != null && popup.style.display.value == DisplayStyle.Flex) Inspect(selectedNode);
        else popup.style.display = DisplayStyle.None;
    }

    // Runtime connections follow positions edited in UI Builder, including diagonal moves.
    void OnNodeGeometry(GeometryChangedEvent evt)
    {
        if (rebirthMode) return;
        foreach (Button node in nodes)
        {
            SkillData skill = Data(node);
            Button parent = skill.IsStart ? null : board.Q<Button>(skill.PrerequisiteId);
            VisualElement edge = board.Q("tree-edge-" + node.name.Substring("skill-".Length));
            if (parent == null || edge == null) continue;
            Vector2 a = board.WorldToLocal(parent.worldBound.center);
            Vector2 b = board.WorldToLocal(node.worldBound.center);
            Vector2 delta = b - a;
            edge.style.left = a.x;
            edge.style.top = a.y - 1;
            edge.style.width = delta.magnitude;
            edge.style.height = 2;
            edge.style.transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(50));
            edge.style.rotate = new Rotate(new Angle(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg));
        }
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
        string id = selectedNode.name;
        if (progress.TryPurchase(table, id) == SkillPurchaseState.Available)
            SkillPurchased?.Invoke(id);
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
            SkillPurchaseState state = progress.GetState(table, node.name);
            node.EnableInClassList("owned", state == SkillPurchaseState.Owned);
            node.EnableInClassList("locked", state == SkillPurchaseState.Locked);
            node.EnableInClassList("affordable", state == SkillPurchaseState.Available);
        }
        if (selectedNode != null && popup.style.display.value == DisplayStyle.Flex) Inspect(selectedNode);
    }

    public void Show()
    {
        Refresh();
        host.style.display = DisplayStyle.Flex;
        popup.style.display = DisplayStyle.None;
        overlay.Focus();
        Opened?.Invoke();
    }
    public void Hide() { StopDrag(); tutorialSkillToCenter = null; host.style.display = DisplayStyle.None; opener?.Focus(); }

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
        foreach (Button node in nodes)
        {
            node.clicked -= clicks[node];
            node.UnregisterCallback<GeometryChangedEvent>(OnNodeGeometry);
        }
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
