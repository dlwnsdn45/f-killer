using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// UXML owns layout, SkillTable owns definitions, SkillProgress owns purchases.
public sealed class SkillTreeUI : IDisposable
{
    readonly VisualElement host, overlay, viewport, board, popup;
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

    public SkillTreeUI(VisualElement root, SkillTable table, SkillProgress progress)
    {
        this.table = table;
        this.progress = progress;
        host = root.Q("skill-tree-host");
        overlay = host.Q("skill-tree-overlay");
        viewport = host.Q("tree-viewport");
        board = host.Q("skill-tree-board");
        popup = host.Q("skill-description");
        points = host.Q<Label>("tree-points");
        heading = host.Q<Label>("tree-detail-title");
        description = host.Q<Label>("tree-detail-effect");
        requirement = host.Q<Label>("tree-detail-requirement");
        buy = host.Q<Button>("tree-buy");
        close = host.Q<Button>("tree-close");
        opener = root.Q<Button>("skill-tree-button");
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
    }

    void OnPointerDown(PointerDownEvent evt)
    {
        if (evt.button != 2 || dragPointer >= 0) return;
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
        if ((evt.pressedButtons & 4) == 0) { StopDrag(); return; }
        pan = panStart + (Vector2)evt.position - dragStart;
        ApplyPan();
        evt.StopPropagation();
    }

    void OnPointerUp(PointerUpEvent evt)
    {
        if (evt.pointerId != dragPointer || evt.button != 2) return;
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
        if (!centered && evt.newRect.width > 0 && evt.newRect.height > 0)
        {
            pan = new Vector2((evt.newRect.width - board.resolvedStyle.width) / 2,
                (evt.newRect.height - board.resolvedStyle.height) / 2);
            ApplyPan();
            centered = true;
        }
        popup.style.display = DisplayStyle.None;
    }

    // Runtime connections follow positions edited in UI Builder, including diagonal moves.
    void OnNodeGeometry(GeometryChangedEvent evt)
    {
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
        if (selectedNode == null) return;
        progress.TryPurchase(table, selectedNode.name);
        Refresh();
    }

    void Refresh()
    {
        points.text = $"{progress.Points:N0} P";
        foreach (Button node in nodes)
        {
            SkillPurchaseState state = progress.GetState(table, node.name);
            node.EnableInClassList("owned", state == SkillPurchaseState.Owned);
            node.EnableInClassList("locked", state == SkillPurchaseState.Locked);
            node.EnableInClassList("affordable", state == SkillPurchaseState.Available);
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

