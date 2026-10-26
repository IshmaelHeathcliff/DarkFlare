using UnityEngine.UIElements;

namespace DarkFlare
{
    public partial class InventoryPanelController
    {
        Button _bottleSlot;
        VisualElement _bottleIcon;
        Label _bottleEnergy;
        Label _bottleState;
        ItemInstance _equippedBottle;

        void BindBottle(VisualElement root)
        {
            _bottleSlot = root.Q<Button>("inventory-bottle-slot");
            _bottleIcon = root.Q("inventory-bottle-icon");
            _bottleEnergy = root.Q<Label>("inventory-bottle-energy");
            _bottleState = root.Q<Label>("inventory-bottle-state");
            if (_bottleSlot == null) { return; }
            _bottleSlot.clicked += SelectBottleSlot;
            _bottleSlot.RegisterCallback<PointerEnterEvent>(OnBottlePointerEnter);
            _bottleSlot.RegisterCallback<PointerLeaveEvent>(OnBottlePointerLeave);
            _bottleSlot.RegisterCallback<FocusInEvent>(OnBottleFocusIn);
            _bottleSlot.RegisterCallback<FocusOutEvent>(OnBottleFocusOut);
        }

        void UnbindBottle()
        {
            if (_bottleSlot != null)
            {
                _bottleSlot.clicked -= SelectBottleSlot;
                _bottleSlot.UnregisterCallback<PointerEnterEvent>(OnBottlePointerEnter);
                _bottleSlot.UnregisterCallback<PointerLeaveEvent>(OnBottlePointerLeave);
                _bottleSlot.UnregisterCallback<FocusInEvent>(OnBottleFocusIn);
                _bottleSlot.UnregisterCallback<FocusOutEvent>(OnBottleFocusOut);
            }
            _bottleSlot = null;
            _bottleIcon = null;
            _bottleEnergy = null;
            _bottleState = null;
            _equippedBottle = null;
        }

        void RefreshBottle()
        {
            if (_bottleSlot == null) { return; }
            ItemInstance bottle = LastSnapshot.HasPlayer ? this.SendQuery(new GetSkillBoardQuery()).EnergyBottle : null;
            ItemVisualPresenter.ApplyIcon(_bottleIcon, bottle?.BaseDefinition.Icon.AssetGUID);
            _equippedBottle = bottle;
            _bottleSlot.userData = bottle;
            _bottleSlot.EnableInClassList("inventory-equipment-slot--filled", bottle != null);
            bool full = bottle != null && bottle.SkillBoardState.Energy >= bottle.BaseDefinition.SkillBoard.EnergyCapacity;
            _bottleSlot.EnableInClassList("inventory-bottle-slot--full", full);
            _bottleState.text = full ? Localize("ui", "inventory.bottle.full") : string.Empty;
            _bottleState.EnableInClassList("inventory-bottle-state--hidden", !full);
            _bottleEnergy.text = bottle == null ? Localize("ui", "inventory.bottle.empty")
                : Localize("ui", "inventory.bottle.energy", bottle.SkillBoardState.Energy, bottle.BaseDefinition.SkillBoard.EnergyCapacity);
            _bottleSlot.tooltip = (bottle == null ? Localize("ui", "inventory.bottle.empty")
                : Resolve(ItemDetailSnapshotFactory.Create(bottle).Name) + " · " + _bottleEnergy.text)
                + "\n" + Localize("ui", "inventory.bottle.help");
        }

        void SelectBottleSlot()
        {
            _workspace.Activate(GameMenuPage.Inventory);
            _suppressTooltipUntilPreview = false;
            _targetSlot = null;
            _selectionHighlightSource = SelectionHighlightSource.EnergyBottle;
            if (_selectedItem?.BaseDefinition.ItemType != ItemType.EnergyBottle)
            {
                _selectedItem = null;
                _selectionState.Reset();
                _workspace.SelectInventoryItem(null);
            }
            RefreshSelection();
        }

        SkillBoardPreview PreviewBottle(bool equip)
        {
            return this.SendQuery(new PreviewSkillBoardQuery(SkillBoardPlayerIdentity.GetProfileId(LastSnapshot.Player),
                new SkillBoardRequest(equip ? SkillBoardOperation.EquipBottle : SkillBoardOperation.UnequipBottle,
                    equip ? _selectedItem : _equippedBottle)));
        }

        void RefreshBottleActions()
        {
            _targetSlotLabel.text = Localize("ui", "inventory.bottle.title");
            bool canEquip = LastSnapshot.HasPlayer && _selectedItem?.BaseDefinition.ItemType == ItemType.EnergyBottle
                && PreviewBottle(true).Success;
            bool canUnequip = LastSnapshot.HasPlayer && _equippedBottle != null && PreviewBottle(false).Success;
            _equipButton.SetEnabled(canEquip);
            _unequipButton.SetEnabled(canUnequip);
            SetFeedback(canEquip ? "inventory.feedback.can_equip" : canUnequip
                ? "inventory.feedback.can_unequip" : "inventory.feedback.select_item_or_slot");
        }

        void CommitBottle(bool equip)
        {
            if (!IsVisible || !LastSnapshot.HasPlayer) { return; }
            ItemInstance item = equip ? _selectedItem : _equippedBottle;
            SkillBoardPreview preview = PreviewBottle(equip);
            if (!preview.Success || !this.SendCommand(new CommitSkillBoardCommand(preview)))
            { SetFeedback("board.error." + (preview.Success ? SkillBoardFailure.Stale : preview.Failure)); return; }
            _selectionHighlightSource = equip ? SelectionHighlightSource.EnergyBottle : SelectionHighlightSource.Inventory;
            _selectionState.SelectedInstanceId = equip ? string.Empty : item.InstanceId;
            RefreshInventory();
            if (equip) { _bottleSlot.Focus(); }
            else if (_itemButtons.TryGetValue(item, out Button returned)) { returned.Focus(); }
        }

        void OnBottlePointerEnter(PointerEnterEvent evt) { PreviewItem(_equippedBottle); }
        void OnBottlePointerLeave(PointerLeaveEvent evt) { EndPreview(_equippedBottle); }
        void OnBottleFocusIn(FocusInEvent evt) { PreviewItem(_equippedBottle); }
        void OnBottleFocusOut(FocusOutEvent evt) { EndPreview(_equippedBottle); }
    }
}
