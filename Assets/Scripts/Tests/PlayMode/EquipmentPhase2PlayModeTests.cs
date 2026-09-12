using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DarkFlare;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DarkFlare.Tests
{
    public class EquipmentPhase2PlayModeTests
    {
        readonly List<Object> _objects = new List<Object>();
        readonly InputTestFixture _inputFixture = new InputTestFixture();
        readonly GameArchitectureTestFixture _fixture = new GameArchitectureTestFixture();

        IArchitecture _architecture;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            yield return _fixture.StopCurrent();
            yield return null;
            yield return InputTestFixtureGuard.Setup(_inputFixture);
            yield return _fixture.Restart();
            _architecture = _fixture.Architecture;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                {
                    Object.DestroyImmediate(_objects[i]);
                }
            }

            _objects.Clear();
            Time.timeScale = 1f;
            _architecture = null;
            yield return _fixture.StopCurrent();
            yield return null;
            InputTestFixtureGuard.TearDown(_inputFixture);
            yield return _fixture.Restart();
        }

        [UnityTest]
        public IEnumerator InventorySlots_SupportKeyboardGamepadLayoutAndProjectileSnapshot()
        {
            int originalWidth = Screen.width;
            int originalHeight = Screen.height;
            yield return _fixture.EnterMain();
            GameMenuController menu = null;
            RuntimePanelView document = null;
            CombatActor player = null;
            float timeout = Time.realtimeSinceStartup + 15f;

            while ((menu == null || document == null || document.Root?.panel == null || player == null)
                   && Time.realtimeSinceStartup < timeout)
            {
                menu = Object.FindAnyObjectByType<GameMenuController>();
                document = menu != null ? menu.GetComponent<RuntimePanelView>() : null;
                player = FindPlayer();
                yield return null;
            }

            Assert.IsNotNull(menu, "Main 场景未初始化 GameMenuController");
            Assert.IsNotNull(document, "UIRoot 缺少 RuntimePanelView");
            Assert.IsNotNull(player, "Main 场景未生成玩家");
            _architecture = menu.GetArchitecture();
            float baseMaxHealth = player.MaxHealth;
            InventoryModel inventory = _architecture.GetModel<InventoryModel>();
            EquipmentModel equipment = _architecture.GetModel<EquipmentModel>();
            ItemInstance weapon = CreateItem(
                "phase2_weapon",
                "阶段二武器",
                ItemType.Weapon,
                EquipmentSlotMask.Weapon,
                20f);
            ItemInstance armor = CreateItem(
                "phase2_armor",
                "阶段二护甲",
                ItemType.Armor,
                EquipmentSlotMask.Armor,
                maxHealth: 200f);
            ItemInstance leftRing = CreateItem(
                "phase2_left_ring",
                "左槽测试戒指",
                ItemType.Accessory,
                EquipmentSlotMask.Rings);
            ItemInstance rightRing = CreateItem(
                "phase2_right_ring",
                "右槽测试戒指",
                ItemType.Accessory,
                EquipmentSlotMask.Rings);
            Assert.IsTrue(inventory.TryAddItem(weapon));
            Assert.IsTrue(inventory.TryAddItem(armor));
            Assert.IsTrue(inventory.TryAddItem(leftRing));
            Assert.IsTrue(inventory.TryAddItem(rightRing));

            menu.OpenPage(GameMenuPage.Inventory);
            yield return null;
            yield return null;
            VisualElement root = document.Root;
            Assert.IsNull(root.Q<VisualElement>("attribute-card"), "HUD 不应继续显示当前属性窗口");
            Assert.IsNotNull(root.Q<VisualElement>("inventory-attribute-card"), "背包右侧缺少当前属性窗口");
            Assert.IsNotNull(root.Q<Button>("inventory-attributes-open"), "背包必须提供独立属性详情入口");
            Assert.IsNull(root.Q<VisualElement>("weapon-card"), "HUD 不应继续显示当前装备");
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            VisualElement backpackWeaponIcon = root.Q<Button>("inventory-item-phase2_weapon")
                .Q<VisualElement>(className: "inventory-item-icon");
            Vector2 backpackWeaponIconSize = backpackWeaponIcon.worldBound.size;

            yield return SelectAndSubmit(root, "阶段二武器", keyboard.enterKey);
            Assert.IsTrue(menu.Workspace.Interactions.IsMenuOpen);
            Assert.IsFalse(root.Q<Button>("item-action-equip-Weapon").enabledInHierarchy,
                "旧武器无法回到来源格时不能偷偷改放到别处");
            Assert.IsTrue(inventory.Grid.Placements.ContainsKey(weapon));
            menu.Workspace.Interactions.CloseMenu(true);
            Assert.IsTrue(_architecture.SendCommand(new MoveInventoryItemCommand(weapon, new Vector2Int(5, 0))));
            yield return null;
            yield return SelectAndSubmit(root, "阶段二武器", keyboard.enterKey);
            yield return Submit(root.Q<Button>("item-action-equip-Weapon"), keyboard.enterKey);
            Assert.AreSame(weapon, equipment.GetItem(player, EquipmentSlot.Weapon));
            Assert.AreSame(root.Q<Button>("inventory-slot-weapon"), root.focusController.focusedElement);
            VisualElement equippedWeaponIcon = root.Q<Button>("inventory-slot-weapon")
                .Q<VisualElement>("equipment-slot-icon");
            Assert.GreaterOrEqual(equippedWeaponIcon.worldBound.width, backpackWeaponIconSize.x - 0.5f);
            Assert.GreaterOrEqual(equippedWeaponIcon.worldBound.height, backpackWeaponIconSize.y - 0.5f);

            yield return SelectAndSubmit(root, "阶段二护甲", keyboard.enterKey);
            yield return Submit(root.Q<Button>("item-action-equip-Armor"), keyboard.enterKey);
            Assert.AreSame(armor, equipment.GetItem(player, EquipmentSlot.Armor));
            Assert.AreEqual(baseMaxHealth + 200f, player.MaxHealth, 0.001f);
            Assert.AreEqual(player.MaxHealth, player.CurrentHealth, 0.001f);
            Assert.AreEqual(
                $"{player.CurrentHealth:0.#} / {player.MaxHealth:0.#}",
                root.Q<ProgressBar>("health-bar").title);

            yield return SelectAndSubmit(root, "左槽测试戒指", gamepad.buttonSouth);
            Assert.IsTrue(menu.Workspace.Interactions.IsMenuOpen, "戒指必须明确选择槽位");
            Assert.IsNotNull(root.Q<Button>("item-action-equip-RingRight"));
            yield return Submit(root.Q<Button>("item-action-equip-RingLeft"), gamepad.buttonSouth);
            Assert.AreSame(leftRing, equipment.GetItem(player, EquipmentSlot.RingLeft));
            Assert.AreSame(root.Q<Button>("inventory-slot-ring-left"), root.focusController.focusedElement);

            yield return SelectAndSubmit(root, "右槽测试戒指", gamepad.buttonSouth);
            Assert.IsTrue(menu.Workspace.Interactions.IsMenuOpen, "第二个饰品仍应显式选择目标槽");
            yield return Submit(root.Q<Button>("item-action-equip-RingRight"), gamepad.buttonSouth);
            Assert.AreSame(rightRing, equipment.GetItem(player, EquipmentSlot.RingRight));
            Assert.AreSame(leftRing, equipment.GetItem(player, EquipmentSlot.RingLeft));

            ItemInstance candidateRing = CreateItem("compare_ring", "对比戒指", ItemType.Accessory, EquipmentSlotMask.Rings);
            Assert.IsTrue(inventory.TryAddItem(candidateRing));
            yield return null;
            FindInventoryButton(root, "对比戒指").Focus();
            yield return null;
            _inputFixture.Press(keyboard.leftShiftKey);
            yield return null;
            yield return null;
            yield return null;
            VisualElement firstComparison = root.Q("item-comparison-0");
            VisualElement secondComparison = root.Q("item-comparison-1");
            Assert.AreEqual(Visibility.Visible, firstComparison.resolvedStyle.visibility);
            Assert.AreEqual(Visibility.Visible, secondComparison.resolvedStyle.visibility);
            Assert.LessOrEqual(root.Q("item-tooltip").worldBound.xMax, firstComparison.worldBound.xMin);
            Assert.LessOrEqual(firstComparison.worldBound.xMax, secondComparison.worldBound.xMin);
            Assert.IsNull(root.Q("item-detail-icon"));
            Assert.AreEqual(ApplicationHost.Current.Localization.GetString(leftRing.BaseDefinition.LocalizedName.Message),
                firstComparison.Q<Label>("item-detail-name").text);
            Assert.AreEqual(ApplicationHost.Current.Localization.GetString(rightRing.BaseDefinition.LocalizedName.Message),
                secondComparison.Q<Label>("item-detail-name").text);
            _inputFixture.Release(keyboard.leftShiftKey);
            yield return null;
            Assert.AreEqual(DisplayStyle.None, firstComparison.resolvedStyle.display);
            Assert.AreEqual(DisplayStyle.None, secondComparison.resolvedStyle.display);
            _inputFixture.Press(gamepad.leftTrigger);
            yield return null;
            yield return null;
            yield return null;
            Assert.AreEqual(Visibility.Visible, firstComparison.resolvedStyle.visibility);
            menu.Workspace.SuppressPreview();
            yield return null;
            Assert.AreEqual(DisplayStyle.None, firstComparison.resolvedStyle.display);
            _inputFixture.Release(gamepad.leftTrigger);
            menu.Workspace.AllowPreview();

            yield return Submit(root.Q<Button>("inventory-slot-ring-left"), gamepad.buttonSouth);
            yield return Submit(root.Q<Button>("item-action-unequip"), gamepad.buttonSouth);
            Assert.IsNull(equipment.GetItem(player, EquipmentSlot.RingLeft));
            Assert.IsTrue(inventory.Grid.Placements.ContainsKey(leftRing));
            Assert.IsTrue(ApplicationHost.TryGetCurrent(out ApplicationHost host));
            StringAssert.Contains(
                host.Localization.GetString(leftRing.BaseDefinition.LocalizedName.Message),
                root.Q<Label>("item-operation-status").text);

            Vector2Int[] resolutions =
            {
                new Vector2Int(1920, 1080),
            };

            try
            {
                for (int i = 0; i < resolutions.Length; i++)
                {
                    SetGameViewResolution(resolutions[i]);
                    yield return WaitForResolution(resolutions[i], 5f);
                    yield return null;
                    menu.OpenPage(GameMenuPage.Attributes);
                    yield return null;
                    AssertLayoutInsideRoot(root, new[]
                    {
                        "game-menu-panel",
                        "attributes-scroll",
                        "inventory-page",
                        "inventory-grid",
                        "inventory-equipment",
                        "inventory-slot-weapon",
                        "inventory-slot-armor",
                        "inventory-slot-ring-left",
                        "inventory-slot-ring-right",
                        "inventory-slot-head",
                        "inventory-slot-hands",
                        "inventory-slot-legs",
                        "inventory-slot-off-hand",
                        "inventory-slot-necklace",
                        "inventory-slot-belt",
                        "inventory-actions",
                        "inventory-equip",
                        "inventory-unequip",
                        "game-menu-close",
                    });
                    ScrollView attributes = root.Q<ScrollView>("attributes-scroll");
                    foreach (string name in new[] { "attribute-armor", "attribute-chaos_resistance" })
                    {
                        VisualElement attribute = root.Q(name);
                        Assert.IsNotNull(attribute);
                        attributes.ScrollTo(attribute);
                        yield return null;
                        Assert.GreaterOrEqual(attribute.worldBound.yMin, attributes.worldBound.yMin - 1f);
                        Assert.LessOrEqual(attribute.worldBound.yMax, attributes.worldBound.yMax + 1f,
                            $"滚动后属性 {name} 仍不可见");
                    }
                }
            }
            finally
            {
                SetGameViewResolution(new Vector2Int(originalWidth, originalHeight));
            }

            menu.ClosePage(GameMenuPage.Attributes);
            foreach (EquipmentSlot slot in EquipmentSlots.All)
            {
                if ((int)slot < 4) { continue; }
                EquipmentSlotMask mask = EquipmentSlots.ToMask(slot);
                ItemType type = (mask & EquipmentSlotMask.Defenses) != 0 ? ItemType.Armor : ItemType.Accessory;
                ItemInstance item = CreateItem("equipment_" + slot, "槽位测试 " + slot, type, mask);
                Assert.IsTrue(inventory.TryAddItem(item));
                yield return SelectAndSubmit(root, "槽位测试 " + slot, gamepad.buttonSouth);
                yield return Submit(root.Q<Button>("item-action-equip-" + slot), gamepad.buttonSouth);
                Assert.AreSame(item, equipment.GetItem(player, slot), slot.ToString());
            }
            string[] upper = { "head", "weapon", "off-hand", "necklace" };
            string[] lower = { "armor", "hands", "legs", "ring-right" };
            for (int i = 0; i < upper.Length; i++)
            {
                root.Q<Button>("inventory-slot-" + upper[i]).Focus();
                yield return null;
                _inputFixture.Press(gamepad.dpad.down);
                yield return null;
                _inputFixture.Release(gamepad.dpad.down);
                yield return null;
                yield return null;
                Assert.AreSame(root.Q<Button>("inventory-slot-" + lower[i]), root.focusController.focusedElement,
                    upper[i] + " 向下应选中视觉对应槽位");
                _inputFixture.Press(gamepad.dpad.up);
                yield return null;
                _inputFixture.Release(gamepad.dpad.up);
                yield return null;
                yield return null;
                Assert.AreSame(root.Q<Button>("inventory-slot-" + upper[i]), root.focusController.focusedElement);

            }

            _architecture.GetUtility<GameInput>().SwitchToGameplay();
            yield return null;
            ProjectileSkillDefinition skill = CreateSkill();
            AttackSnapshot inFlight = AttackSnapshotFactory.CreateProjectile(player, skill, equipment, 42);
            Assert.AreEqual(20f, inFlight.BaseDamages[0].Amount);
            ItemInstance strongerWeapon = CreateItem(
                "phase2_strong_weapon",
                "换装后武器",
                ItemType.Weapon,
                EquipmentSlotMask.Weapon,
                100f);
            Assert.IsTrue(inventory.TryAddItem(strongerWeapon));
            Assert.IsTrue(_architecture.SendCommand(
                new EquipItemCommand(player, strongerWeapon, EquipmentSlot.Weapon)));
            CombatActor defender = CreateDefender();
            GameObject projectileObject = new GameObject("Phase2SnapshotProjectile");
            _objects.Add(projectileObject);
            ProjectileController projectile = projectileObject.AddComponent<ProjectileController>();
            projectile.Init(skill, Vector2.right, inFlight);
            Assert.IsTrue(projectile.TryHit(defender, out DamageResult hitResult));
            yield return null;

            Assert.AreEqual(20f, hitResult.TotalDamage, 0.001f, "在途投射物读取了换装后的武器");
        }

        IEnumerator SelectAndSubmit(VisualElement root, string displayName, ButtonControl submitControl)
        {
            Button button = FindInventoryButton(root, displayName);
            Assert.IsNotNull(button, $"背包中缺少 {displayName}");
            yield return Submit(button, submitControl);
        }

        IEnumerator Submit(Button button, ButtonControl submitControl)
        {
            Assert.IsNotNull(button);
            button.Focus();
            yield return null;
            _inputFixture.PressAndRelease(submitControl);
            yield return null;
            yield return null;
        }

        ItemInstance CreateItem(
            string id,
            string displayName,
            ItemType type,
            EquipmentSlotMask slots,
            float damage = 0f,
            float maxHealth = 0f)
        {
            ItemBaseDefinition definition = ScriptableObject.CreateInstance<ItemBaseDefinition>();
            _objects.Add(definition);
            SetField(definition, "_id", id);
            SetField(definition, "_displayName", displayName);
            SetField(
                definition,
                "_localizedName",
                new LocalizedContentReference("items", GetItemLocalizationKey(id, type)));
            SetField(definition, "_itemType", type);
            SetField(definition, "_allowedEquipmentSlots", slots);
            SetField(definition, "_gridSize", Vector2Int.one);

            if (damage > 0f)
            {
                DamageRollDefinition roll = new DamageRollDefinition();
                SetField(roll, "_damageType", DamageType.Physical);
                SetField(roll, "_amountRange", new Vector2(damage, damage));
                SetField(definition, "_baseDamages", new List<DamageRollDefinition> { roll });
            }

            if (maxHealth > 0f)
            {
                StatDefinition stat = ScriptableObject.CreateInstance<StatDefinition>();
                _objects.Add(stat);
                SetField(stat, "_id", StatIds.MaxHealth);
                SetField(stat, "_displayName", "最大生命");
                SetField(
                    stat,
                    "_localizedName",
                    new LocalizedContentReference("stats", StatIds.MaxHealth));
                StatModifierDefinition modifier = new StatModifierDefinition();
                SetField(modifier, "_stat", stat);
                SetField(modifier, "_operation", ModifierOperation.Flat);
                SetField(modifier, "_scope", ModifierScope.GlobalActor);
                SetField(modifier, "_valueRange", new Vector2(maxHealth, maxHealth));
                SetField(definition, "_implicitModifiers", new List<StatModifierDefinition> { modifier });
            }

            return definition.CreateInstance(id, 1, 1);
        }

        static string GetItemLocalizationKey(string id, ItemType type)
        {
            if (id.Contains("left_ring"))
            {
                return "item.jade_ring.name";
            }

            if (id.Contains("right_ring"))
            {
                return "item.obsidian_ring.name";
            }

            return type == ItemType.Armor
                ? "item.leather_armor.name"
                : "item.great_sword.name";
        }

        ProjectileSkillDefinition CreateSkill()
        {
            ProjectileSkillDefinition skill = ScriptableObject.CreateInstance<ProjectileSkillDefinition>();
            _objects.Add(skill);
            SetField(skill, "_id", "phase2_snapshot_skill");
            SetField(skill, "_damageSource", ProjectileDamageSource.EquippedWeapon);
            SetField(skill, "_projectileSpeed", 1f);
            SetField(skill, "_projectileLifetime", 10f);
            return skill;
        }

        CombatActor CreateDefender()
        {
            GameObject defenderObject = new GameObject("Phase2SnapshotDefender");
            defenderObject.SetActive(false);
            _objects.Add(defenderObject);
            defenderObject.AddComponent<CircleCollider2D>();
            CombatActor defender = defenderObject.AddComponent<CombatActor>();
            defender.Configure("phase2_snapshot_defender", ActorTeam.Monster, 1000f, new StatBlock(), TagSet.Empty);
            return defender;
        }

        static CombatActor FindPlayer()
        {
            CombatActor[] actors = Object.FindObjectsByType<CombatActor>();

            for (int i = 0; i < actors.Length; i++)
            {
                if (actors[i].Team == ActorTeam.Player)
                {
                    return actors[i];
                }
            }

            return null;
        }

        static Button FindInventoryButton(VisualElement root, string displayName)
        {
            VisualElement grid = root.Q<VisualElement>("inventory-grid");

            for (int i = 0; i < grid.childCount; i++)
            {
                if (grid[i] is not Button button)
                {
                    continue;
                }

                if (button.userData is ItemInstance item
                    && item.BaseDefinition != null
                    && item.BaseDefinition.DisplayName == displayName)
                {
                    return button;
                }
            }

            return null;
        }

        static IEnumerator WaitForResolution(Vector2Int resolution, float timeoutSeconds)
        {
            float timeout = Time.realtimeSinceStartup + timeoutSeconds;

            while ((Screen.width != resolution.x || Screen.height != resolution.y)
                   && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.AreEqual(resolution.x, Screen.width);
            Assert.AreEqual(resolution.y, Screen.height);
        }

        static void SetGameViewResolution(Vector2Int resolution)
        {
            const string UtilityTypeName = "DarkFlare.Editor.Phase0GameViewResolutionUtility, DarkFlare.Editor";
            System.Type utilityType = System.Type.GetType(UtilityTypeName, true);
            MethodInfo method = utilityType.GetMethod("SetResolution", BindingFlags.Static | BindingFlags.Public)
                ?? throw new System.MissingMethodException(UtilityTypeName, "SetResolution");
            method.Invoke(null, new object[] { resolution.x, resolution.y });
        }

        static void AssertLayoutInsideRoot(VisualElement root, IReadOnlyList<string> names)
        {
            Rect rootBounds = root.worldBound;

            for (int i = 0; i < names.Count; i++)
            {
                VisualElement element = root.Q<VisualElement>(names[i]);
                Assert.IsNotNull(element, $"缺少 UI 元素 {names[i]}");
                Rect bounds = element.worldBound;
                Assert.Greater(bounds.width, 0f, $"{names[i]} 宽度无效");
                Assert.Greater(bounds.height, 0f, $"{names[i]} 高度无效");
                Assert.GreaterOrEqual(bounds.xMin, rootBounds.xMin - 1f, $"{names[i]} 超出左边界");
                Assert.GreaterOrEqual(bounds.yMin, rootBounds.yMin - 1f, $"{names[i]} 超出上边界");
                Assert.LessOrEqual(bounds.xMax, rootBounds.xMax + 1f, $"{names[i]} 超出右边界");
                Assert.LessOrEqual(bounds.yMax, rootBounds.yMax + 1f, $"{names[i]} 超出下边界");
            }


            Rect gridFrame = root.Q<VisualElement>("inventory-grid-frame").worldBound;
            Rect grid = root.Q<VisualElement>("inventory-grid").worldBound;
            Assert.GreaterOrEqual(grid.xMin, gridFrame.xMin - 1f, "背包格超出左边界");
            Assert.GreaterOrEqual(grid.yMin, gridFrame.yMin - 1f, "背包格超出上边界");
            Assert.LessOrEqual(grid.xMax, gridFrame.xMax + 1f, "背包格超出右边界");
            Assert.LessOrEqual(grid.yMax, gridFrame.yMax + 1f, "背包格超出下边界");
        }

        static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"缺少字段 {target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }
    }
}
