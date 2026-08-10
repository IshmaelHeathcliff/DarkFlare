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
