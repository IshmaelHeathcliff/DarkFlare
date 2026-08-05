using System.Collections.Generic;

namespace DarkFlare
{
    public static class StatConfigurationValidator
    {
        public static List<string> Validate(StatDefinition definition)
        {
            List<string> issues = new List<string>();

            if (definition == null)
            {
                issues.Add("属性定义为空");
                return issues;
            }

            if (string.IsNullOrWhiteSpace(definition.Id))
            {
                issues.Add("稳定 ID 不能为空");
            }
            else
            {
                if (!IsCanonicalId(definition.Id))
                {
                    issues.Add("稳定 ID 必须使用小写 snake_case");
                }

                if (!StatIds.IsSupported(definition.Id))
                {
                    issues.Add($"稳定 ID {definition.Id} 未在 StatIds 中登记");
                }
            }

            if (string.IsNullOrWhiteSpace(definition.DisplayName))
            {
                issues.Add("中文名不能为空");
            }

            if (definition.MinValue > definition.MaxValue)
            {
                issues.Add("最小值不能大于最大值");
            }
            else if (definition.DefaultValue < definition.MinValue
                     || definition.DefaultValue > definition.MaxValue)
            {
                issues.Add("默认值必须位于最小值和最大值之间");
            }

            return issues;
        }

        public static List<string> Validate(IReadOnlyList<StatDefinition> definitions)
        {
            List<string> issues = new List<string>();
            HashSet<string> ids = new HashSet<string>();

            if (definitions == null)
            {
                issues.Add("属性定义集合为空");
                return issues;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                StatDefinition definition = definitions[i];
                List<string> definitionIssues = Validate(definition);
                string label = definition != null ? definition.name : $"索引 {i}";

                for (int j = 0; j < definitionIssues.Count; j++)
                {
                    issues.Add($"{label}: {definitionIssues[j]}");
                }

                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    continue;
                }

                if (!ids.Add(definition.Id))
                {
                    issues.Add($"稳定 ID {definition.Id} 存在重复定义");
                }
            }

            for (int i = 0; i < StatIds.All.Count; i++)
            {
                string statId = StatIds.All[i];

                if (!ids.Contains(statId))
                {
                    issues.Add($"StatIds.{statId} 缺少 StatDefinition 资产");
                }
            }

            return issues;
        }

        static bool IsCanonicalId(string statId)
        {
            if (string.IsNullOrEmpty(statId) || statId[0] < 'a' || statId[0] > 'z')
            {
                return false;
            }

            for (int i = 1; i < statId.Length; i++)
            {
                char character = statId[i];
                bool isLowerLetter = character >= 'a' && character <= 'z';
                bool isDigit = character >= '0' && character <= '9';

                if (isLowerLetter || isDigit)
                {
                    continue;
                }

                if (character != '_'
                    || i == statId.Length - 1
                    || statId[i - 1] == '_')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
