using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DarkFlare.Tests
{
    public partial class Phase1UxPlayModeTests
    {
        [UnityTest]
        public IEnumerator Hud_SkillHoverBlocksHeldPointerAndKeepsGameplayControls()
        {
            yield return _fixture.EnterMain();
            HudController hud = Object.FindAnyObjectByType<HudController>();
            GameMenuController menu = Object.FindAnyObjectByType<GameMenuController>();
            _architecture = hud.GetArchitecture();
            PlayerController player = Object.FindAnyObjectByType<PlayerController>();
            VisualElement root = hud.GetComponent<RuntimePanelView>().Root;
            VisualElement details = root.Q("hud-skill-tooltip");
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            Vector2Int resolution = new Vector2Int(Screen.width, Screen.height);
            InputSettings.UpdateMode updateMode = InputSystem.settings.updateMode;
            object testRuntime = GetSimulatedInputRuntime();
            System.Reflection.PropertyInfo physicalClock = testRuntime.GetType().GetProperty("currentTimeForFixedUpdate");
            object previousPhysicalTime = physicalClock.GetValue(testRuntime);
            int casts = 0;
            IUnRegister registration = _architecture.RegisterEvent<PlayerSkillCastEvent>(_ => casts++);
            try
            {
                SetGameViewResolution(new Vector2Int(1920, 1080));
                yield return WaitForResolution(new Vector2Int(1920, 1080), 5);
                yield return null; yield return null;
                float inputDeadline = Time.realtimeSinceStartup + 5;
                while (!ApplicationHost.Current.Input.IsGameplayEnabled && Time.realtimeSinceStartup < inputDeadline) { yield return null; }
                Assert.IsTrue(ApplicationHost.Current.Input.IsGameplayEnabled);
                _inputFixture.Set(mouse.position, new Vector2(10, 10));
                yield return null; yield return null;
                yield return Hover("hud-flame-0");
                Assert.That(details.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(root.Q<Label>("hud-skill-name").text, Is.EqualTo(ApplicationHost.Current.Localization.GetString("ui", "hud.skill.empty")));
                _inputFixture.Press(mouse.leftButton);
                yield return new WaitForSecondsRealtime(.65f);
                Assert.That(casts, Is.Zero, "空槽点击不能普攻或建立保持");
                SetSkillPointer(mouse, player, Vector2.up);
                yield return new WaitForSecondsRealtime(.65f);
                Assert.That(details.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
                Assert.That(casts, Is.Zero, "离开热点不能复活旧的保持");
                _inputFixture.Release(mouse.leftButton); yield return null;
                _inputFixture.Press(mouse.leftButton);
                yield return WaitForSkillCasts(() => casts, 1);
                yield return Hover("hud-normal");
                int entered = casts;
                Assert.That(details.worldBound.Overlaps(root.Q("hud-normal").worldBound), Is.False);
                Assert.That(details.worldBound.Overlaps(root.Q("health-card").worldBound), Is.False);
                yield return Hover("hud-skill-tooltip");
                yield return new WaitForSecondsRealtime(.65f);
                Assert.That(details.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), "移入说明可继续阅读");
                Assert.That(casts, Is.EqualTo(entered), "持有鼠标进入技能或说明区域必须取消保持");
                SetSkillPointer(mouse, player, Vector2.up);
                yield return new WaitForSecondsRealtime(.65f);
                Assert.That(casts, Is.EqualTo(entered));
                _inputFixture.Release(mouse.leftButton); yield return null;
                yield return Hover("hud-normal");
                _inputFixture.Press(mouse.leftButton); yield return new WaitForSecondsRealtime(.65f);
                _inputFixture.Release(mouse.leftButton); yield return null;
                Assert.That(casts, Is.EqualTo(entered), "技能槽不提供点击施法");
                Assert.IsFalse(ApplicationHost.Current.Input.IsUiEnabled);
                Vector3 previous = player.transform.position;
                // 模拟运行时的移动在物理步采样，避免夹具手动 Dynamic 更新与 Fixed 状态缓冲交错。
                InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInFixedUpdate;
                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.right });
                FlushPhysicalInput();
                Assert.That(ApplicationHost.Current.Input.Move.x, Is.GreaterThan(.9f));
                yield return new WaitForSeconds(.2f);
                Assert.That(Vector3.Distance(previous, player.transform.position), Is.GreaterThan(.05f), "悬停不夺走手柄移动");
                InputSystem.QueueStateEvent(pad, new GamepadState());
                FlushPhysicalInput();
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                InputSystem.settings.updateMode = updateMode;
                _inputFixture.PressAndRelease(pad.buttonWest);
                yield return WaitForSkillCasts(() => casts, entered + 1);
                foreach (string entry in new[] { "inventory", "attributes", "board", "pause" })
                {
                    yield return Hover("game-hud-" + entry);
                    Assert.That(root.Q<Label>("hud-skill-name").text, Is.Not.Empty);
                    Assert.That(root.Q("game-hud-" + entry).worldBound.width, Is.GreaterThanOrEqualTo(40));
                    yield return BoardClick(mouse, root, root.Q("game-hud-" + entry).worldBound.center);
                    Assert.That(menu.IsOpen, Is.True, entry);
                    Assert.That(details.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
                    Assert.That(root.Q("hud-flames").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
                    Assert.That(root.Q("health-card").worldBound.height, Is.GreaterThan(0));
                    Assert.That(root.Q("hud-normal").worldBound.height, Is.GreaterThan(0));
                    if (entry == "pause") { menu.TogglePause(); }
                    else { menu.ClosePage(entry == "inventory" ? GameMenuPage.Inventory : entry == "attributes" ? GameMenuPage.Attributes : GameMenuPage.SkillBoard); }
                    yield return null; yield return null;
                }
                Assert.That(casts, Is.EqualTo(entered + 1), "四窗口入口点击不能施法");
                player.GetComponent<PlayerInteractionController>().enabled = false;
                _architecture.SendCommand(new SetInteractionFocusCommand(Object.FindObjectsByType<WorldInteractionTarget>().First(t => t.CanInteract)));
                ApplicationHost.Current.ApplicationShell.ShowToast(LocalizedMessage.Ui("world.reset.success"));
                yield return Hover("hud-normal");
                VisualElement prompt = root.Q("interaction-prompt");
                VisualElement toast = root.panel.visualTree.Q("application-toast-message");
                Assert.That(details.worldBound.Overlaps(prompt.worldBound), Is.False, "悬停说明避开世界交互提示");
                Assert.That(toast.worldBound.Overlaps(prompt.worldBound), Is.False, "Toast 避开世界交互提示");
                Assert.That(toast.worldBound.Overlaps(details.worldBound), Is.False, "Toast 避开技能详情");
            }
            finally
            {
                registration.UnRegister();
                physicalClock.SetValue(testRuntime, previousPhysicalTime);
                InputSystem.settings.updateMode = updateMode;
                SetGameViewResolution(resolution);
            }

            IEnumerator Hover(string name)
            {
                _inputFixture.Set(mouse.position, PanelToScreen(root, root.Q(name).worldBound.center));
                yield return null; yield return null;
                float deadline = Time.realtimeSinceStartup + 2;
                while (details.resolvedStyle.display != DisplayStyle.Flex && Time.realtimeSinceStartup < deadline) { yield return null; }
            }
        }

        void FlushPhysicalInput()
        {
            // 夹具只推进 Dynamic 时钟，物理输入要同步模拟 Fixed 时钟再处理同一事件。
            object runtime = GetSimulatedInputRuntime();
            runtime.GetType().GetProperty("currentTimeForFixedUpdate").SetValue(runtime, InputState.currentTime + 1);
            typeof(InputSystem).GetMethod("Update", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic,
                null, new[] { typeof(InputUpdateType) }, null).Invoke(null, new object[] { InputUpdateType.Fixed });
        }

        static object GetSimulatedInputRuntime()
        {
            object manager = typeof(InputSystem).GetProperty("manager", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
            return manager.GetType().GetProperty("runtime", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(manager);
        }

        [UnityTest]
        public IEnumerator Hud_SkillDetailsFollowAuthorityAndReleaseOldVisualTrees()
        {
            yield return _fixture.EnterMain();
            ApplicationHost host = ApplicationHost.Current;
            HudController hud = Object.FindAnyObjectByType<HudController>();
            _architecture = hud.GetArchitecture();
            RuntimePanelView view = hud.GetComponent<RuntimePanelView>();
            VisualElement root = view.Root;
            PlayerController player = Object.FindAnyObjectByType<PlayerController>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            ItemInstance weapon = _architecture.GetModel<EquipmentModel>().GetWeapon(player.Actor);
            Assert.That(_architecture.SendQuery(new GetPlayerNormalSkillQuery()).IconGuid, Is.EqualTo(weapon.BaseDefinition.Icon.AssetGUID));
            yield return HoverNormal();
            Assert.That(root.Q("hud-normal-icon").resolvedStyle.backgroundImage.sprite,
                Is.SameAs(_architecture.GetUtility<SpriteAssetLoader>().GetSprite(weapon.BaseDefinition.Icon.AssetGUID)));
            _architecture.SendCommand(new RestoreNormalSkillCooldownCommand(.4f));
            hud.RefreshHud(); yield return null;
            ProgressBar mask = root.Q<ProgressBar>("skill-cooldown");
            Assert.That(mask.value, Is.EqualTo(hud.RuntimeSnapshot.CooldownNormalized).Within(.001f));
            VisualElement fill = mask.Q(className: "unity-progress-bar__progress");
            VisualElement background = mask.Q(className: "unity-progress-bar__background");
            Assert.That(fill.worldBound.width / background.worldBound.width, Is.EqualTo(mask.value).Within(.05f),
                "遮罩面积读取权威剩余比例");
            Assert.That(root.Q<Label>("hud-normal-time").text, Is.Not.Empty);
            float before = mask.value;
            yield return new WaitForSeconds(.15f);
            Assert.That(mask.value, Is.LessThan(before));
            GameplayPauseSystem pause = _architecture.GetSystem<GameplayPauseSystem>();
            pause.SetPaused(true); hud.RefreshHud();
            float frozen = mask.value;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(mask.value, Is.EqualTo(frozen).Within(.001f));
            Assert.That(root.Q<Label>("skill-status-label").text, Is.EqualTo(host.Localization.GetString("ui", "hud.state.paused")));
            pause.SetPaused(false);
            yield return new WaitForSeconds(.5f);
            Assert.That(mask.value, Is.Zero);
            Assert.That(root.Q<Label>("hud-normal-time").text, Is.Empty);
            ItemInstance axe = host.ContentCatalog.GetAll<ItemBaseDefinition>().Single(d => d.Id == "war_axe")
                .CreateInstance(_architecture.GetUtility<IItemInstanceIdGenerator>().Next(), 1, 21, ItemRarity.Normal);
            Assert.IsTrue(_architecture.GetModel<InventoryModel>().TryAddItem(axe));
            Assert.IsTrue(_architecture.SendCommand(new EquipItemCommand(player.Actor, axe)));
            Assert.That(root.Q("hud-skill-tooltip").style.display.value, Is.EqualTo(DisplayStyle.None), "来源替换关闭旧说明");
            yield return HoverNormal();
            yield return host.Localization.ChangeLanguageAsync(UserLanguagePreference.English).ToCoroutine();
            Assert.That(root.Q<Label>("hud-skill-name").text, Is.EqualTo(host.Localization.GetString("ui", "attributes.skill." + axe.BaseDefinition.NormalSkill.Id)));
            player.Actor.TrySpendMana(player.Actor.CurrentMana);
            hud.RefreshHud();
            Assert.That(root.Q<Label>("skill-status-label").text, Is.EqualTo(host.Localization.GetString("ui", "hud.state.no_mana")));
            Assert.That(root.Q<Label>("hud-detail-status").text, Is.EqualTo(host.Localization.GetString("ui", "attributes.state.no_mana")));
            _architecture.GetSystem<EquipmentSystem>().RestoreLoadout(player.Actor, new EquipmentLoadout());
            hud.RefreshHud();
            Assert.That(root.Q<Label>("skill-status-label").text, Is.EqualTo(host.Localization.GetString("ui", "hud.state.no_source")));
            Assert.That(root.Q("hud-normal-icon").ClassListContains("hud-skill-icon--missing"), Is.True);
            yield return HoverNormal();
            VisualElement oldDetails = root.Q("hud-skill-tooltip");
            hud.enabled = false;
            Assert.That(oldDetails.style.display.value, Is.EqualTo(DisplayStyle.None));
            hud.enabled = true; yield return null; yield return null;
            yield return HoverNormal();
            VisualTreeAsset source = view.Renderer.visualTreeAsset;
            VisualTreeAsset replacement = Object.Instantiate(source); _objects.Add(replacement);
            try
            {
                view.Renderer.visualTreeAsset = replacement;
                yield return null; yield return null;
                Assert.That(oldDetails.style.display.value, Is.EqualTo(DisplayStyle.None));
                Assert.That(oldDetails.panel, Is.Null);
                root = view.Root;
                Assert.That(root.Q("hud-skill-tooltip"), Is.Not.SameAs(oldDetails));
                yield return HoverNormal();
                Assert.That(root.Q("hud-skill-tooltip").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            }
            finally { view.Renderer.visualTreeAsset = source; }
            yield return host.Localization.ChangeLanguageAsync(UserLanguagePreference.SimplifiedChinese).ToCoroutine();

            IEnumerator HoverNormal()
            {
                SetSkillPointer(mouse, player, Vector2.up); yield return null; yield return null;
                _inputFixture.Set(mouse.position, PanelToScreen(root, root.Q("hud-normal").worldBound.center));
                yield return null; yield return null;
            }
        }
    }
}
