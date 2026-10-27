using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DarkFlare.Tests
{
    public partial class Phase1UxPlayModeTests
    {
        [UnityTest]
        public IEnumerator Board_MultipleLinksRequireReviewCancelAndExplicitCommit()
        {
            yield return _fixture.EnterMain();
            GameMenuController menu = UnityEngine.Object.FindAnyObjectByType<GameMenuController>();
            _architecture = menu.GetArchitecture();
            ItemInstance closing = PrepareBoardLoop();
            VisualElement root = menu.GetComponent<RuntimePanelView>().Root;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            menu.OpenPage(GameMenuPage.SkillBoard);
            yield return null; yield return null;
            string original = BoardIdentity(_architecture.SendQuery(new GetSkillBoardQuery()));
            for (int attempt = 0; attempt < 2; attempt++)
            {
                menu.Board.Jump(new HexCoordinate(17, 3));
                FindItemButton(root, closing, "inventory-item").Focus(); yield return null;
                yield return BoardPress(root, "board-take", keyboard);
                for (int i = 0; i < 2; i++)
                { _inputFixture.PressAndRelease(pad.rightShoulder); yield return null; yield return null; }
                yield return BoardPadSubmit(pad);
                Assert.That(menu.Board.IsReviewingLinks, Is.True);
                Toggle[] links = root.Q("board-links").Query<Toggle>().ToList().ToArray();
                Assert.That(links.Length, Is.EqualTo(2), "闭环安装必须显示两个真实候选");
                float deadline = Time.realtimeSinceStartup + 2;
                while (root.focusController.focusedElement != links[0] && Time.realtimeSinceStartup < deadline) { yield return null; }
                Assert.That(root.focusController.focusedElement, Is.SameAs(links[0]));
                AssertVisibleTextFits(root, ApplicationHost.Current.Localization.CurrentLocaleCode, new Vector2Int(Screen.width, Screen.height));
                Assert.That(BoardIdentity(_architecture.SendQuery(new GetSkillBoardQuery())), Is.EqualTo(original));
                if (attempt == 0)
                {
                    _inputFixture.PressAndRelease(pad.buttonEast); yield return null; yield return null;
                    Assert.That(menu.Board.IsReviewingLinks, Is.False);
                    Assert.That(_architecture.GetModel<InventoryModel>().Grid.Placements.ContainsKey(closing), Is.True);
                    continue;
                }
                _inputFixture.PressAndRelease(keyboard.enterKey); yield return null; yield return null;
                Assert.That(links[0].value, Is.True);
                Assert.That(root.Q<Button>("board-confirm").enabledSelf, Is.False, "只选一条会留下外伸冲突，不能提交半笔事务");
                yield return BoardPadDirection(pad, Vector2.down);
                Assert.That(root.focusController.focusedElement, Is.SameAs(links[1]));
                _inputFixture.PressAndRelease(keyboard.enterKey); yield return null; yield return null;
                Assert.That(links[1].value, Is.True);
                Assert.That(root.Q<Button>("board-confirm").enabledSelf, Is.True);
                Assert.That(_architecture.GetModel<InventoryModel>().Grid.Placements.ContainsKey(closing), Is.True);
                yield return BoardPress(root, "board-confirm", keyboard);
            }
            SkillBoardState final = _architecture.SendQuery(new GetSkillBoardQuery());
            Assert.That(final.Firewoods.Count, Is.EqualTo(4));
            Assert.That(final.Connections.Count, Is.EqualTo(4));
            Assert.That(_architecture.GetModel<InventoryModel>().Grid.Placements.ContainsKey(closing), Is.False);
            string identity = BoardIdentity(final);
            ApplicationHost host = ApplicationHost.Current;
            SaveOperationResult saved = null;
            yield return host.SessionSaveFacade.SaveAutoAsync().ToCoroutine(result => saved = result);
            Assert.That(saved.Succeeded, Is.True);
            yield return host.SceneFlow.RequestAsync(SceneFlowRequest.ReturnToFrontEnd()).ToCoroutine();
            SceneFlowResult continued = null;
            yield return host.SceneFlow.RequestAsync(SceneFlowRequest.StartGame(GameStartIntent.Continue)).ToCoroutine(result => continued = result);
            Assert.That(continued.Succeeded, Is.True);
            menu = UnityEngine.Object.FindAnyObjectByType<GameMenuController>();
            Assert.That(BoardIdentity(menu.GetArchitecture().SendQuery(new GetSkillBoardQuery())), Is.EqualTo(identity));
        }

        ItemInstance PrepareBoardLoop()
        {
            var turn = new FirewoodLayout(FirewoodSize.Small, 999, FirewoodGenerator.ContractVersion,
                new[]
                {
                    new FirewoodNode("a", new HexCoordinate(0, 0), FirewoodNodeKind.Crack, new HexCoordinate(-1, 0)),
                    new FirewoodNode("b", new HexCoordinate(1, 0), FirewoodNodeKind.Crack, new HexCoordinate(1, -1)),
                    new FirewoodNode("c", new HexCoordinate(0, 1), FirewoodNodeKind.Talent)
                },
                new[] { new FirewoodEdge("a", "b"), new FirewoodEdge("b", "c"), new FirewoodEdge("c", "a") });
            var mirror = new FirewoodLayout(FirewoodSize.Small, 998, FirewoodGenerator.ContractVersion,
                turn.Nodes.Select(n => new FirewoodNode(n.Id, new HexCoordinate(n.Position.R, n.Position.Q), n.Kind,
                    n.Outward.HasValue ? new HexCoordinate(n.Outward.Value.R, n.Outward.Value.Q) : (HexCoordinate?)null)), turn.Edges);
            FirewoodLayout[] layouts = { FirewoodGenerator.Generate(FirewoodSize.Large, 1, crackCount: 6).Layout,
                turn, FirewoodGenerator.Generate(FirewoodSize.Small, 3, crackCount: 2).Layout, mirror };
            HexCoordinate[] anchors = { new HexCoordinate(16, 0), new HexCoordinate(19, 1), new HexCoordinate(19, 2) };
            int[] rotations = { 0, 4, 1 };
            ItemInstance closing = null;
            PlayerController player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            for (int i = 0; i < layouts.Length; i++)
            {
                FirewoodLayout layout = layouts[i];
                ItemBaseDefinition definition = ApplicationHost.Current.ContentCatalog.GetAll<ItemBaseDefinition>()
                    .Single(d => d.Id == (layout.Size == FirewoodSize.Large ? "firewood_large" : "firewood_small"));
                ItemInstance generated = definition.CreateInstance(_architecture.GetUtility<IItemInstanceIdGenerator>().Next(), 1, 41, ItemRarity.Normal);
                var item = new ItemInstance(generated.Id, definition, ItemRarity.Normal, 1, layout.Seed, Array.Empty<ModifierInstance>());
                ModifierInstance modifier = generated.SkillBoardState.Talents[0].Modifiers[0];
                item.InitializeSkillBoardState(new SkillBoardItemState(layout, layout.Nodes.Where(n => n.Kind == FirewoodNodeKind.Talent)
                    .Select(n => new SkillBoardTalentState(n.Id, 0, new[] { modifier }))));
                Assert.That(_architecture.GetModel<InventoryModel>().TryAddItem(item), Is.True);
                if (i == layouts.Length - 1) { closing = item; break; }
                SkillBoardState board = _architecture.SendQuery(new GetSkillBoardQuery());
                FirewoodPlacement[] placements = board.Firewoods.Select(w => w.Placement)
                    .Append(new FirewoodPlacement(item.InstanceId, layout, anchors[i], rotations[i])).ToArray();
                SkillBoardPreview preview = _architecture.SendQuery(new PreviewSkillBoardQuery(player.Id.Value,
                    new SkillBoardRequest(SkillBoardOperation.Install, item, anchors[i], rotations[i],
                        connections: SkillBoardPlacementValidator.FindConnections(placements))));
                Assert.That(preview.Success, Is.True, preview.Error);
                Assert.That(_architecture.SendCommand(new CommitSkillBoardCommand(preview)), Is.True);
            }
            return closing;
        }
    }
}
