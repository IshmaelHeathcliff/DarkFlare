using System.Collections.Generic;

namespace DarkFlare
{
    public static class DamageCalculator
    {
        public static DamageResult Calculate(DamageContext context)
        {
            if (!context.IsHit)
            {
                return new DamageResult(false, context.IsCritical, new Dictionary<DamageType, float>(), new Dictionary<DamageType, float>());
            }

            List<DamagePacket> packets = new List<DamagePacket>(context.BaseDamages);

            packets = ApplyConversion(packets, context.AttackerModifiers, context.ContextTags);
            packets = ApplyGainAsExtra(packets, context.AttackerModifiers, context.ContextTags);
            packets = ApplyAttackerScaling(packets, context.AttackerStats, context.AttackerModifiers, context.ContextTags);

            if (context.IsCritical)
            {
                packets = ApplyCriticalDamage(packets, context.AttackerStats);
            }

            Dictionary<DamageType, float> beforeDefense = SumByType(packets);
            Dictionary<DamageType, float> afterDefense = ApplyDefense(beforeDefense, context.DefenderStats, context.DefenderModifiers, context.ContextTags);

            return new DamageResult(true, context.IsCritical, beforeDefense, afterDefense);
        }

        static List<DamagePacket> ApplyConversion(List<DamagePacket> packets, IEnumerable<ModifierInstance> modifiers, TagSet contextTags)
        {
            List<DamagePacket> result = new List<DamagePacket>();
            List<ModifierInstance> conversionModifiers = new List<ModifierInstance>();

            foreach (ModifierInstance modifier in modifiers)
            {
                if (modifier == null
                    || modifier.Operation != ModifierOperation.Conversion
                    || !IsAttackerModifier(modifier)
                    || !modifier.Matches(contextTags))
                {
                    continue;
                }

                conversionModifiers.Add(modifier);
            }

            for (int i = 0; i < packets.Count; i++)
            {
                DamagePacket packet = packets[i];
                float totalConversion = 0f;

                for (int j = 0; j < conversionModifiers.Count; j++)
                {
                    ModifierInstance modifier = conversionModifiers[j];

                    if (modifier.FromDamageType == packet.DamageType)
                    {
                        totalConversion += modifier.Value;
                    }
                }

                if (totalConversion <= 0f)
                {
                    result.Add(packet);
                    continue;
                }

                float scale = totalConversion > 100f ? 100f / totalConversion : 1f;
                float convertedTotal = 0f;

                for (int j = 0; j < conversionModifiers.Count; j++)
                {
                    ModifierInstance modifier = conversionModifiers[j];

                    if (modifier.FromDamageType != packet.DamageType)
                    {
                        continue;
                    }

                    float ratio = Clamp01(modifier.Value * scale / 100f);
                    float convertedAmount = packet.Amount * ratio;
                    convertedTotal += convertedAmount;
                    result.Add(new DamagePacket(modifier.ToDamageType, convertedAmount, packet.Tags));
                }

                result.Add(packet.WithAmount(packet.Amount - convertedTotal));
            }

            return result;
        }

        static List<DamagePacket> ApplyGainAsExtra(List<DamagePacket> packets, IEnumerable<ModifierInstance> modifiers, TagSet contextTags)
        {
            List<DamagePacket> result = new List<DamagePacket>(packets);

            foreach (ModifierInstance modifier in modifiers)
            {
                if (modifier == null
                    || modifier.Operation != ModifierOperation.GainAsExtra
                    || !IsAttackerModifier(modifier)
                    || !modifier.Matches(contextTags))
                {
                    continue;
                }

                float ratio = modifier.Value / 100f;

                for (int i = 0; i < packets.Count; i++)
                {
                    DamagePacket packet = packets[i];

                    if (packet.DamageType == modifier.FromDamageType && packet.Amount > 0f)
                    {
                        result.Add(new DamagePacket(modifier.ToDamageType, packet.Amount * ratio, packet.Tags));
                    }
                }
            }

            return result;
        }

        static List<DamagePacket> ApplyAttackerScaling(
            List<DamagePacket> packets,
            StatBlock attackerStats,
            IEnumerable<ModifierInstance> modifiers,
            TagSet contextTags)
        {
            List<DamagePacket> result = new List<DamagePacket>(packets.Count);

            for (int i = 0; i < packets.Count; i++)
            {
                DamagePacket packet = packets[i];
                TagSet tags = contextTags.Union(packet.Tags);
                float amount = packet.Amount;
                amount += GetFlatDamage(attackerStats, modifiers, tags, packet.DamageType);
                amount *= 1f + GetIncreasedDamage(modifiers, tags, packet.DamageType) / 100f;
                amount *= GetMoreDamageMultiplier(modifiers, tags, packet.DamageType);
                result.Add(packet.WithAmount(amount));
            }

            return result;
        }

        static float GetFlatDamage(
            StatBlock stats,
            IEnumerable<ModifierInstance> modifiers,
            TagSet tags,
            DamageType damageType)
        {
            string typedDamageStatId = GetDamageStatId(damageType);
            float value = stats.GetValue(typedDamageStatId);

            foreach (ModifierInstance modifier in modifiers)
            {
                if (modifier == null
                    || modifier.Operation != ModifierOperation.Flat
                    || !IsAttackerModifier(modifier)
                    || !modifier.Matches(tags))
                {
                    continue;
                }

                if (modifier.StatId == StatIds.Damage || modifier.StatId == typedDamageStatId)
                {
                    value += modifier.Value;
                }
            }

            return value;
        }

        static float GetIncreasedDamage(IEnumerable<ModifierInstance> modifiers, TagSet tags, DamageType damageType)
        {
            float value = 0f;

            foreach (ModifierInstance modifier in modifiers)
            {
                if (modifier == null
                    || modifier.Operation != ModifierOperation.Increase
                    || !IsAttackerModifier(modifier)
                    || !modifier.Matches(tags))
                {
                    continue;
                }

                if (modifier.StatId == StatIds.Damage || modifier.StatId == GetDamageStatId(damageType))
                {
                    value += modifier.Value;
                }
            }

            return value;
        }

        static float GetMoreDamageMultiplier(IEnumerable<ModifierInstance> modifiers, TagSet tags, DamageType damageType)
        {
            float multiplier = 1f;

            foreach (ModifierInstance modifier in modifiers)
            {
                if (modifier == null
                    || modifier.Operation != ModifierOperation.More
                    || !IsAttackerModifier(modifier)
                    || !modifier.Matches(tags))
                {
                    continue;
                }

                if (modifier.StatId == StatIds.Damage || modifier.StatId == GetDamageStatId(damageType))
                {
                    multiplier *= 1f + modifier.Value / 100f;
                }
            }

            return multiplier;
        }

        static List<DamagePacket> ApplyCriticalDamage(List<DamagePacket> packets, StatBlock attackerStats)
        {
            List<DamagePacket> result = new List<DamagePacket>(packets.Count);
            float criticalDamage = attackerStats.GetValue(StatIds.CriticalDamage);
            float multiplier = 1f + criticalDamage / 100f;

            for (int i = 0; i < packets.Count; i++)
            {
                result.Add(packets[i].WithAmount(packets[i].Amount * multiplier));
            }

            return result;
        }

        static Dictionary<DamageType, float> SumByType(IEnumerable<DamagePacket> packets)
        {
            Dictionary<DamageType, float> result = new Dictionary<DamageType, float>();

            foreach (DamagePacket packet in packets)
            {
                if (!result.ContainsKey(packet.DamageType))
                {
                    result[packet.DamageType] = 0f;
                }

                result[packet.DamageType] += packet.Amount;
            }

            return result;
        }

        static Dictionary<DamageType, float> ApplyDefense(
            Dictionary<DamageType, float> damageByType,
            StatBlock defenderStats,
            IEnumerable<ModifierInstance> defenderModifiers,
            TagSet contextTags)
        {
            Dictionary<DamageType, float> result = new Dictionary<DamageType, float>();

            foreach (KeyValuePair<DamageType, float> pair in damageByType)
            {
                float amount = pair.Value;
                amount *= GetTakenDamageMultiplier(pair.Key, defenderModifiers, contextTags);
                amount = ApplyResistance(amount, pair.Key, defenderStats);

                if (pair.Key == DamageType.Physical)
                {
                    amount = ApplyArmor(amount, defenderStats.GetValue(StatIds.Armor));
                }

                result[pair.Key] = amount < 0f ? 0f : amount;
            }

            return result;
        }

        static float GetTakenDamageMultiplier(DamageType damageType, IEnumerable<ModifierInstance> modifiers, TagSet contextTags)
        {
            float multiplier = 1f;

            foreach (ModifierInstance modifier in modifiers)
            {
                if (modifier == null || modifier.Scope != ModifierScope.TargetTaken || !modifier.Matches(contextTags))
                {
                    continue;
                }

                if (modifier.Operation == ModifierOperation.Increase || modifier.Operation == ModifierOperation.More)
                {
                    if (modifier.StatId == StatIds.Damage || modifier.StatId == GetDamageStatId(damageType))
                    {
                        multiplier *= 1f + modifier.Value / 100f;
                    }
                }
            }

            return multiplier;
        }

        static float ApplyResistance(float amount, DamageType damageType, StatBlock defenderStats)
        {
            string resistanceStatId = GetResistanceStatId(damageType);

            if (string.IsNullOrEmpty(resistanceStatId))
            {
                return amount;
            }

            float resistance = Clamp(defenderStats.GetValue(resistanceStatId), -100f, 75f);
            return amount * (1f - resistance / 100f);
        }

        static float ApplyArmor(float amount, float armor)
        {
            if (amount <= 0f || armor <= 0f)
            {
                return amount;
            }

            float reduction = armor / (armor + amount * 10f);
            return amount * (1f - reduction);
        }

        static string GetDamageStatId(DamageType damageType)
        {
            if (damageType == DamageType.Physical)
            {
                return StatIds.PhysicalDamage;
            }

            if (damageType == DamageType.Fire)
            {
                return StatIds.FireDamage;
            }

            if (damageType == DamageType.Cold)
            {
                return StatIds.ColdDamage;
            }

            if (damageType == DamageType.Lightning)
            {
                return StatIds.LightningDamage;
            }

            return StatIds.ChaosDamage;
        }

        static string GetResistanceStatId(DamageType damageType)
        {
            if (damageType == DamageType.Fire)
            {
                return StatIds.FireResistance;
            }

            if (damageType == DamageType.Cold)
            {
                return StatIds.ColdResistance;
            }

            if (damageType == DamageType.Lightning)
            {
                return StatIds.LightningResistance;
            }

            if (damageType == DamageType.Chaos)
            {
                return StatIds.ChaosResistance;
            }

            return string.Empty;
        }

        static bool IsAttackerModifier(ModifierInstance modifier)
        {
            return modifier.Scope == ModifierScope.GlobalActor
                || modifier.Scope == ModifierScope.Skill
                || modifier.Scope == ModifierScope.LocalItem;
        }

        static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        static float Clamp01(float value)
        {
            return Clamp(value, 0f, 1f);
        }
    }
}
