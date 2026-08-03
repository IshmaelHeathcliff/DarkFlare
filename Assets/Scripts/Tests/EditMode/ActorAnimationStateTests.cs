using System.Linq;
using DarkFlare;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DarkFlare.Tests
{
    public class ActorAnimationStateTests
    {
        const string PlayerControllerPath = "Assets/Art/Animations/Controllers/Player.controller";
        const string MonsterControllerPath = "Assets/Art/Animations/Controllers/Monster_Basic.controller";
        const string PlayerPrefabPath = "Assets/Prefabs/Combat/Player.prefab";
        const string MonsterPrefabPath = "Assets/Prefabs/Combat/Monster_Basic.prefab";
        [TestCase(PlayerControllerPath)]
        [TestCase(MonsterControllerPath)]
        public void AnimatorController_ContainsReachableCombatStates(string path)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);

            Assert.IsNotNull(controller, path);
            AssertParameter(controller, "Moving", AnimatorControllerParameterType.Bool);
            AssertParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
            AssertParameter(controller, "Hit", AnimatorControllerParameterType.Trigger);
            AssertParameter(controller, "Death", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState attack = GetState(stateMachine, "Attack");
            AnimatorState hit = GetState(stateMachine, "Hit");
            AnimatorState death = GetState(stateMachine, "Death");

            AssertAnyStateTransition(stateMachine, attack, "Attack");
            AssertAnyStateTransition(stateMachine, hit, "Hit");
            AssertAnyStateTransition(stateMachine, death, "Death");
            AssertLocomotionReturns(attack);
            AssertLocomotionReturns(hit);
            Assert.GreaterOrEqual(((AnimationClip)attack.motion).length, 0.3f);
            Assert.GreaterOrEqual(((AnimationClip)hit.motion).length, 0.2f);
            Assert.GreaterOrEqual(((AnimationClip)death.motion).length, 0.6f);
        }

        [Test]
        public void ActorPrefabs_KeepDeathVisibleUntilAnimationFinishes()
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            GameObject monsterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterPrefabPath);

            Assert.IsNotNull(playerPrefab);
            Assert.IsNotNull(monsterPrefab);
            Assert.IsFalse(GetHideVisualOnDeath(playerPrefab));
            Assert.IsFalse(GetHideVisualOnDeath(monsterPrefab));

            MonsterController monster = monsterPrefab.GetComponent<MonsterController>();
            SerializedObject serializedMonster = new SerializedObject(monster);
            float delay = serializedMonster.FindProperty("_deathDespawnDelay").floatValue;
            Assert.GreaterOrEqual(delay, 0.6f);
        }

        static void AssertParameter(
            AnimatorController controller,
            string name,
            AnimatorControllerParameterType type)
        {
            AnimatorControllerParameter parameter = controller.parameters.FirstOrDefault(item => item.name == name);
            Assert.IsNotNull(parameter, $"缺少 Animator 参数: {name}");
            Assert.AreEqual(type, parameter.type, name);
        }

        static AnimatorState GetState(AnimatorStateMachine stateMachine, string name)
        {
            AnimatorState state = stateMachine.states
                .Select(item => item.state)
                .FirstOrDefault(item => item.name == name);
            Assert.IsNotNull(state, $"缺少 Animator 状态: {name}");
            return state;
        }

        static void AssertAnyStateTransition(
            AnimatorStateMachine stateMachine,
            AnimatorState destination,
            string parameter)
        {
            bool found = stateMachine.anyStateTransitions.Any(transition =>
                transition.destinationState == destination &&
                transition.conditions.Any(condition => condition.parameter == parameter));
            Assert.IsTrue(found, $"Any State 缺少到 {destination.name} 的 {parameter} 转换");
        }

        static void AssertLocomotionReturns(AnimatorState state)
        {
            string[] destinations = state.transitions
                .Where(transition => transition.destinationState != null)
                .Select(transition => transition.destinationState.name)
                .ToArray();
            CollectionAssert.Contains(destinations, "Idle", $"{state.name} 必须能返回 Idle");
            CollectionAssert.Contains(destinations, "Move", $"{state.name} 必须能返回 Move");
        }

        static bool GetHideVisualOnDeath(GameObject prefab)
        {
            CombatActor actor = prefab.GetComponent<CombatActor>();
            SerializedObject serializedActor = new SerializedObject(actor);
            return serializedActor.FindProperty("_hideVisualOnDeath").boolValue;
        }

    }
}
