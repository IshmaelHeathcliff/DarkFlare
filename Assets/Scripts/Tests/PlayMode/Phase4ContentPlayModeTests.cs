using System.Collections;
using System.Collections.Generic;
using DarkFlare;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public class Phase4ContentPlayModeTests
    {
        const float SetupTimeoutSeconds = 20f;

        readonly GameArchitectureTestFixture _fixture = new GameArchitectureTestFixture();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return _fixture.Restart();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return _fixture.Restart();
        }

        [UnityTest]
        public IEnumerator MainScene_OfficialEquipmentCompletesTradeCraftAndFourSlotFlow()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);

            IArchitecture architecture = GameArchitectureProvider.RequireCurrent();
            EconomyModel economy = architecture.GetModel<EconomyModel>();
            CraftingSystem crafting = architecture.GetSystem<CraftingSystem>();
            PlayerController player = null;
            float timeout = Time.realtimeSinceStartup + SetupTimeoutSeconds;

            while ((player == null || economy.MerchantStock.Count != 7 || !crafting.IsConfigured)
                   && Time.realtimeSinceStartup < timeout)
            {
                player = Object.FindAnyObjectByType<PlayerController>();
                yield return null;
            }

            Assert.IsNotNull(player, "Main 场景未在时限内生成玩家");
            Assert.AreEqual(7, economy.MerchantStock.Count, "商人未加载七件正式装备");
            Assert.IsTrue(crafting.IsConfigured, "打造系统未加载十二词条池");

            player.enabled = false;
            MonsterSpawner spawner = Object.FindAnyObjectByType<MonsterSpawner>();

            if (spawner != null)
            {
                spawner.enabled = false;
            }

            ItemInstance weapon = FindStock(economy.MerchantStock, "great_sword");
            ItemInstance armor = FindStock(economy.MerchantStock, "leather_armor");
            ItemInstance leftRing = FindStock(economy.MerchantStock, "iron_ring");
            ItemInstance rightRing = FindStock(economy.MerchantStock, "jade_ring");
            ItemInstance[] purchased = { weapon, armor, leftRing, rightRing };
            architecture.GetSystem<TradingSystem>().GrantGold(1000);

            for (int i = 0; i < purchased.Length; i++)
            {
                Assert.IsTrue(
                    architecture.SendCommand(new BuyItemCommand(purchased[i])),
                    $"购买 {purchased[i].BaseDefinition.Id} 失败");
            }

            InventoryModel inventory = architecture.GetModel<InventoryModel>();

            for (int i = 0; i < purchased.Length; i++)
            {
                Assert.IsTrue(inventory.Grid.Placements.ContainsKey(purchased[i]));
            }

            Assert.AreEqual(0, weapon.Prefixes.Count + weapon.Suffixes.Count);
            CraftingResult crafted = architecture.SendCommand(new CraftItemCommand(
                CraftOperation.UpgradeRarity,
                CraftingAffixScope.Any,
                weapon));
            Assert.IsTrue(crafted.Succeeded, $"正式武器提升稀有度失败：{crafted.FailureReason}");
            Assert.AreEqual(ItemRarity.Magic, weapon.Rarity);
            Assert.That(
                weapon.Prefixes.Count + weapon.Suffixes.Count,
                Is.InRange(1, 2),
                "打造未生成符合魔法物品规则的兼容词条");

            EquipmentModel equipment = architecture.GetModel<EquipmentModel>();
            CombatActor actor = player.Actor;
            Assert.IsTrue(architecture.SendCommand(new EquipItemCommand(actor, weapon, EquipmentSlot.Weapon)));
            Assert.IsTrue(architecture.SendCommand(new EquipItemCommand(actor, armor, EquipmentSlot.Armor)));
            Assert.IsTrue(architecture.SendCommand(new EquipItemCommand(actor, leftRing, EquipmentSlot.RingLeft)));
            Assert.IsTrue(architecture.SendCommand(new EquipItemCommand(actor, rightRing, EquipmentSlot.RingRight)));
            Assert.AreSame(weapon, equipment.GetItem(actor, EquipmentSlot.Weapon));
            Assert.AreSame(armor, equipment.GetItem(actor, EquipmentSlot.Armor));
            Assert.AreSame(leftRing, equipment.GetItem(actor, EquipmentSlot.RingLeft));
            Assert.AreSame(rightRing, equipment.GetItem(actor, EquipmentSlot.RingRight));

            IReadOnlyList<EquipmentSlot> slots = EquipmentSlots.All;

            for (int i = 0; i < slots.Count; i++)
            {
                Assert.IsTrue(architecture.SendCommand(new UnequipItemCommand(actor, slots[i])));
            }

            for (int i = 0; i < purchased.Length; i++)
            {
                Assert.IsTrue(inventory.Grid.Placements.ContainsKey(purchased[i]));
                Assert.IsTrue(
                    architecture.SendCommand(new SellItemCommand(purchased[i])),
                    $"出售 {purchased[i].BaseDefinition.Id} 失败");
                Assert.IsFalse(inventory.Grid.Placements.ContainsKey(purchased[i]));
            }
        }

        static ItemInstance FindStock(IReadOnlyList<ItemInstance> stock, string itemId)
        {
            for (int i = 0; i < stock.Count; i++)
            {
                ItemInstance item = stock[i];

                if (item.BaseDefinition != null && item.BaseDefinition.Id == itemId)
                {
                    return item;
                }
            }

            Assert.Fail($"商店库存中缺少 {itemId}");
            return null;
        }
    }
}
