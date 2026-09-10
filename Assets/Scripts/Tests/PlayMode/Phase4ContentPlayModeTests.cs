using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        public IEnumerator MainScene_OfficialEquipmentCompletesTradeCraftAndRegisteredSlotFlow()
        {
            yield return _fixture.EnterMain();

            IArchitecture architecture = GameArchitectureProvider.RequireCurrent();
            EconomyModel economy = architecture.GetModel<EconomyModel>();
            CraftingSystem crafting = architecture.GetSystem<CraftingSystem>();
            PlayerController player = null;
            float timeout = Time.realtimeSinceStartup + SetupTimeoutSeconds;

            while ((player == null || economy.MerchantStock.Count == 0 || !crafting.IsConfigured)
                   && Time.realtimeSinceStartup < timeout)
            {
                player = Object.FindAnyObjectByType<PlayerController>();
                yield return null;
            }

            Assert.IsNotNull(player, "Main 场景未在时限内生成玩家");
            CollectionAssert.AreEquivalent(ApplicationHost.Current.ContentCatalog.GetAll<ItemBaseDefinition>(),
                economy.MerchantStock.Select(item => item.BaseDefinition), "商人未加载完整正式装备池");
            Assert.IsTrue(crafting.IsConfigured, "打造系统未加载词条池");

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
            var primary = new List<ItemInstance> { weapon, armor, leftRing, rightRing };
            foreach (EquipmentSlot slot in EquipmentSlots.All)
            {
                if ((int)slot >= 4) { primary.Add(economy.MerchantStock.First(item => item.BaseDefinition.CanEquipTo(slot))); }
            }
            ItemInstance[] purchased = primary.ToArray();
            ItemInstance[] alternatives = economy.MerchantStock.Where(item => !primary.Contains(item)).ToArray();
            architecture.GetSystem<TradingSystem>().GrantGold(10000);

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
            foreach (ItemInstance item in purchased.Skip(4))
            {
                EquipmentSlot slot = EquipmentSlots.GetUniqueTarget(item.BaseDefinition.AllowedEquipmentSlots).Value;
                Assert.IsTrue(architecture.SendCommand(new EquipItemCommand(actor, item, slot)), slot.ToString());
            }

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
            foreach (ItemInstance item in alternatives)
            {
                Assert.IsTrue(architecture.SendCommand(new BuyItemCommand(item)), item.BaseDefinition.Id);
                CraftingResult result = architecture.SendCommand(new CraftItemCommand(
                    CraftOperation.UpgradeRarity, CraftingAffixScope.Any, item));
                Assert.IsTrue(result.Succeeded, $"{item.BaseDefinition.Id}: {result.FailureReason}");
                Assert.IsNotEmpty(item.Prefixes.Concat(item.Suffixes));
                foreach (EquipmentSlot slot in slots.Where(item.BaseDefinition.CanEquipTo))
                {
                    Assert.IsTrue(architecture.SendCommand(new EquipItemCommand(actor, item, slot)), item.BaseDefinition.Id);
                    Assert.AreSame(item, equipment.GetItem(actor, slot));
                    Assert.IsTrue(architecture.SendCommand(new UnequipItemCommand(actor, slot)));
                }
                Assert.IsTrue(architecture.SendCommand(new SellItemCommand(item)), item.BaseDefinition.Id);
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
