using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public sealed class Alpha013CollisionSafetyPlayModeTests
    {
        readonly List<GameObject> _objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                {
                    Object.DestroyImmediate(_objects[i]);
                }
            }

            _objects.Clear();
        }

        [UnityTest]
        public IEnumerator PlayerPassesThroughMonsterCrowd_ButWorldObstacleStillBlocksMovement()
        {
            int playerLayer = LayerMask.NameToLayer(GameplayPhysicsLayers.PlayerActor);
            int monsterLayer = LayerMask.NameToLayer(GameplayPhysicsLayers.MonsterActor);
            int worldLayer = LayerMask.NameToLayer(GameplayPhysicsLayers.WorldObstacle);
            Assert.GreaterOrEqual(playerLayer, 0);
            Assert.GreaterOrEqual(monsterLayer, 0);
            Assert.GreaterOrEqual(worldLayer, 0);
            Vector2[] directions =
            {
                Vector2.right,
                Vector2.left,
                Vector2.up,
                Vector2.down,
            };

            for (int directionIndex = 0; directionIndex < directions.Length; directionIndex++)
            {
                Vector2 direction = directions[directionIndex];
                Vector2 perpendicular = new Vector2(-direction.y, direction.x);
                Vector2 origin = new Vector2(1000f + directionIndex * 20f, 1000f);
                Rigidbody2D playerBody = CreateDynamicActor(
                    $"CollisionSafetyPlayer_{directionIndex}",
                    playerLayer,
                    origin,
                    RigidbodyConstraints2D.FreezeRotation);

                for (int column = 0; column < 4; column++)
                {
                    for (int row = -1; row <= 1; row++)
                    {
                        CreateDynamicActor(
                            $"CollisionSafetyMonster_{directionIndex}_{column}_{row}",
                            monsterLayer,
                            origin
                            + direction * (1.2f + column * 0.65f)
                            + perpendicular * (row * 0.65f),
                            RigidbodyConstraints2D.FreezeAll);
                    }
                }

                Physics2D.SyncTransforms();
                yield return new WaitForFixedUpdate();
                playerBody.linearVelocity = direction * 5f;

                for (int i = 0; i < 40; i++)
                {
                    yield return new WaitForFixedUpdate();
                }

                float forwardDistance = Vector2.Dot(playerBody.position - origin, direction);
                Assert.GreaterOrEqual(
                    forwardDistance,
                    3.6f,
                    $"玩家沿 {direction} 移动时未达到无阻挡位移的 90%");
                playerBody.linearVelocity = Vector2.zero;
                playerBody.constraints = RigidbodyConstraints2D.FreezeAll;
            }

            Vector2 obstacleOrigin = new Vector2(1100f, 1100f);
            Rigidbody2D obstacleTestBody = CreateDynamicActor(
                "WorldObstaclePlayer",
                playerLayer,
                obstacleOrigin,
                RigidbodyConstraints2D.FreezeRotation);
            CreateWorldObstacle(obstacleOrigin + new Vector2(5f, 0f), worldLayer);
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            obstacleTestBody.linearVelocity = Vector2.right * 5f;

            for (int i = 0; i < 70; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.LessOrEqual(
                obstacleTestBody.position.x,
                obstacleOrigin.x + 4.55f,
                "玩家应继续被 WorldObstacle 实体边界阻挡");
        }

        [UnityTest]
        public IEnumerator DefaultLayerTrigger_StillReceivesPlayerAndMonsterActors()
        {
            int playerLayer = LayerMask.NameToLayer(GameplayPhysicsLayers.PlayerActor);
            int monsterLayer = LayerMask.NameToLayer(GameplayPhysicsLayers.MonsterActor);
            Vector2 origin = new Vector2(2000f, 2000f);
            GameObject triggerObject = new GameObject("CollisionSafetyTrigger");
            triggerObject.layer = 0;
            triggerObject.transform.position = origin;
            BoxCollider2D triggerCollider = triggerObject.AddComponent<BoxCollider2D>();
            triggerCollider.isTrigger = true;
            triggerCollider.size = new Vector2(2f, 2f);
            Alpha013TriggerProbe probe = triggerObject.AddComponent<Alpha013TriggerProbe>();
            _objects.Add(triggerObject);
            Rigidbody2D playerBody = CreateDynamicActor(
                "TriggerPlayer",
                playerLayer,
                origin + Vector2.left * 2f,
                RigidbodyConstraints2D.FreezeRotation);
            Rigidbody2D monsterBody = CreateDynamicActor(
                "TriggerMonster",
                monsterLayer,
                origin + Vector2.right * 2f,
                RigidbodyConstraints2D.FreezeRotation);
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            playerBody.linearVelocity = Vector2.right * 4f;
            monsterBody.linearVelocity = Vector2.left * 4f;

            for (int i = 0; i < 40 && (!probe.PlayerEntered || !probe.MonsterEntered); i++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.IsTrue(probe.PlayerEntered, "Default 层触发器未收到 PlayerActor");
            Assert.IsTrue(probe.MonsterEntered, "Default 层触发器未收到 MonsterActor");
        }

        Rigidbody2D CreateDynamicActor(
            string name,
            int layer,
            Vector2 position,
            RigidbodyConstraints2D constraints)
        {
            GameObject actor = new GameObject(name);
            actor.layer = layer;
            actor.transform.position = position;
            Rigidbody2D body = actor.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.constraints = constraints;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            CircleCollider2D collider = actor.AddComponent<CircleCollider2D>();
            collider.radius = 0.35f;
            _objects.Add(actor);
            return body;
        }

        void CreateWorldObstacle(Vector2 position, int layer)
        {
            GameObject obstacle = new GameObject("CollisionSafetyWorldObstacle");
            obstacle.layer = layer;
            obstacle.transform.position = position;
            BoxCollider2D collider = obstacle.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.4f, 5f);
            _objects.Add(obstacle);
        }
    }

    public sealed class Alpha013TriggerProbe : MonoBehaviour
    {
        public bool PlayerEntered { get; private set; }

        public bool MonsterEntered { get; private set; }

        void OnTriggerEnter2D(Collider2D other)
        {
            PlayerEntered |= other.gameObject.layer == LayerMask.NameToLayer(GameplayPhysicsLayers.PlayerActor);
            MonsterEntered |= other.gameObject.layer == LayerMask.NameToLayer(GameplayPhysicsLayers.MonsterActor);
        }
    }
}
