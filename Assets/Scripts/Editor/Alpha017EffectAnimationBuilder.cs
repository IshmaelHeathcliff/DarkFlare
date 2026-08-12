using System;
using UnityEditor;
using UnityEngine;

namespace DarkFlare.Editor
{
    public static class Alpha017EffectAnimationBuilder
    {
        public const string ClipRoot = "Assets/Art/Animations/Effects";

        static readonly ClipDefinition[] ClipDefinitions =
        {
            new ClipDefinition(
                "Projectile_Arcane_Flight",
                "projectile-arcane-flight",
                true),
            new ClipDefinition(
                "Projectile_Arcane_Impact",
                "projectile-arcane-impact",
                false),
            new ClipDefinition(
                "Actor_Hit_Default",
                "actor-hit-default",
                false),
            new ClipDefinition(
                "Actor_Hit_Critical",
                "actor-hit-critical",
                false),
        };

        [MenuItem("DarkFlare/Alpha 0.1.7/构建候选特效动画")]
        public static void BuildClips()
        {
            EnsureFolder(ClipRoot);

            for (int i = 0; i < ClipDefinitions.Length; i++)
            {
                BuildClip(ClipDefinitions[i]);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Alpha017EffectAnimationBuilder] 已构建 {ClipDefinitions.Length} 个候选特效动画。");
        }

        public static string GetClipPath(string familyId)
        {
            for (int i = 0; i < ClipDefinitions.Length; i++)
            {
                if (string.Equals(ClipDefinitions[i].FamilyId, familyId, StringComparison.Ordinal))
                {
                    return ClipDefinitions[i].ClipPath;
                }
            }

            throw new ArgumentException($"未知特效家族：{familyId}", nameof(familyId));
        }

        static void BuildClip(ClipDefinition definition)
        {
            Alpha017EffectFamilyContract family = FindFamily(definition.FamilyId);
            ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[family.FrameCount + 1];

            for (int frameIndex = 0; frameIndex < family.FrameCount; frameIndex++)
            {
                string framePath = family.GetFramePath(frameIndex);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(framePath);

                if (sprite == null)
                {
                    throw new InvalidOperationException($"特效帧未正确导入为 Sprite：{framePath}");
                }

                keys[frameIndex] = new ObjectReferenceKeyframe
                {
                    time = frameIndex / (float)family.FramesPerSecond,
                    value = sprite,
                };
            }

            keys[family.FrameCount] = new ObjectReferenceKeyframe
            {
                time = family.FrameCount / (float)family.FramesPerSecond,
                value = keys[family.FrameCount - 1].value,
            };

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(definition.ClipPath);

            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, definition.ClipPath);
            }

            clip.name = definition.ClipName;
            clip.frameRate = family.FramesPerSecond;

            EditorCurveBinding binding = new EditorCurveBinding
            {
                path = string.Empty,
                type = typeof(SpriteRenderer),
                propertyName = "m_Sprite",
            };

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            SerializedObject serializedClip = new SerializedObject(clip);
            SerializedProperty loopTime = serializedClip.FindProperty("m_AnimationClipSettings.m_LoopTime");

            if (loopTime == null)
            {
                throw new InvalidOperationException($"无法设置动画循环合同：{definition.ClipPath}");
            }

            loopTime.boolValue = definition.Loop;
            serializedClip.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(clip);
        }

        static Alpha017EffectFamilyContract FindFamily(string familyId)
        {
            for (int i = 0; i < Alpha017VisualMigrationPreflight.EffectFamilies.Count; i++)
            {
                Alpha017EffectFamilyContract family = Alpha017VisualMigrationPreflight.EffectFamilies[i];

                if (string.Equals(family.Id, familyId, StringComparison.Ordinal))
                {
                    return family;
                }
            }

            throw new InvalidOperationException($"缺少特效家族合同：{familyId}");
        }

        static void EnsureFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];

            for (int i = 1; i < segments.Length; i++)
            {
                string next = $"{current}/{segments[i]}";

                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }

        readonly struct ClipDefinition
        {
            public string ClipName { get; }

            public string ClipPath => $"{ClipRoot}/{ClipName}.anim";

            public string FamilyId { get; }

            public bool Loop { get; }

            public ClipDefinition(string clipName, string familyId, bool loop)
            {
                ClipName = clipName;
                FamilyId = familyId;
                Loop = loop;
            }
        }
    }
}
