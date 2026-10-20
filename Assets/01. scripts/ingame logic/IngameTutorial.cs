using System;
using UnityEngine;
using UnityEngine.UIElements;

// Four input-blocking shades leave the actual UI visible and clickable in the spotlight.
public sealed class IngameTutorial : IDisposable
{
    const string FirstSkillId = "skill-9";
    enum Step { Grade, Timer, Assignments, Playing, OpenTree, SelectSkill, BuySkill }
    readonly VisualElement root, layer, highlight, shield, panel;
    readonly VisualElement[] shades = new VisualElement[4];
    readonly Label title, body;
    readonly Button next;
    readonly SkillTreeUI tree;
    readonly SkillProgress progress;
    readonly SkillTable table;
    readonly Action startPlaying;
    readonly Func<VisualElement> prepareAssignment;
    readonly IVisualElementScheduledItem layoutTask;
    VisualElement target;
    VisualElement appearingAssignment;
    VisualElement resultPanel;
    StyleTranslate originalResultTranslate;
    float resultPanelShift;
    double animationStartedAt, fadeStartedAt;
    float fadeFromOpacity;
    bool fadingOut, finishAfterFade;
    Step step;
    bool disposed;

    public IngameTutorial(VisualElement uiRoot, SkillTreeUI tree,
        SkillProgress progress, SkillTable table, Func<VisualElement> prepareAssignment, Action startPlaying)
    {
        root = uiRoot.Q("game-root") ?? uiRoot;
        this.tree = tree;
        this.progress = progress;
        this.table = table;
        this.startPlaying = startPlaying;
        this.prepareAssignment = prepareAssignment;
        layer = new VisualElement { name = "tutorial-overlay", pickingMode = PickingMode.Ignore };
        layer.AddToClassList("tutorial-overlay");
        for (int i = 0; i < shades.Length; i++)
        {
            shades[i] = new VisualElement();
            shades[i].AddToClassList("tutorial-shade");
            layer.Add(shades[i]);
        }
        highlight = new VisualElement { pickingMode = PickingMode.Ignore };
        highlight.AddToClassList("tutorial-highlight");
        layer.Add(highlight);
        shield = new VisualElement();
        shield.style.position = Position.Absolute;
        layer.Add(shield);
        panel = new VisualElement();
        panel.AddToClassList("tutorial-panel");
        title = new Label();
        title.AddToClassList("tutorial-title");
        body = new Label();
        body.AddToClassList("tutorial-body");
        next = new Button(Advance) { text = "다음 →" };
        next.AddToClassList("tutorial-next");
        panel.Add(title);
        panel.Add(body);
        panel.Add(next);
        layer.Add(panel);
        root.Add(layer);
        root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        root.RegisterCallback<FocusInEvent>(OnFocusIn, TrickleDown.TrickleDown);
        if (tree != null)
        {
            tree.Opened += OnTreeOpened;
            tree.SkillInspected += OnSkillInspected;
            tree.SkillPurchased += OnSkillPurchased;
        }
        layoutTask = layer.schedule.Execute(UpdateLayout).Every(16);
        ShowStep();
    }

    void ShowStep()
    {
        if (step != Step.OpenTree) RestoreResultPanel();
        if (step == Step.Playing) { FadeOut(false); return; }
        next.style.display = step < Step.Assignments ? DisplayStyle.Flex : DisplayStyle.None;
        shield.style.display = step < Step.Assignments ? DisplayStyle.Flex : DisplayStyle.None;
        if (step == Step.Grade)
        {
            target = root.Q("grade-panel");
            title.text = "학점을 올려요";
            body.text = "과제를 끝내면 F → D → C → B → A 순서로 학점이 올라가요.";
        }
        else if (step == Step.Timer)
        {
            target = root.Q("timer-panel");
            title.text = "시간 안에 끝내세요";
            body.text = "남은 시간이 0이 되면 이번 라운드가 끝나요.";
        }
        else if (step == Step.Assignments)
        {
            target = prepareAssignment?.Invoke();
            if (target == null) { OnAssignmentClicked(); return; }
            title.text = "눌러보세요";
            body.text = "과제를 클릭해 체력을 줄이면 포인트(P)를 얻어요.";
        }
        else if (step == Step.OpenTree)
        {
            target = root.Q("skill-tree-button");
            title.text = "스킬 트리를 눌러보세요";
            body.text = "여기서 모은 P로 스킬을 강화할 수 있어요.";
        }
        else if (step == Step.SelectSkill)
        {
            title.text = "스킬을 눌러보세요";
            body.text = "‘샤프심 교체’를 눌러 효과를 확인하세요.";
            target = root.Q(FirstSkillId);
            tree.FocusTutorialSkill(FirstSkillId);
        }
        else if (step == Step.BuySkill)
        {
            target = root.Q("skill-description");
            title.text = "스킬을 찍어보세요";
            body.text = "구매 버튼을 누르면 클릭 파워가 +1 올라가요.";
        }
        AnimateIn();
        UpdateLayout();
        FocusAllowed();
    }

    public void OnRoundEnded()
    {
        if (disposed || step != Step.Playing) return;
        SkillData skill = table != null ? table.Find(FirstSkillId) : null;
        if (tree == null || skill == null || progress.IsOwned(skill))
        {
            Finish();
            return;
        }
        // Ensure a first round with few completed assignments can still teach purchasing.
        progress.GrantTutorialReward(skill.Cost);
        step = Step.OpenTree;
        ShowStep();
    }

    void Advance()
    {
        if (disposed) return;
        if (step < Step.Assignments) { step++; ShowStep(); }
    }

    public void OnAssignmentClicked()
    {
        if (disposed || step != Step.Assignments) return;
        RestoreAssignment();
        step = Step.Playing;
        ShowStep();
        startPlaying?.Invoke();
    }

    void OnTreeOpened()
    {
        if (disposed || step != Step.OpenTree) return;
        step = Step.SelectSkill;
        ShowStep();
    }

    void OnSkillInspected(string id)
    {
        if (step != Step.SelectSkill || id != FirstSkillId) return;
        step = Step.BuySkill;
        ShowStep();
    }

    void OnSkillPurchased(string id)
    {
        if (disposed || finishAfterFade || step != Step.BuySkill || id != FirstSkillId) return;
        Finish();
    }

    void Finish()
    {
        progress.CompleteTutorial();
        FadeOut(true);
    }

    void AnimateIn()
    {
        fadingOut = false;
        animationStartedAt = Time.unscaledTimeAsDouble;
        layer.style.display = DisplayStyle.Flex;
        layer.style.opacity = 0f;
        panel.style.opacity = 0f;
        panel.style.translate = new Translate(0, 14);
        foreach (VisualElement shade in shades) shade.pickingMode = PickingMode.Position;
        shield.pickingMode = panel.pickingMode = next.pickingMode = PickingMode.Position;
        if (step == Step.Assignments)
        {
            appearingAssignment = target;
            appearingAssignment.style.opacity = 0f;
            appearingAssignment.style.translate = new Translate(0, 10);
        }
    }

    void FadeOut(bool finish)
    {
        fadingOut = true;
        finishAfterFade = finish;
        fadeStartedAt = Time.unscaledTimeAsDouble;
        fadeFromOpacity = float.IsNaN(layer.resolvedStyle.opacity) ? 1f : layer.resolvedStyle.opacity;
        // Release input immediately while the visual guide fades away.
        layer.Query<VisualElement>().ForEach(element => element.pickingMode = PickingMode.Ignore);
        next.style.display = DisplayStyle.None;
    }

    static float EaseOut(float value)
    {
        float t = 1f - Mathf.Clamp01(value);
        return 1f - t * t * t;
    }

    void Animate()
    {
        double now = Time.unscaledTimeAsDouble;
        if (fadingOut)
        {
            float t = Mathf.Clamp01((float)(now - fadeStartedAt) / 0.22f);
            layer.style.opacity = fadeFromOpacity * (1f - EaseOut(t));
            if (t >= 1f)
            {
                fadingOut = false;
                layer.style.display = DisplayStyle.None;
                if (finishAfterFade) Dispose();
            }
            return;
        }
        if (step == Step.Playing) return;
        float elapsed = (float)(now - animationStartedAt);
        layer.style.opacity = EaseOut(elapsed / 0.35f);
        float panelT = EaseOut((elapsed - 0.06f) / 0.4f);
        panel.style.opacity = panelT;
        panel.style.translate = new Translate(0, 14f * (1f - panelT));
        highlight.style.opacity = 0.78f + 0.22f * Mathf.Sin(elapsed * Mathf.PI * 1.5f);
        if (appearingAssignment != null)
        {
            float cardT = EaseOut(elapsed / 0.55f);
            appearingAssignment.style.opacity = cardT;
            appearingAssignment.style.translate = new Translate(0, 10f * (1f - cardT));
            if (cardT >= 1f) RestoreAssignment();
        }
    }

    void RestoreAssignment()
    {
        if (appearingAssignment == null) return;
        appearingAssignment.style.opacity = StyleKeyword.Null;
        appearingAssignment.style.translate = StyleKeyword.Null;
        appearingAssignment = null;
    }

    bool Allowed(VisualElement element)
        => element != null && (element == panel || panel.Contains(element) ||
            (step >= Step.Assignments && target != null && (element == target || target.Contains(element))));

    void FocusAllowed()
    {
        if (step < Step.Assignments) next.Focus();
        else if (step == Step.BuySkill) root.Q<Button>("tree-buy")?.Focus();
        else target?.Focus();
    }

    void OnFocusIn(FocusInEvent evt)
    {
        if (!fadingOut && step != Step.Playing && !Allowed(evt.target as VisualElement)) FocusAllowed();
    }

    void OnKeyDown(KeyDownEvent evt)
    {
        // Keep keyboard navigation and Escape from reaching the covered controls.
        if (fadingOut || step == Step.Playing) return;
        if (!Allowed(evt.target as VisualElement) || evt.keyCode == KeyCode.Escape || evt.keyCode == KeyCode.Tab)
        {
            evt.StopImmediatePropagation();
            root.focusController?.IgnoreEvent(evt);
            FocusAllowed();
        }
    }

    static void Place(VisualElement element, float x, float y, float width, float height)
    {
        element.style.left = x;
        element.style.top = y;
        element.style.width = Mathf.Max(0, width);
        element.style.height = Mathf.Max(0, height);
    }

    void UpdateLayout()
    {
        if (disposed) return;
        Animate();
        if (disposed || fadingOut || finishAfterFade || step == Step.Playing || target == null) return;
        float width = root.resolvedStyle.width, height = root.resolvedStyle.height;
        if (float.IsNaN(width) || float.IsNaN(height) || width <= 0 || height <= 0) return;
        Vector2 min = root.WorldToLocal(target.worldBound.min);
        Vector2 max = root.WorldToLocal(target.worldBound.max);
        if (float.IsNaN(min.x) || float.IsNaN(min.y) || float.IsNaN(max.x) || float.IsNaN(max.y)) return;
        float left = Mathf.Clamp(min.x - 8, 0, width), right = Mathf.Clamp(max.x + 8, left, width);
        float top = Mathf.Clamp(min.y - 8, 0, height), bottom = Mathf.Clamp(max.y + 8, top, height);
        Place(shades[0], 0, 0, width, top);
        Place(shades[1], 0, bottom, width, height - bottom);
        Place(shades[2], 0, top, left, bottom - top);
        Place(shades[3], right, top, width - right, bottom - top);
        Place(highlight, left, top, right - left, bottom - top);
        Place(shield, left, top, right - left, bottom - top);
        float panelWidth = Mathf.Min(340, width - 24);
        panel.style.width = panelWidth;
        panel.style.left = (width - panelWidth) / 2;
        float panelHeight = panel.resolvedStyle.height;
        if (float.IsNaN(panelHeight)) panelHeight = 100;
        // Use the larger free space above or below the spotlight.
        float panelTop = height - bottom >= top ? bottom + 12 : top - panelHeight - 12;
        if (step == Step.OpenTree)
        {
            // Make room below the button on short windows instead of covering the result controls.
            if (resultPanel == null)
            {
                resultPanel = root.Q(className: "result-panel");
                if (resultPanel != null) originalResultTranslate = resultPanel.style.translate;
            }
            if (resultPanel != null)
            {
                float originalTop = root.WorldToLocal(resultPanel.worldBound.min).y + resultPanelShift;
                float requiredShift = Mathf.Max(0, bottom + resultPanelShift + 12 - (height - panelHeight - 12));
                float shift = Mathf.Min(requiredShift, Mathf.Max(0, originalTop - 12));
                Vector2 origin = root.WorldToLocal(resultPanel.LocalToWorld(Vector2.zero));
                float scale = Mathf.Abs(root.WorldToLocal(resultPanel.LocalToWorld(Vector2.up)).y - origin.y);
                if (scale > 0 && Mathf.Abs(shift - resultPanelShift) > 0.5f)
                {
                    resultPanel.style.translate = new Translate(0, -shift / scale);
                    resultPanelShift = shift;
                }
            }
            panelTop = bottom + 12;
        }
        panel.style.top = Mathf.Clamp(panelTop, 12, Mathf.Max(12, height - panelHeight - 12));
    }

    void RestoreResultPanel()
    {
        if (resultPanel != null) resultPanel.style.translate = originalResultTranslate;
        resultPanel = null;
        resultPanelShift = 0;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        RestoreResultPanel();
        RestoreAssignment();
        layoutTask?.Pause();
        root.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        root.UnregisterCallback<FocusInEvent>(OnFocusIn, TrickleDown.TrickleDown);
        if (tree != null)
        {
            tree.Opened -= OnTreeOpened;
            tree.SkillInspected -= OnSkillInspected;
            tree.SkillPurchased -= OnSkillPurchased;
        }
        layer.RemoveFromHierarchy();
    }
}
