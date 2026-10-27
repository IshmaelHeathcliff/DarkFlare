using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFlare
{
    public sealed partial class SkillBoardPanelController
    {
        bool _detailsOpen;
        bool _toolsOpen;
        bool _initialFit;
        int _selectedSlot = -1;
        VisualElement _toolsReturn;
        IVisualElementScheduledItem _focusRequest;

        void StopFocusRequest()
        {
            _focusRequest?.Pause(); _focusRequest = null;
        }

        void FocusControl(VisualElement target)
        {
            StopFocusRequest();
            int requestedFrame = Time.frameCount;
            float deadline = Time.realtimeSinceStartup + 1;
            _focusRequest = _root.schedule.Execute(() =>
            {
                if (_disposed || !IsVisible || _menu.Workspace.ActiveWindow != GameMenuPage.SkillBoard
                    || Time.realtimeSinceStartup > deadline) { StopFocusRequest(); return; }
                if (Time.frameCount <= requestedFrame || !GameMenuController.IsNavigable(target)) { return; }
                StopFocusRequest();
                target.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(target);
                target.Focus();
            }).Every(1);
        }

        public void LeaveFocus()
        {
            StopFocusRequest(); SetTools(false, false);
        }

        void Visible(string name, bool visible)
        {
            _root.Q("board-" + name)?.EnableInClassList("board-hidden", !visible);
        }

        void ShowDetails(bool open)
        {
            if (!open && (_preview != null || _growthPreview != null || _bottle != null || _reviewingLinks)) { return; }
            _detailsOpen = open;
            Visible("details-scroll", open);
            if (_board != null && IsVisible) { RefreshContext(); }
        }

        void RefreshContext()
        {
            bool pending = _preview != null || _growthPreview != null || _bottle != null || _reviewingLinks;
            if (pending) { _detailsOpen = true; }
            Visible("details-scroll", _detailsOpen);
            _root.Q<Button>("board-details-toggle").text = L(_detailsOpen ? "hide_details" : "details");
            _root.Q<Button>("board-details-toggle").SetEnabled(!pending);
            _root.Q<Button>("board-tools").text = L("tools");
            bool center = Canvas.Cursor.Equals(default);
            string attribute = SkillBoardProjection.AttributeAt(Canvas.Cursor);
            bool slot = _selectedSlot >= 0;
            bool bagItem = SelectedBag()?.BaseDefinition.IsSkillBoardItem == true && SelectedBag()?.BaseDefinition.ItemType != ItemType.EnergyBottle;
            Visible("selected-name", !slot && attribute.Length == 0);
            Visible("selected-details", !slot && attribute.Length == 0);
            Visible("take", bagItem && !slot);
            Visible("move", _wood != null && !slot);
            Visible("activate", _wood != null && !slot);
            Visible("extract", Embedding != null && !slot);
            Visible("remove", _wood != null && !slot);
            Visible("links-caption", _multipleLinks);
            Visible("links", _multipleLinks);
            Visible("normal", slot);
            Visible("slot-details", slot);
            Visible("selected-icon", !slot && attribute.Length == 0 && (Embedding?.Item ?? _wood?.Item ?? SelectedBag()) != null);
            Visible("bottle-icon", center && !slot && _board.EnergyBottle != null);
            Visible("bottle-summary", center && !slot);
            Visible("bottle-help", center && !slot);
            Visible("charge-equipped", center && !slot);
            Visible("choose-bottle", center && !slot && SelectedBag()?.BaseDefinition.ItemType == ItemType.EnergyBottle);
            for (int i = 0; i < 3; i++)
            {
                string id = SkillBoardProjection.AttributeAt(SkillBoardProjection.AttributeCell(i));
                Visible("attribute-" + id, id == attribute && !slot);
                Visible("allocate-" + id, id == attribute && !slot);
            }
            for (int i = 0; i < 6; i++)
            { Visible("clear-" + i, _detailsOpen && _selectedSlot == i && _root.Q<Button>("board-clear-" + i).enabledSelf); }
            Visible("preview", _root.Q<Label>("board-preview").text.Length > 0);
            Visible("actions", pending);
        }

        void SelectSlot(int slot)
        {
            if (_menu.Workspace.IsDragging) { return; }
            _selectedSlot = slot;
            ShowDetails(true);
            if (SelectedFlame != null) { Assign(slot); }
            RefreshSlots(); RefreshContext();
        }

        void FocusPrepared()
        {
            FocusControl(_root.Q<Button>(_preview?.Success == true || _growthPreview?.Success == true ? "board-confirm" : "board-cancel"));
        }

        void ClearSlot(int slot)
        {
            this.SendCommand(new AssignPlayerSkillCommand(PlayerId, slot, string.Empty));
            Refresh(); _root.Q<Button>("board-slot-" + slot).Focus();
        }

        void SetTools(bool open, bool restoreFocus = true)
        {
            if (open && _menu.Workspace.IsDragging) { return; }
            StopFocusRequest();
            if (_toolsOpen == open) { return; }
            _toolsOpen = open;
            Visible("tools-popup", open);
            if (open)
            {
                _toolsReturn = _root.panel?.focusController.focusedElement as VisualElement;
                FocusControl(_root.Q<Button>("board-center"));
            }
            else if (restoreFocus)
            {
                if (_toolsReturn != null && GameMenuController.IsNavigable(_toolsReturn)) { _toolsReturn.Focus(); }
                else { Canvas.Focus(); }
            }
            if (!open) { _toolsReturn = null; }
        }

        void ViewAction(Action action)
        {
            SetTools(false, false); action(); Canvas.Focus();
        }

        void NavigateTools(Vector2 direction)
        {
            VisualElement popup = _root.Q("board-tools-popup");
            VisualElement focused = _root.panel?.focusController.focusedElement as VisualElement;
            var candidates = new List<Button>();
            var rectangles = new List<Rect>();
            foreach (Button button in popup.Query<Button>().ToList())
            {
                if (button == focused || !GameMenuController.IsNavigable(button)) { continue; }
                candidates.Add(button); rectangles.Add(button.worldBound);
            }
            int index = focused == null || !popup.Contains(focused) ? 0 : SpatialNavigation.FindNeighbor(focused.worldBound, rectangles, direction);
            if (index >= 0 && index < candidates.Count) { candidates[index].Focus(); }
        }

        void OnPanelPointerDown(PointerDownEvent evt)
        {
            if (!_toolsOpen || evt.target is not VisualElement target || _root.Q("board-tools-popup").Contains(target)
                || target == _root.Q("board-tools")) { return; }
            SetTools(false); evt.StopImmediatePropagation();
        }
    }
}
