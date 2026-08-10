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
        string shop = Read("Assets/UI/Shop.uxml");
        string crafting = Read("Assets/UI/Crafting.uxml");
        string workbench = Read("Assets/UI/ItemWorkbench.uxml");

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
    }

    [Test]
    public void WorkbenchFrame_IsThinAndInputHasRearrangeBindings()
    {
        string style = Read("Assets/UI/ItemWorkbench.uss");
        string actions = Read("Assets/Settings/InputSystem_Actions.inputactions");

        StringAssert.Contains("border-left-width: 1px;", style);
        StringAssert.DoesNotContain("background-image", style);
        StringAssert.Contains("flex-direction: column;", style);
        StringAssert.Contains("max-width: 49%;", style);
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
