using System;
using System.Collections.Generic;

namespace DarkFlare
{
    public static class ItemGenerator
    {
        public static ItemInstance Generate(
            ItemBaseDefinition baseDefinition,
            IEnumerable<AffixDefinition> affixPool,
            ItemGenerationOptions options)
        {
            if (baseDefinition == null)
            {
                throw new System.ArgumentNullException(nameof(baseDefinition));
            }

            if (!ItemRarityRules.IsNormalGenerationValid(
                    options.Rarity,
                    options.PrefixCount,
                    options.SuffixCount))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    $"{options.Rarity} 不能生成 {options.PrefixCount} 前缀 / {options.SuffixCount} 后缀");
            }

            ItemInstance item = baseDefinition.CreateInstance(options.InstanceId, options.ItemLevel, options.Seed, options.Rarity);
            List<AffixType?> requestedTypes = new List<AffixType?>(options.PrefixCount + options.SuffixCount);

            for (int i = 0; i < options.PrefixCount; i++)
            {
                requestedTypes.Add(AffixType.Prefix);
            }

            for (int i = 0; i < options.SuffixCount; i++)
            {
                requestedTypes.Add(AffixType.Suffix);
            }

            bool generated = AffixGenerationUtility.TryGenerate(
                item,
                affixPool,
                options.Rarity,
                Array.Empty<AffixInstance>(),
                Array.Empty<AffixInstance>(),
                requestedTypes,
                options.Seed,
                options.Seed,
                out List<AffixInstance> affixes);

            if (!generated)
            {
                throw new InvalidOperationException(
                    $"{baseDefinition.Id} 无法完整生成 {options.PrefixCount} 前缀 / {options.SuffixCount} 后缀");
            }

            for (int i = 0; i < affixes.Count; i++)
            {
                if (!item.TryAddAffix(affixes[i]))
                {
                    throw new InvalidOperationException($"{baseDefinition.Id} 生成词缀时提交失败");
                }
            }

            return item;
        }
    }
}
