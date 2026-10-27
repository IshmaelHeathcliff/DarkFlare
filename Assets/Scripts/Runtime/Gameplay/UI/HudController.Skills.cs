using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFlare
{
    public partial class HudController
    {
        sealed class SkillView
        {
            internal int Index { get; set; }
            internal VisualElement Card { get; set; }
            internal VisualElement Icon { get; set; }
            internal ProgressBar Cooldown { get; set; }
            internal Label Time { get; set; }
            internal Label State { get; set; }
            internal PlayerSkillState Source { get; set; }
            internal AttributeRuntimeSnapshot Runtime { get; set; }
        }

        readonly SkillView[] _skills = new SkillView[7];
        readonly List<Button> _entries = new List<Button>();
        VisualElement _details;
        VisualElement _detailAnchor;
        Label _detailStatus;
        IVisualElementScheduledItem _hideDetails;
        int _inspected = -1;
        bool _overDetails;

        void BindSkillDetails(VisualElement root)
        {
            root.RegisterCallback<DetachFromPanelEvent>(OnHudDetached);
            _details = root.Q("hud-skill-tooltip");
            _detailStatus = root.Q<Label>("hud-detail-status");
            _details.RegisterCallback<PointerEnterEvent>(OnDetailsEnter);
            _details.RegisterCallback<PointerLeaveEvent>(OnDetailsLeave);
            _details.RegisterCallback<GeometryChangedEvent>(OnDetailsGeometry);
            root.Q("hud-entries").Query<Button>().ForEach(button =>
            {
                _entries.Add(button);
                button.RegisterCallback<PointerEnterEvent>(OnEntryEnter);
                button.RegisterCallback<PointerLeaveEvent>(OnEntryLeave);
            });
            for (int i = 0; i < _skills.Length; i++)
            {
                string prefix = i == 0 ? "hud-normal" : "hud-flame-" + (i - 1);
                var view = new SkillView
                {
                    Index = i,
                    Card = root.Q(prefix),
                    Icon = root.Q(i == 0 ? "hud-normal-icon" : "hud-flame-icon-" + (i - 1)),
                    Cooldown = root.Q<ProgressBar>(i == 0 ? "skill-cooldown" : "hud-flame-cooldown-" + (i - 1)),
                    Time = root.Q<Label>(prefix + "-time"),
                    State = root.Q<Label>(i == 0 ? "skill-status-label" : "hud-flame-state-" + (i - 1))
                };
                _skills[i] = view;
                view.Card.userData = view;
                view.Card.RegisterCallback<PointerEnterEvent>(OnSkillEnter);
                view.Card.RegisterCallback<PointerLeaveEvent>(OnSkillLeave);
            }
        }

        void RefreshSkills()
        {
            bool interactive = _input.IsGameplayEnabled;
            _uiPanel.Root.Q("hud-flames").EnableInClassList("hud-flames--hidden", !interactive);
            if (!interactive) { CloseDetails(); }
            IReadOnlyList<PlayerSkillState> slots = this.SendQuery(new GetPlayerSkillsQuery());
            ApplySkill(_skills[0], this.SendQuery(new GetPlayerNormalSkillQuery()), RuntimeSnapshot);
            for (int i = 0; i < slots.Count; i++) { ApplySkill(_skills[i + 1], slots[i], this.SendQuery(new GetPlayerSkillRuntimeQuery(i))); }
            if (_inspected >= 0) { RefreshDetails(); }
        }

        void ApplySkill(SkillView view, PlayerSkillState source, AttributeRuntimeSnapshot runtime)
        {
            if (_inspected == view.Index && (view.Source.SourceId != source.SourceId || view.Source.Skill != source.Skill)) { CloseDetails(); }
            view.Source = source;
            view.Runtime = runtime;
            view.Cooldown.value = runtime.CooldownNormalized;
            view.Time.text = runtime.RemainingSeconds > 0 ? N(runtime.RemainingSeconds) : string.Empty;
            bool empty = view.Index > 0 && source.Skill == null;
            string state = empty ? "empty" : runtime.SkillState;
            view.State.text = state == "ready" || state == "cooldown" ? string.Empty : L("hud.state." + state);
            view.Card.EnableInClassList("hud-skill--unavailable", state != "ready" && state != "cooldown");
            view.Card.EnableInClassList("hud-skill--empty", empty);
            Sprite icon = string.IsNullOrEmpty(source.IconGuid) ? null : this.GetUtility<SpriteAssetLoader>().GetSprite(source.IconGuid);
            view.Icon.EnableInClassList("hud-skill-icon--missing", !empty && icon == null);
            view.Icon.style.backgroundImage = icon != null ? new StyleBackground(icon) : new StyleBackground(StyleKeyword.Null);
        }

        void OnSkillEnter(PointerEnterEvent evt)
        {
            if (!_input.IsGameplayEnabled || (evt.currentTarget as VisualElement)?.userData is not SkillView view) { return; }
            _hideDetails?.Pause();
            _input.ConsumeGameplayPointer();
            _inspected = view.Index;
            _detailAnchor = view.Card;
            _details.style.display = DisplayStyle.Flex;
            RefreshDetails();
            PositionDetails();
        }

        void OnSkillLeave(PointerLeaveEvent evt) { ScheduleDetailsClose(); }
        void OnHudDetached(DetachFromPanelEvent evt) { CloseDetails(); }
        void OnEntryEnter(PointerEnterEvent evt)
        {
            if (!_input.IsGameplayEnabled || evt.currentTarget is not Button button) { return; }
            _hideDetails?.Pause();
            _input.ConsumeGameplayPointer();
            _inspected = -1;
            _detailAnchor = button;
            _skillName.text = button.tooltip;
            _skillDetails.text = string.Empty;
            _detailStatus.text = string.Empty;
            _details.style.display = DisplayStyle.Flex;
            PositionDetails();
        }
        void OnEntryLeave(PointerLeaveEvent evt) { ScheduleDetailsClose(); }
        void OnDetailsEnter(PointerEnterEvent evt) { _overDetails = true; _hideDetails?.Pause(); _input.ConsumeGameplayPointer(); }
        void OnDetailsLeave(PointerLeaveEvent evt) { _overDetails = false; ScheduleDetailsClose(); }
        void OnDetailsGeometry(GeometryChangedEvent evt) { PositionDetails(); }

        void ScheduleDetailsClose()
        {
            _hideDetails?.Pause();
            // 留出跨过卡片与说明间隙的时间；调度依附当前视觉树，解绑时明确取消。
            _hideDetails = _details.schedule.Execute(() => { if (!_overDetails) { CloseDetails(); } }).StartingIn(120);
        }

        void RefreshDetails()
        {
            SkillView view = _skills[_inspected];
            AttributeRuntimeSnapshot state = view.Runtime;
            _skillName.text = view.Source.Skill == null ? L(view.Index == 0 ? "attributes.state.no_source" : "hud.skill.empty") : L("attributes.skill." + view.Source.Skill.Id);
            _skillDetails.text = L("hud.combat.details", N(state.ManaCost), N(state.Interval), N(state.RemainingSeconds));
            _detailStatus.text = L("attributes.state." + state.SkillState);
        }

        void PositionDetails()
        {
            if (_detailAnchor == null || _details.panel == null) { return; }
            Rect viewport = _pointerRoot.worldBound;
            Rect anchor = _detailAnchor.worldBound;
            float width = float.IsNaN(_details.resolvedStyle.width) ? 360 : _details.resolvedStyle.width;
            float height = float.IsNaN(_details.resolvedStyle.height) ? 120 : _details.resolvedStyle.height;
            Vector2 world = new Vector2(Mathf.Clamp(anchor.center.x - width * .5f, viewport.xMin + 8, Mathf.Max(viewport.xMin + 8, viewport.xMax - width - 8)),
                Mathf.Max(viewport.yMin + 8, _uiPanel.Root.Q("hud-root").worldBound.yMin - height - 10));
            Vector2 local = _details.parent.WorldToLocal(world);
            _details.style.left = local.x;
            _details.style.top = local.y;
        }

        void CloseDetails()
        {
            _hideDetails?.Pause();
            _inspected = -1;
            _detailAnchor = null;
            _overDetails = false;
            if (_details != null) { _details.style.display = DisplayStyle.None; }
        }

        void RefreshEntryNames()
        {
            string[] names = { "inventory", "attributes", "board", "pause" };
            string[] keys = { "menu.tab.inventory", "attributes.title", "board.title", "window.pause" };
            for (int i = 0; i < names.Length; i++)
            {
                Button button = _uiPanel.Root.Q<Button>("game-hud-" + names[i]);
                string binding = i == 0 ? ApplicationHost.Current.Input.GetGlyphToken(RebindableInputAction.PlayerToggleMenu).FallbackText
                    : i == 3 ? ApplicationHost.Current.Input.GetGlyphToken(RebindableInputAction.PlayerPause).FallbackText : string.Empty;
                button.tooltip = L(keys[i]) + (string.IsNullOrEmpty(binding) ? string.Empty : " · " + binding);
                if (_detailAnchor == button) { _skillName.text = button.tooltip; }
            }
        }

        static bool IsPointerInteraction(VisualElement hit)
        {
            for (VisualElement element = hit; element != null; element = element.parent)
            {
                if (element is Button || element.ClassListContains("hud-pointer-interaction")) { return true; }
            }
            return false;
        }

        void UnbindSkillDetails()
        {
            _pointerRoot?.UnregisterCallback<DetachFromPanelEvent>(OnHudDetached);
            CloseDetails();
            _hideDetails = null;
            foreach (Button button in _entries)
            {
                button.UnregisterCallback<PointerEnterEvent>(OnEntryEnter);
                button.UnregisterCallback<PointerLeaveEvent>(OnEntryLeave);
            }
            _entries.Clear();
            foreach (SkillView view in _skills)
            {
                if (view == null) { continue; }
                view.Card.UnregisterCallback<PointerEnterEvent>(OnSkillEnter);
                view.Card.UnregisterCallback<PointerLeaveEvent>(OnSkillLeave);
                view.Card.userData = null;
                view.Icon.style.backgroundImage = StyleKeyword.Null;
            }
            Array.Clear(_skills, 0, _skills.Length);
            _details?.UnregisterCallback<PointerEnterEvent>(OnDetailsEnter);
            _details?.UnregisterCallback<PointerLeaveEvent>(OnDetailsLeave);
            _details?.UnregisterCallback<GeometryChangedEvent>(OnDetailsGeometry);
            _details = null;
            _detailStatus = null;
        }
    }
}
