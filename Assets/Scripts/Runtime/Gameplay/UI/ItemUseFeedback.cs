namespace DarkFlare
{
    public static class ItemUseFeedback
    {
        public static string GetKey(ItemUseResult result, ItemUseDefinition definition)
        {
            if (result == null) { return "item.use.unavailable"; }
            switch (result.Code)
            {
                case ItemUseCode.Success: return null;
                case ItemUseCode.NoChange:
                    if (definition?.Effects.Count == 1)
                    {
                        switch (definition.Effects[0].Kind)
                        {
                            case ItemUseEffectKind.Heal: return "item.use.health_full";
                            case ItemUseEffectKind.RestoreMana: return "item.use.mana_full";
                            case ItemUseEffectKind.Dispel: return "item.use.no_ailment";
                        }
                    }
                    return "item.use.no_change";
                case ItemUseCode.ActionBlocked: return "item.use.blocked";
                case ItemUseCode.InsufficientQuantity: return "item.use.quantity";
                case ItemUseCode.InvalidConfiguration: return "item.use.configuration";
                case ItemUseCode.NotUsable: return "item.use.not_usable";
                case ItemUseCode.StatusRejected:
                    switch (result.StatusCode)
                    {
                        case StatusResultCode.Capacity: return "item.use.status_capacity";
                        case StatusResultCode.Protected: return "item.use.status_protected";
                        case StatusResultCode.InsufficientStacks: return "item.use.status_stacks";
                        case StatusResultCode.RuleConflict: return "item.use.status_conflict";
                        case StatusResultCode.InvalidRequest: return "item.use.configuration";
                        case StatusResultCode.Busy: return "item.use.busy";
                        case StatusResultCode.StaleVersion:
                        case StatusResultCode.AlreadyCommitted:
                        case StatusResultCode.InvalidTarget: return "item.action.stale";
                        default: return "item.use.status_rejected";
                    }
                case ItemUseCode.Busy: return "item.use.busy";
                case ItemUseCode.Unavailable: return "item.use.unavailable";
                default: return "item.action.stale";
            }
        }
    }
}
