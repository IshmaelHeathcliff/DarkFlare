using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFlare
{
    public sealed partial class SkillBoardPanelController
    {
        bool _multipleLinks;
        bool _reviewingLinks;

        void TakeBag()
        {
            ReadState();
            ItemInstance item = SelectedBag();
            if (item != null) { _menu.Workspace.Interactions.PickForBoard(item, _root.Q<Button>("board-take")); }
        }
        void TakeMounted()
        {
            if (_wood != null && !_menu.Workspace.IsDragging)
            { _menu.Workspace.Interactions.PickForBoard(_wood.Item, _root.Q<Button>("board-move")); }
        }
        void TakeMountedPointer(Vector2 position, int pointer)
        {
            if (_wood != null && !_menu.Workspace.IsDragging)
            { _menu.Workspace.Interactions.PickForBoard(_wood.Item, _root.Q<Button>("board-move"), position, pointer); }
        }
        public bool TakeCanvas()
        {
            if (!IsVisible || Canvas.panel?.focusController.focusedElement != Canvas || _wood == null) { return false; }
            TakeMounted(); return true;
        }

        public void PointDrop(ItemInstance item, Vector2 world)
        {
            if (!IsVisible) { return; }
            BeginPlacement(item);
            Canvas.SelectAt(Canvas.WorldToLocal(world));
        }

        void BeginPlacement(ItemInstance item)
        {
            if (_moving == item) { return; }
            ResetOperation(); ReadState(); _moving = item;
            _rotation = _board.Firewoods.FirstOrDefault(w => w.Item == item)?.Placement.Rotation ?? 0;
            _lastRotation = -1;
        }

        public string ValidateDrop(ItemInstance item)
        {
            BeginPlacement(item); ReadState(); SelectNode(); UpdatePlacement();
            return _multipleLinks || _preview?.Success == true ? null : "board.error." + (_preview?.Failure ?? SkillBoardFailure.InvalidPlacement);
        }

        void UpdatePlacement()
        {
            if (_moving == null) { return; }
            SkillBoardRequest request;
            try
            {
                if (_moving.BaseDefinition.ItemType == ItemType.Firewood)
                {
                    InstalledFirewood mounted = _board.Firewoods.FirstOrDefault(w => w.Item == _moving);
                    var woods = _board.Firewoods.Where(w => w.Item != _moving).Append(new InstalledFirewood(_moving, Canvas.Cursor, _rotation)).ToArray();
                    FirewoodConnection[] candidates = SkillBoardPlacementValidator.FindConnections(woods.Select(w => w.Placement).ToArray())
                        .Where(c => c.First.PlacementId == _moving.InstanceId || c.Second.PlacementId == _moving.InstanceId).ToArray();
                    _multipleLinks = candidates.Length > 1;
                    if (_lastRotation != _rotation || !_lastAnchor.Equals(Canvas.Cursor))
                    {
                        _lastAnchor = Canvas.Cursor; _lastRotation = _rotation;
                        _chosen.Clear();
                        if (candidates.Length == 1) { _chosen.Add(candidates[0]); }
                        VisualElement links = _root.Q("board-links"); links.Clear();
                        foreach (FirewoodConnection connection in candidates)
                        {
                            FirewoodConnection pair = connection;
                            InstalledFirewood a = woods.First(w => w.Item.InstanceId == pair.First.PlacementId);
                            InstalledFirewood b = woods.First(w => w.Item.InstanceId == pair.Second.PlacementId);
                            var toggle = new Toggle(L("link", Name(a.Item) + " " + a.Placement.Anchor, CrackNumber(a, pair.First.NodeId), Name(b.Item) + " " + b.Placement.Anchor, CrackNumber(b, pair.Second.NodeId)));
                            toggle.AddToClassList("board-link"); toggle.SetValueWithoutNotify(_chosen.Contains(pair));
                            toggle.RegisterValueChangedCallback(evt => { if (evt.newValue) { _chosen.Add(pair); } else { _chosen.Remove(pair); } UpdatePlacement(); RefreshDetails(); Draw(); });
                            links.Add(toggle);
                        }
                    }
                    FirewoodConnection[] connections = _board.Connections.Where(c => c.First.PlacementId != _moving.InstanceId && c.Second.PlacementId != _moving.InstanceId).Concat(_chosen).ToArray();
                    request = new SkillBoardRequest(mounted == null ? SkillBoardOperation.Install : SkillBoardOperation.Move,
                        _moving, Canvas.Cursor, _rotation, connections: connections);
                }
                else if (_moving.BaseDefinition.ItemType == ItemType.EnergyBottle)
                {
                    _bottle = this.SendQuery(new SelectEnergyBottleQuery(PlayerId, _moving));
                    _preview = Canvas.Cursor.Equals(default) ? this.SendQuery(new PreviewEnergyChargeQuery(_bottle))
                        : new SkillBoardPreview("瓶子需放在中心", SkillBoardFailure.InvalidPlacement);
                    _charging = true; DescribeCharge(); RefreshDetails(); Draw(); return;
                }
                else
                {
                    request = new SkillBoardRequest(SkillBoardOperation.Embed, _moving,
                        slot: new FirewoodPortReference(_wood?.Item.InstanceId ?? string.Empty, _node));
                }
                _preview = this.SendQuery(new PreviewSkillBoardQuery(PlayerId, request));
                _feedback = _preview.Failure;
                Text("preview", L("preview", _preview.Success ? L("ready") + " · " + L("rotated", _rotation + 1) : L("error." + _feedback)));
                RefreshDetails(); Draw();
            }
            catch (OverflowException) { _preview = null; _feedback = SkillBoardFailure.Overflow; Text("preview", L("error.Overflow")); }
        }

        static int CrackNumber(InstalledFirewood wood, string id)
        {
            return wood.Placement.Layout.Nodes.Where(n => n.Kind == FirewoodNodeKind.Crack).Select(n => n.Id).ToList().IndexOf(id) + 1;
        }

        public bool CommitDrop(ItemInstance item)
        {
            if (!IsVisible || _moving != item) { return false; }
            if (_multipleLinks) { _reviewingLinks = true; RefreshDetails(); return true; }
            if (_preview?.Success != true) { return false; }
            if (_charging) { _moving = null; RefreshDetails(); _root.Q<Button>("board-confirm").Focus(); return true; }
            return CommitPrepared();
        }

        public void FinishDrop()
        {
            if (_reviewingLinks)
            {
                Toggle first = _root.Q("board-links").Q<Toggle>();
                first?.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(first); first?.Focus();
                RefreshDetails(); Draw(); return;
            }
            _moving = null;
            if (!_charging) { _preview = null; _root.Q("board-links").Clear(); }
            if (IsVisible) { RefreshDetails(); Draw(); }
        }

        public bool IsReviewingLinks => _reviewingLinks;

        public bool Rotate(int step)
        {
            if (!IsVisible || !HasPlacement) { return false; }
            _rotation = (_rotation + step + 6) % 6; UpdatePlacement(); return true;
        }

        void Prepare(SkillBoardRequest request)
        {
            ResetOperation(); ReadState();
            _preview = this.SendQuery(new PreviewSkillBoardQuery(PlayerId, request));
            _feedback = _preview.Failure;
            Text("preview", L("preview", L(_preview.Success ? "ready" : "error." + _feedback)));
            RefreshDetails();
        }

        void Extract()
        {
            if (_wood == null) { return; }
            Prepare(new SkillBoardRequest(SkillBoardOperation.Extract, slot: new FirewoodPortReference(_wood.Item.InstanceId, _node)));
        }
        void Remove()
        {
            if (_wood == null) { return; }
            ItemInstance item = _wood.Item;
            Prepare(new SkillBoardRequest(SkillBoardOperation.Remove, item));
            if (_preview.Success)
            {
                Text("preview", L("removal", _board.Connections.Count(c => c.First.PlacementId == item.InstanceId || c.Second.PlacementId == item.InstanceId),
                    _preview.GrowthResult.TalentPoints - _growth.TalentPoints));
            }
        }

        void Activate()
        {
            if (_wood == null) { return; }
            string wood = _wood.Item.InstanceId;
            string node = _wood.Placement.Layout.Nodes.Any(n => n.Id == _node && n.Kind == FirewoodNodeKind.Talent) ? _node : string.Empty;
            ResetOperation();
            _growthPreview = this.SendQuery(new PreviewPlayerGrowthQuery(PlayerId, woodId: wood, nodeId: node));
            Text("preview", _growthPreview.Success ? L("activation", _growth.TalentPoints - _growthPreview.Result.TalentPoints, _growthPreview.Result.TalentPoints)
                : L("error." + _growthPreview.Failure));
            RefreshDetails();
        }
        void Allocate(string stat)
        {
            ResetOperation(); ReadState();
            _growthPreview = this.SendQuery(new PreviewPlayerGrowthQuery(PlayerId, stat));
            Text("preview", _growthPreview.Success ? L("allocation", _growthPreview.Result.AttributePoints) : L("error." + _growthPreview.Failure));
            RefreshDetails();
        }

        void SelectBottle(ItemInstance item)
        {
            ResetOperation(); ReadState();
            _bottle = this.SendQuery(new SelectEnergyBottleQuery(PlayerId, item));
            _preview = this.SendQuery(new PreviewEnergyChargeQuery(_bottle)); _charging = true;
            DescribeCharge(); RefreshDetails();
        }
        void DescribeCharge()
        {
            Text("preview", _preview?.Success == true ? L("charge", _bottle.Item.SkillBoardState.Energy,
                _preview.GrowthResult.EarnedPoints - _growth.EarnedPoints, _preview.GrowthResult.Energy,
                _preview.GrowthResult.Rules.NextCost(_preview.GrowthResult.EarnedPoints)) : L("error." + (_preview?.Failure ?? SkillBoardFailure.Stale)));
        }

        void Confirm()
        {
            if (_menu.Workspace.IsDragging) { return; }
            CommitPrepared();
        }
        bool CommitPrepared()
        {
            bool success = false;
            _executing = true;
            try
            {
                if (_growthPreview?.Success == true) { success = this.SendCommand(new CommitPlayerGrowthCommand(_growthPreview)); }
                else if (_preview?.Success == true) { success = this.SendCommand(new CommitSkillBoardCommand(_preview)); }
            }
            finally
            {
                _executing = false; ResetOperation(); ReadState(); SelectNode(); Refresh();
            }
            Text("preview", success ? L("success") : L("error.Stale"));
            return success;
        }

        void Assign(int slot)
        {
            ItemInstance flame = SelectedFlame;
            bool success = flame != null && this.SendCommand(new AssignPlayerSkillCommand(PlayerId, slot, flame.InstanceId));
            Text("preview", L(success ? "success" : "error.InvalidSource")); RefreshSlots();
        }

        public void CancelDrop() { ResetOperation(); if (IsVisible) { Refresh(); } }

        public bool Cancel()
        {
            if (!IsVisible) { return false; }
            if (_menu.Workspace.IsDragging) { _menu.Workspace.CancelDrag(); ResetOperation(); return true; }
            if (_preview != null || _growthPreview != null || _bottle != null) { ResetOperation(); Refresh(); return true; }
            if (Canvas.panel?.focusController.focusedElement != Canvas) { Canvas.Focus(); return true; }
            return false;
        }
        void ResetOperation()
        {
            _moving = null; _preview = null; _growthPreview = null; _bottle = null; _charging = false; _feedback = SkillBoardFailure.None;
            _chosen.Clear(); _lastRotation = -1; _multipleLinks = false; _reviewingLinks = false;
            _root.Q("board-links")?.Clear();
            _root.Q<Label>("board-preview").text = string.Empty;
            if (_board != null && IsVisible) { Draw(); }
        }
    }
}
