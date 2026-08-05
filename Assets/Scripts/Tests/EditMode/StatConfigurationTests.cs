using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFlare.Tests
{
    public class StatConfigurationTests
    {
        const string PresetPath = "Assets/Data/Preset/Stats";

        [Test]
        public void PresetDefinitions_MatchAllStatIdsAndPassValidation()
        {
            string[] guids = AssetDatabase.FindAssets("t:StatDefinition", new[] { PresetPath });
            List<StatDefinition> definitions = new List<StatDefinition>(guids.Length);
            List<string> ids = new List<string>(guids.Length);

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                StatDefinition definition = AssetDatabase.LoadAssetAtPath<StatDefinition>(path);
                Assert.IsNotNull(definition, $"无法加载属性资产 {path}");
                definitions.Add(definition);
                ids.Add(definition.Id);
            }

            List<string> issues = StatConfigurationValidator.Validate(definitions);
            Assert.IsEmpty(issues, string.Join("\n", issues));
            Assert.AreEqual(StatIds.All.Count, definitions.Count, "属性资产数量与 StatIds 不一致");
            CollectionAssert.AreEquivalent(StatIds.All, ids, "属性资产稳定 ID 与 StatIds 不一致");
        }

        [Test]
        public void CriticalDamageDefinition_UsesPercentageBonusSemantics()
        {
            StatDefinition definition = AssetDatabase.LoadAssetAtPath<StatDefinition>(
                $"{PresetPath}/07暴击倍率.asset");

            Assert.IsNotNull(definition);
            Assert.AreEqual(StatIds.CriticalDamage, definition.Id);
            Assert.AreEqual("暴击伤害", definition.DisplayName);
            Assert.AreEqual(0f, definition.DefaultValue);
            Assert.AreEqual(0f, definition.MinValue);
            Assert.IsTrue(definition.IsPercent);
        }

        [Test]
        public void Validator_ReportsNonCanonicalUnsupportedAndInvalidRange()
        {
            StatDefinition definition = ScriptableObject.CreateInstance<StatDefinition>();

            try
            {
                SetField(definition, "_id", "Health Value");
                SetField(definition, "_displayName", string.Empty);
                SetField(definition, "_defaultValue", 5f);
                SetField(definition, "_minValue", 10f);
                SetField(definition, "_maxValue", 1f);
                List<string> issues = StatConfigurationValidator.Validate(definition);

                Assert.IsTrue(issues.Exists(issue => issue.Contains("snake_case")));
                Assert.IsTrue(issues.Exists(issue => issue.Contains("未在 StatIds")));
                Assert.IsTrue(issues.Exists(issue => issue.Contains("中文名")));
                Assert.IsTrue(issues.Exists(issue => issue.Contains("最小值")));
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"缺少字段 {target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }
    }
}
