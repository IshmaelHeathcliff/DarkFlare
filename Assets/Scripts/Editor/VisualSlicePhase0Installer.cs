using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkFlare.Editor
{
    public static class VisualSlicePhase0Installer
    {
        const string AttackParameter = "Attack";
        const string DeathParameter = "Death";
        const string HitParameter = "Hit";
        const string MovingParameter = "Moving";
        const string MainScenePath = "Assets/Scenes/Main.unity";
        const string GroundPath = "Assets/Art/Sprites/Environment/VisualSlice/ground_slice.png";
        const string PlayerRoot = "Assets/Art/Sprites/Characters/Player";
        const string MonsterRoot = "Assets/Art/Sprites/Characters/Monsters/Basic";
        const string ProjectilePath = "Assets/Art/Sprites/Effects/projectile_arcane.png";
        const string GreatswordPath = "Assets/Art/Sprites/Items/Equipment/weapon_greatsword.png";
        const string UiRoot = "Assets/Art/Sprites/UI";
        const string PlayerControllerPath = "Assets/Art/Animations/Controllers/Player.controller";
        const string MonsterControllerPath = "Assets/Art/Animations/Controllers/Monster_Basic.controller";

        static readonly string[] PlayerIdlePaths = BuildFramePaths(PlayerRoot, "player_idle_se");
        static readonly string[] PlayerMovePaths = BuildFramePaths(PlayerRoot, "player_move_se");
        static readonly string[] MonsterIdlePaths = BuildFramePaths(MonsterRoot, "monster_basic_idle_se");
        static readonly string[] MonsterMovePaths = BuildFramePaths(MonsterRoot, "monster_basic_move_se");

        [MenuItem("Tools/DarkFlare/视觉切片/安装阶段 0")]
        public static void InstallFromMenu()
        {
            Install();
        }

        static void Install()
        {
            ConfigureImports();

            AnimationClip playerIdle = CreateClip(
                "Assets/Art/Animations/Clips/Player_Idle.anim",
                PlayerIdlePaths,
                true);
            AnimationClip playerMove = CreateClip(
                "Assets/Art/Animations/Clips/Player_Move.anim",
                PlayerMovePaths,
                true);
            AnimationClip playerAttack = CreateClip(
                "Assets/Art/Animations/Clips/Player_Attack.anim",
                new[]
                {
                    $"{PlayerRoot}/Preview/player_attack_anticipation_se.png",
                    $"{PlayerRoot}/Preview/player_attack_release_se.png",
                },
                false,
                0.3f);
            AnimationClip playerHit = CreateClip(
                "Assets/Art/Animations/Clips/Player_Hit.anim",
                new[] { $"{PlayerRoot}/Preview/player_hit_se.png" },
                false,
                0.2f);
            AnimationClip playerDeath = CreateClip(
                "Assets/Art/Animations/Clips/Player_Death.anim",
                new[] { $"{PlayerRoot}/Preview/player_death_se.png" },
                false,
                0.6f);

            AnimationClip monsterIdle = CreateClip(
                "Assets/Art/Animations/Clips/Monster_Basic_Idle.anim",
                MonsterIdlePaths,
                true);
            AnimationClip monsterMove = CreateClip(
                "Assets/Art/Animations/Clips/Monster_Basic_Move.anim",
                MonsterMovePaths,
                true);
            AnimationClip monsterAttack = CreateClip(
                "Assets/Art/Animations/Clips/Monster_Basic_Attack.anim",
                new[]
                {
                    $"{MonsterRoot}/Preview/monster_basic_attack_anticipation_se.png",
                    $"{MonsterRoot}/Preview/monster_basic_attack_release_se.png",
                },
                false,
                0.3f);
            AnimationClip monsterHit = CreateClip(
                "Assets/Art/Animations/Clips/Monster_Basic_Hit.anim",
                new[] { $"{MonsterRoot}/Preview/monster_basic_hit_se.png" },
                false,
                0.2f);
            AnimationClip monsterDeath = CreateClip(
                "Assets/Art/Animations/Clips/Monster_Basic_Death.anim",
                new[] { $"{MonsterRoot}/Preview/monster_basic_death_se.png" },
                false,
                0.6f);

            AnimatorController playerController = ConfigureAnimatorController(
                PlayerControllerPath,
                playerIdle,
                playerMove,
                playerAttack,
                playerHit,
                playerDeath);
            AnimatorController monsterController = ConfigureAnimatorController(
                MonsterControllerPath,
                monsterIdle,
                monsterMove,
                monsterAttack,
                monsterHit,
                monsterDeath);

            ConfigureActorPrefab(
                "Assets/Prefabs/Combat/Player.prefab",
                PlayerIdlePaths[0],
                playerController,
                10);
            ConfigureActorPrefab(
                "Assets/Prefabs/Combat/Monster_Basic.prefab",
                MonsterIdlePaths[0],
                monsterController,
                10);
            ConfigureSpritePrefab("Assets/Prefabs/Combat/Projectile_Default.prefab", ProjectilePath, 20);
            ConfigureSpritePrefab("Assets/Prefabs/Loot/LootPickup.prefab", GreatswordPath, 5);
            ConfigureGroundScene();

            AssetDatabase.SaveAssets();
            Debug.Log("[VisualSlicePhase0] 视觉切片已完成导入、动画、Prefab 与 Main 场景接入");
        }

        static void ConfigureImports()
        {
            ConfigureSpriteImport(GroundPath, 64f, new Vector2(0.5f, 0.5f), TextureWrapMode.Repeat);

            foreach (string path in EnumerateCharacterPaths(PlayerRoot).Concat(EnumerateCharacterPaths(MonsterRoot)))
            {
                ConfigureSpriteImport(path, 64f, new Vector2(0.5f, 0.12f), TextureWrapMode.Clamp);
            }

            ConfigureSpriteImport(ProjectilePath, 64f, new Vector2(0.5f, 0.5f), TextureWrapMode.Clamp);
            ConfigureSpriteImport(GreatswordPath, 64f, new Vector2(0.5f, 0.5f), TextureWrapMode.Clamp);

            foreach (string path in new[]
                     {
                         $"{UiRoot}/ui_inventory_panel.png",
                         $"{UiRoot}/ui_slot_normal.png",
                         $"{UiRoot}/ui_slot_focus.png",
                         $"{UiRoot}/ui_slot_disabled.png",
                     })
            {
                ConfigureSpriteImport(path, 100f, new Vector2(0.5f, 0.5f), TextureWrapMode.Clamp);
            }
        }

        static IEnumerable<string> EnumerateCharacterPaths(string root)
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { root });
            return guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
        }

        static void ConfigureSpriteImport(string path, float pixelsPerUnit, Vector2 pivot, TextureWrapMode wrapMode)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer == null)
            {
                throw new InvalidOperationException($"无法读取 Sprite 导入器: {path}");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = wrapMode;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }

        static AnimationClip CreateClip(
            string path,
            IReadOnlyList<string> spritePaths,
            bool loop,
            float minimumDuration = 0f)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }

            clip.frameRate = 10f;
            List<ObjectReferenceKeyframe> keyframes = new List<ObjectReferenceKeyframe>();

            for (int i = 0; i < spritePaths.Count; i++)
            {
                Sprite sprite = LoadSprite(spritePaths[i]);
                keyframes.Add(new ObjectReferenceKeyframe
                {
                    time = i / 10f,
                    value = sprite,
                });
            }

            if (spritePaths.Count > 0)
            {
                Sprite terminalSprite = loop ? LoadSprite(spritePaths[0]) : LoadSprite(spritePaths[spritePaths.Count - 1]);
                float terminalTime = Mathf.Max(Math.Max(1, spritePaths.Count) / 10f, minimumDuration);
                keyframes.Add(new ObjectReferenceKeyframe
                {
                    time = terminalTime,
                    value = terminalSprite,
                });
            }

            EditorCurveBinding binding = new EditorCurveBinding
            {
                path = string.Empty,
                type = typeof(SpriteRenderer),
                propertyName = "m_Sprite",
            };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes.ToArray());
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        static AnimatorController ConfigureAnimatorController(
            string path,
            AnimationClip idle,
            AnimationClip move,
            AnimationClip attack,
            AnimationClip hit,
            AnimationClip death)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);

            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            }

            foreach (AnimatorControllerParameter parameter in controller.parameters.ToArray())
            {
                controller.RemoveParameter(parameter);
            }

            controller.AddParameter(MovingParameter, AnimatorControllerParameterType.Bool);
            controller.AddParameter(AttackParameter, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(HitParameter, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(DeathParameter, AnimatorControllerParameterType.Trigger);
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

            foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions.ToArray())
            {
                stateMachine.RemoveAnyStateTransition(transition);
            }

            foreach (ChildAnimatorState child in stateMachine.states.ToArray())
            {
                stateMachine.RemoveState(child.state);
            }

            AnimatorState idleState = stateMachine.AddState("Idle", new Vector3(200f, 100f));
            idleState.motion = idle;
            AnimatorState moveState = stateMachine.AddState("Move", new Vector3(450f, 100f));
            moveState.motion = move;
            stateMachine.defaultState = idleState;

            AnimatorStateTransition toMove = idleState.AddTransition(moveState);
            toMove.hasExitTime = false;
            toMove.duration = 0.05f;
            toMove.AddCondition(AnimatorConditionMode.If, 0f, MovingParameter);

            AnimatorStateTransition toIdle = moveState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.05f;
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, MovingParameter);

            AnimatorState attackState = stateMachine.AddState("Attack", new Vector3(200f, 260f));
            attackState.motion = attack;
            AnimatorState hitState = stateMachine.AddState("Hit", new Vector3(450f, 260f));
            hitState.motion = hit;
            AnimatorState deathState = stateMachine.AddState("Death", new Vector3(700f, 260f));
            deathState.motion = death;

            AddAnyStateTriggerTransition(stateMachine, deathState, DeathParameter);
            AddAnyStateTriggerTransition(stateMachine, hitState, HitParameter);
            AddAnyStateTriggerTransition(stateMachine, attackState, AttackParameter);
            AddLocomotionExitTransitions(attackState, idleState, moveState);
            AddLocomotionExitTransitions(hitState, idleState, moveState);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static void AddAnyStateTriggerTransition(
            AnimatorStateMachine stateMachine,
            AnimatorState destination,
            string parameter)
        {
            AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(destination);
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = 0.02f;
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.If, 0f, parameter);
        }

        static void AddLocomotionExitTransitions(
            AnimatorState source,
            AnimatorState idle,
            AnimatorState move)
        {
            AnimatorStateTransition toIdle = source.AddTransition(idle);
            toIdle.hasExitTime = true;
            toIdle.exitTime = 1f;
            toIdle.hasFixedDuration = true;
            toIdle.duration = 0.03f;
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, MovingParameter);

            AnimatorStateTransition toMove = source.AddTransition(move);
            toMove.hasExitTime = true;
            toMove.exitTime = 1f;
            toMove.hasFixedDuration = true;
            toMove.duration = 0.03f;
            toMove.AddCondition(AnimatorConditionMode.If, 0f, MovingParameter);
        }

        static void ConfigureActorPrefab(
            string prefabPath,
            string spritePath,
            RuntimeAnimatorController controller,
            int sortingOrder)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();

                if (renderer == null)
                {
                    renderer = root.AddComponent<SpriteRenderer>();
                }

                renderer.sprite = LoadSprite(spritePath);
                renderer.color = Color.white;
                renderer.sortingOrder = sortingOrder;

                Animator animator = root.GetComponent<Animator>();

                if (animator == null)
                {
                    animator = root.AddComponent<Animator>();
                }

                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;

                ActorAnimatorController visualController = root.GetComponent<ActorAnimatorController>();

                if (visualController == null)
                {
                    visualController = root.AddComponent<ActorAnimatorController>();
                }

                SerializedObject serializedController = new SerializedObject(visualController);
                serializedController.FindProperty("_animator").objectReferenceValue = animator;
                serializedController.FindProperty("_renderer").objectReferenceValue = renderer;
                serializedController.ApplyModifiedPropertiesWithoutUndo();

                CombatActor actor = root.GetComponent<CombatActor>();

                if (actor != null)
                {
                    SerializedObject serializedActor = new SerializedObject(actor);
                    serializedActor.FindProperty("_hideVisualOnDeath").boolValue = false;
                    serializedActor.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void ConfigureSpritePrefab(string prefabPath, string spritePath, int sortingOrder)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();

                if (renderer == null)
                {
                    renderer = root.AddComponent<SpriteRenderer>();
                }

                renderer.sprite = LoadSprite(spritePath);
                renderer.color = Color.white;
                renderer.sortingOrder = sortingOrder;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void ConfigureGroundScene()
        {
            Scene scene = SceneManager.GetActiveScene();

            if (!scene.IsValid() || scene.path != MainScenePath)
            {
                Debug.LogWarning("[VisualSlicePhase0] 当前未打开 Main.unity，跳过地表场景接入");
                return;
            }

            GameObject root = scene.GetRootGameObjects().FirstOrDefault(item => item.name == "VisualSliceGround");

            if (root == null)
            {
                root = new GameObject("VisualSliceGround");
                SceneManager.MoveGameObjectToScene(root, scene);
            }

            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;
            Sprite groundSprite = LoadSprite(GroundPath);

            for (int y = -1; y <= 1; y++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    string childName = $"Ground_{x + 1}_{y + 1}";
                    Transform child = root.transform.Find(childName);

                    if (child == null)
                    {
                        GameObject tile = new GameObject(childName);
                        child = tile.transform;
                        child.SetParent(root.transform, false);
                    }

                    child.localPosition = new Vector3(x * 8f, y * 8f, 0f);
                    child.localRotation = Quaternion.identity;
                    child.localScale = Vector3.one;
                    SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();

                    if (renderer == null)
                    {
                        renderer = child.gameObject.AddComponent<SpriteRenderer>();
                    }

                    renderer.sprite = groundSprite;
                    renderer.color = Color.white;
                    renderer.sortingOrder = -100;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static Sprite LoadSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (sprite == null)
            {
                throw new InvalidOperationException($"无法加载 Sprite: {path}");
            }

            return sprite;
        }

        static string[] BuildFramePaths(string root, string prefix)
        {
            return Enumerable.Range(0, 4)
                .Select(index => $"{root}/{prefix}_{index}.png")
                .ToArray();
        }

    }
}
