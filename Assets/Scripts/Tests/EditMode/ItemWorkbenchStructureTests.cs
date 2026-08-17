using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public class ItemWorkbenchStructureTests
{
    [Test]
    public void GameRoot_UsesOneTooltipAndOneSharedWorkbench()
    {
        string root = Read("Assets/UI/GameRoot.uxml");

        Assert.AreEqual(1, Count(root, "template=\"ItemWorkbench\""));
        Assert.AreEqual(1, Count(root, "template=\"ItemDetail\""));
        Assert.AreEqual(1, Count(root, "name=\"item-tooltip\""));
        StringAssert.Contains("name=\"item-drag-layer\"", root);
    }

    [Test]
    public void ContextPages_DoNotDuplicatePlayerInventoryOrItemDetail()
    {
        string inventory = Read("Assets/UI/Inventory.uxml");
        string shop = Read("Assets/UI/Shop.uxml");
        string crafting = Read("Assets/UI/Crafting.uxml");
        string workbench = Read("Assets/UI/ItemWorkbench.uxml");
        string detail = Read("Assets/UI/ItemDetail.uxml");

        StringAssert.DoesNotContain("inventory-comparison", inventory);
        StringAssert.Contains("inventory-attribute-card", inventory);
        StringAssert.DoesNotContain("shop-player-scroll", shop);
        StringAssert.DoesNotContain("shop-item-detail", shop);
        StringAssert.DoesNotContain("crafting-item-list", crafting);
        StringAssert.DoesNotContain("crafting-item-detail", crafting);
        StringAssert.DoesNotContain("ScrollView", crafting);
        StringAssert.DoesNotContain("crafting-affix-list", crafting);
        StringAssert.Contains("name=\"crafting-scope-any\"", crafting);
        StringAssert.Contains("name=\"crafting-scope-prefix\"", crafting);
        StringAssert.Contains("name=\"crafting-scope-suffix\"", crafting);
        StringAssert.Contains("name=\"crafting-reroll-affixes\"", crafting);
        StringAssert.Contains("name=\"crafting-add-affix\"", crafting);
        StringAssert.Contains("name=\"crafting-remove-affix\"", crafting);
        StringAssert.Contains("name=\"crafting-reroll-values\"", crafting);
        StringAssert.Contains("name=\"inventory-grid\"", workbench);
        StringAssert.Contains("name=\"inventory-slot-ring-left\"", workbench);
        StringAssert.Contains("name=\"inventory-slot-ring-right\"", workbench);
        StringAssert.Contains("name=\"equipment-future-slots\"", workbench);
        StringAssert.Contains("name=\"shop-merchant-frame\"", shop);
        StringAssert.DoesNotContain("ScrollView", shop);
        StringAssert.DoesNotContain("ScrollView", detail);
        StringAssert.Contains("name=\"item-detail-body\"", detail);
    }

    [Test]
    public void WorkbenchFrame_IsThinAndInputHasRearrangeBindings()
    {
        string style = Read("Assets/UI/ItemWorkbench.uss");
        string menuStyle = Read("Assets/UI/GameMenu.uss");
        string actions = Read("Assets/Settings/InputSystem_Actions.inputactions");

        StringAssert.Contains("border-left-width: 1px;", style);
        StringAssert.DoesNotContain("background-image", style);
        StringAssert.Contains("flex-direction: column;", style);
        StringAssert.Contains("max-width: 49%;", style);
        StringAssert.Contains("width: 1160px;", menuStyle);
        StringAssert.Contains("height: 940px;", menuStyle);
        StringAssert.Contains("width: 360px;", menuStyle);
        StringAssert.Contains("height: 680px;", menuStyle);
        StringAssert.Contains("\"name\": \"Rearrange\"", actions);
        StringAssert.Contains("<Keyboard>/space", actions);
        StringAssert.Contains("<Gamepad>/buttonWest", actions);
    }

    [Test]
    public void BackpackItems_UseIconOnlyAndMerchantGridHasNoScrollState()
    {
        string inventoryController = Read("Assets/Scripts/Runtime/Gameplay/UI/InventoryPanelController.cs");
        string shopController = Read("Assets/Scripts/Runtime/Gameplay/UI/ShopPanelController.cs");
        string shopState = Read("Assets/Scripts/Runtime/Gameplay/UI/ShopViewState.cs");

        StringAssert.DoesNotContain("inventory-item-label", inventoryController);
        StringAssert.DoesNotContain("shop-item-summary", shopController);
        StringAssert.DoesNotContain("ScrollView", shopController);
        StringAssert.DoesNotContain("ScrollOffset", shopState);
        StringAssert.Contains("const int MerchantGridWidth = 10;", shopController);
        StringAssert.Contains("const int MerchantGridMinimumHeight = 6;", shopController);
    }

    [Test]
    public void PointerInteraction_RequiresDragAndExposesShopContextSale()
    {
        string inventoryController = Read("Assets/Scripts/Runtime/Gameplay/UI/InventoryPanelController.cs");
        string shopController = Read("Assets/Scripts/Runtime/Gameplay/UI/ShopPanelController.cs");

        StringAssert.Contains("const float DragThreshold = 10f;", inventoryController);
        StringAssert.Contains("if (_pointerPending && (evt.pressedButtons & 1) == 0)", inventoryController);
        StringAssert.DoesNotContain("evt.target != _workbench || _dragPointerId", inventoryController);
        StringAssert.Contains("ContextActionRequested?.Invoke(item);", inventoryController);
        StringAssert.Contains("_inventoryPanel.ContextActionRequested += OnInventoryContextActionRequested;", shopController);
        StringAssert.Contains("SellItem(selected);", shopController);
    }

    [Test]
    public void Crafting_RequiresAnExplicitInputSlotTarget()
    {
        string crafting = Read("Assets/UI/Crafting.uxml");
        string craftingStyle = Read("Assets/UI/Crafting.uss");
        string craftingController = Read("Assets/Scripts/Runtime/Gameplay/UI/CraftingPanelController.cs");
        string inventoryController = Read("Assets/Scripts/Runtime/Gameplay/UI/InventoryPanelController.cs");

        StringAssert.Contains("name=\"crafting-input-slot\"", crafting);
        StringAssert.Contains("name=\"crafting-slot-place\"", crafting);
        StringAssert.Contains("name=\"crafting-slot-remove\"", crafting);
        StringAssert.Contains("inventory-external-drop--valid", craftingStyle);
        StringAssert.Contains("ConfigureExternalDropTarget(", craftingController);
        StringAssert.Contains("public ItemInstance SlottedItem => _slottedItem;", craftingController);
        StringAssert.Contains("new CraftItemCommand(operation, scope, _slottedItem)", craftingController);
        StringAssert.DoesNotContain("new CraftItemCommand(operation, scope, _candidateItem)", craftingController);
        StringAssert.Contains("DropTargetKind.External", inventoryController);
        StringAssert.Contains("_externalDropHandler(item);", inventoryController);
        StringAssert.IsMatch(
            @"\.crafting-input-slot\s*\{[^}]*width:\s*100px;[^}]*height:\s*152px;",
            craftingStyle);
        StringAssert.Contains("SetExternalSlotItem(_slottedItem);", craftingController);
    }

    [Test]
    public void ContextTemplate_OnlyKeepsTheActivePageInLayout()
    {
        string menuController = Read("Assets/Scripts/Runtime/Gameplay/UI/GameMenuController.cs");

        StringAssert.Contains("SetTemplateVisible(_inventoryTemplate, showInventory);", menuController);
        StringAssert.Contains("SetTemplateVisible(_shopTemplate, showShop);", menuController);
        StringAssert.Contains("SetTemplateVisible(_craftingTemplate, showCrafting);", menuController);
    }

    [Test]
    public void ItemDetail_PrioritizesAffixContentAndUsesDenseLayout()
    {
        string detailView = Read("Assets/Scripts/Runtime/Gameplay/UI/ItemDetailView.cs");
        string detailStyle = Read("Assets/UI/ItemDetail.uss");
        int content = detailView.IndexOf("\"item-detail-affix-content\"", StringComparison.Ordinal);
        int name = detailView.IndexOf("\"item-detail-affix-name\"", StringComparison.Ordinal);

        Assert.GreaterOrEqual(content, 0);
        Assert.Greater(name, content, "词条内容必须先于弱化后的词条名显示");
        StringAssert.Contains("detail.AffixCount >= 4", detailView);
        StringAssert.IsMatch(
            @"\.item-detail-affix-name\s*\{[^}]*font-size:\s*9px;",
            detailStyle);
        StringAssert.IsMatch(
            @"\.item-detail-affix-content\s*\{[^}]*font-size:\s*12px;[^}]*-unity-font-style:\s*bold;",
            detailStyle);
        StringAssert.Contains(".item-detail--dense .item-detail-affix-content", detailStyle);
    }

    [Test]
    public void EquipmentSlots_RespectBackpackGridFootprints()
    {
        string style = Read("Assets/UI/ItemWorkbench.uss");

        StringAssert.IsMatch(
            @"\.equipment-slot--weapon\s*\{[^}]*width:\s*100px;[^}]*height:\s*152px;",
            style);
        StringAssert.IsMatch(
            @"\.equipment-slot--armor\s*\{[^}]*width:\s*100px;[^}]*height:\s*152px;",
            style);
        StringAssert.IsMatch(
            @"\.equipment-slot--ring-left\s*\{[^}]*width:\s*60px;[^}]*height:\s*60px;",
            style);
        StringAssert.IsMatch(
            @"\.equipment-slot--ring-right\s*\{[^}]*width:\s*60px;[^}]*height:\s*60px;",
            style);
        StringAssert.IsMatch(
            @"\.item-workbench-equipment\s*\{[^}]*flex-grow:\s*1;[^}]*min-height:\s*304px;",
            style);
        StringAssert.IsMatch(
            @"\.inventory-grid-frame\s*\{[^}]*height:\s*324px;",
            style);
        StringAssert.IsMatch(
            @"\.inventory-equipment-slot-icon\s*\{[^}]*position:\s*absolute;[^}]*left:\s*4px;[^}]*right:\s*4px;[^}]*top:\s*4px;[^}]*bottom:\s*4px;",
            style);
        StringAssert.Contains(".inventory-equipment-slot-label", style);
    }

    [Test]
    public void Tooltip_IsHiddenUntilPositionedAndOnlyUsesPreviewedItem()
    {
        string tooltip = Read("Assets/Scripts/Runtime/Gameplay/UI/ItemTooltipView.cs");
        string inventory = Read("Assets/Scripts/Runtime/Gameplay/UI/InventoryPanelController.cs");
        int hideBeforeDisplay = tooltip.IndexOf(
            "_root.style.visibility = Visibility.Hidden;",
            StringComparison.Ordinal);
        int display = tooltip.IndexOf(
            "_root.style.display = DisplayStyle.Flex;",
            StringComparison.Ordinal);

        Assert.GreaterOrEqual(hideBeforeDisplay, 0);
        Assert.Greater(display, hideBeforeDisplay, "提示窗必须先隐藏，再参与布局定位");
        StringAssert.Contains("_root.style.visibility = Visibility.Visible;", tooltip);
        StringAssert.Contains("if (_isDragging || _suppressTooltipUntilPreview)", inventory);
        StringAssert.Contains("if (_suppressTooltipUntilPreview)", inventory);
        StringAssert.Contains("if (_previewItem != null)", inventory);
        StringAssert.DoesNotContain(
            "ItemInstance item = _previewItem != null ? _previewItem : _selectedItem;",
            inventory);
    }

    static string Read(string path)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("无法解析项目根目录");
        return File.ReadAllText(Path.Combine(projectRoot, path));
    }

    static int Count(string text, string value)
    {
        int count = 0;
        int index = 0;

        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
