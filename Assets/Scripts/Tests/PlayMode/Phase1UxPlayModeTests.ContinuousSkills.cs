using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DarkFlare.Tests
{
    public partial class Phase1UxPlayModeTests
    {
        [UnityTest]
        public IEnumerator ManualSkills_HoldRepeatsWithLatestAimAndReleaseStops()
        {
            yield return _fixture.EnterMain();
            ApplicationHost host = ApplicationHost.Current;
            IArchitecture architecture = host.CurrentSession.Architecture;
            PlayerController player = Object.FindAnyObjectByType<PlayerController>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            var headings = new List<Vector2>();
            IUnRegister registration = architecture.RegisterEvent<PlayerSkillCastEvent>(e =>
            {
                if (e.Actor != player.Actor) { return; }
                ProjectileController shot = architecture.GetUtility<SessionObjectRegistry>().CaptureObjects()
                    .Select(go => go.GetComponent<ProjectileController>()).First(p => p != null && p.Attack == e.Attack);
                headings.Add(shot.GetComponent<Rigidbody2D>().linearVelocity.normalized);
            });
            try
            {
                SetSkillPointer(mouse, player, Vector2.right);
                _inputFixture.Press(mouse.leftButton);
                yield return WaitForSkillCasts(() => headings.Count, 1);
                SetSkillPointer(mouse, player, Vector2.up);
                yield return WaitForSkillCasts(() => headings.Count, 2);
                Assert.Greater(Vector2.Dot(headings[0], Vector2.right), .99f);
                Assert.Greater(Vector2.Dot(headings[1], Vector2.up), .99f);
                _inputFixture.Release(mouse.leftButton);
                int released = headings.Count;
                yield return new WaitForSeconds(.75f);
                Assert.AreEqual(released, headings.Count, "松开不补发");
                _inputFixture.Set(pad.rightStick, Vector2.left);
                _inputFixture.Press(pad.buttonWest);
                yield return WaitForSkillCasts(() => headings.Count, released + 2);
                Assert.Greater(Vector2.Dot(headings.Last(), Vector2.left), .99f);
                _inputFixture.Release(pad.buttonWest);
            }
            finally { registration.UnRegister(); }
        }

        [UnityTest]
        public IEnumerator ManualSkills_HeldInputDoesNotResumeAfterUiPauseOrSuspension()
        {
            yield return _fixture.EnterMain();
            IArchitecture architecture = ApplicationHost.Current.CurrentSession.Architecture;
            GameMenuController menu = Object.FindAnyObjectByType<GameMenuController>();
            ApplicationInputService input = ApplicationHost.Current.Input;
            GameplayPauseSystem pause = architecture.GetSystem<GameplayPauseSystem>();
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            int casts = 0;
            int requests = 0;
            Action<PlayerSkillInput> onRequest = _ => requests++;
            input.SkillRequested += onRequest;
            IUnRegister registration = architecture.RegisterEvent<PlayerSkillCastEvent>(_ => casts++);
            Action[] interruptions =
            {
                () => { menu.OpenPage(GameMenuPage.Inventory); menu.ClosePage(GameMenuPage.Inventory); },
                () => { pause.SetPaused(true); pause.SetPaused(false); },
                () => { using (input.AcquireSuspension(InputSuspensionReason.FocusLost)) { } }
            };
            try
            {
                foreach (Action interrupt in interruptions)
                {
                    int before = casts;
                    int requested = requests;
                    _inputFixture.Press(pad.buttonWest);
                    yield return WaitForSkillCasts(() => casts, before + 1);
                    Assert.AreEqual(requested + 1, requests, "新的按下必须进入 Gameplay 输入");
                    interrupt();
                    Assert.IsTrue(input.IsGameplayEnabled, "中断结束应恢复 Gameplay");
                    Assert.IsFalse(pause.IsPaused, "中断结束应解除菜单暂停");
                    int interrupted = casts;
                    yield return new WaitForSeconds(.75f);
                    Assert.AreEqual(interrupted, casts, "跨上下文 / 暂停必须重新按下");
                    _inputFixture.Release(pad.buttonWest);
                    yield return null;
                }
            }
            finally { registration.UnRegister(); input.SkillRequested -= onRequest; }
        }

        [UnityTest]
        public IEnumerator ManualSkills_HeldSourceReplacementCancelsAndManaRecoveryAllowsValidHold()
        {
            yield return _fixture.EnterMain();
            ApplicationHost host = ApplicationHost.Current;
            IArchitecture architecture = host.CurrentSession.Architecture;
            PlayerController player = Object.FindAnyObjectByType<PlayerController>();
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            int casts = 0;
            bool suppressRegeneration = false;
            IUnRegister registration = architecture.RegisterEvent<PlayerSkillCastEvent>(_ => casts++);
            IUnRegister regeneration = architecture.RegisterEvent<ActorResourceChangedEvent>(e =>
            {
                if (suppressRegeneration && e.Actor == player.Actor && e.ResourceType == ActorResourceType.Mana
                    && e.Reason == ActorResourceChangeReason.Regeneration)
                {
                    player.Actor.TrySpendMana(player.Actor.CurrentMana);
                }
            });
            try
            {
                _inputFixture.Press(pad.buttonWest);
                yield return WaitForSkillCasts(() => casts, 1);
                ItemInstance sword = architecture.GetModel<EquipmentModel>().GetWeapon(player.Actor);
                ItemInstance axe = host.ContentCatalog.GetAll<ItemBaseDefinition>().Single(i => i.Id == "war_axe")
                    .CreateInstance(architecture.GetUtility<IItemInstanceIdGenerator>().Next(), 1, 11, ItemRarity.Normal);
                Assert.IsTrue(architecture.GetModel<InventoryModel>().TryAddItem(axe));
                Assert.IsTrue(architecture.SendCommand(new EquipItemCommand(player.Actor, axe)));
                Assert.IsTrue(architecture.SendCommand(new EquipItemCommand(player.Actor, sword)));
                yield return new WaitForSeconds(.75f);
                Assert.AreEqual(1, casts, "同帧换出再换回也不能复活旧保持意图");
                _inputFixture.Release(pad.buttonWest);
                suppressRegeneration = true;
                Assert.IsTrue(player.Actor.TrySpendMana(player.Actor.CurrentMana));
                _inputFixture.Press(pad.buttonWest);
                yield return new WaitForSeconds(.1f);
                Assert.AreEqual(1, casts, "不足一次成本不施法");
                suppressRegeneration = false;
                player.Actor.RestoreResources(new CombatResourceSnapshot(player.Actor.CurrentHealth, player.Actor.MaxHealth,
                    player.Actor.MaxMana, player.Actor.MaxMana), true);
                yield return WaitForSkillCasts(() => casts, 2);
                _inputFixture.Release(pad.buttonWest);
            }
            finally { registration.UnRegister(); regeneration.UnRegister(); }
        }

        [UnityTest]
        public IEnumerator ManualSkills_DeathStopsHoldAndRespawnAcceptsNewPress()
        {
            yield return _fixture.EnterMain();
            IArchitecture architecture = ApplicationHost.Current.CurrentSession.Architecture;
            PlayerController player = Object.FindAnyObjectByType<PlayerController>();
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            int casts = 0;
            IUnRegister registration = architecture.RegisterEvent<PlayerSkillCastEvent>(_ => casts++);
            try
            {
                _inputFixture.Press(pad.buttonWest);
                yield return WaitForSkillCasts(() => casts, 1);
                int seed = 1;
                while (AttackRandomRolls.FromRootSeed(seed).HitRoll >= .5f) { seed++; }
                var stats = new StatBlock();
                stats.SetValue(StatIds.Accuracy, 1000000f);
                var attack = new AttackSnapshot("held_skill_enemy", ActorTeam.Monster, "death", string.Empty, seed,
                    new[] { new DamagePacket(DamageType.Physical, 1000000f, TagSet.Empty) }, TagSet.Empty, stats, Array.Empty<ModifierInstance>());
                architecture.SendCommand(new ApplyDamageCommand(attack, player.Actor));
                Assert.IsFalse(player.Actor.IsAlive);
                float deadline = Time.realtimeSinceStartup + 8;
                while (!player.Actor.IsAlive && Time.realtimeSinceStartup < deadline) { yield return null; }
                Assert.IsTrue(player.Actor.IsAlive, "角色应按原有流程自动复活");
                yield return new WaitForSeconds(.75f);
                Assert.AreEqual(1, casts, "死亡前保持的按键在复活后不能自动施法");
                _inputFixture.Release(pad.buttonWest);
                yield return null;
                _inputFixture.Press(pad.buttonWest);
                yield return WaitForSkillCasts(() => casts, 2);
                _inputFixture.Release(pad.buttonWest);
            }
            finally { registration.UnRegister(); }
        }

        void SetSkillPointer(Mouse mouse, PlayerController player, Vector2 direction)
        {
            _inputFixture.Set(mouse.position, (Vector2)Camera.main.WorldToScreenPoint(player.transform.position + (Vector3)direction * 5));
        }

        static IEnumerator WaitForSkillCasts(Func<int> count, int expected)
        {
            float deadline = Time.realtimeSinceStartup + 5;
            while (count() < expected && Time.realtimeSinceStartup < deadline) { yield return null; }
            Assert.AreEqual(expected, count(), "等待技能释放次数超时或重复施法");
        }
    }
}
