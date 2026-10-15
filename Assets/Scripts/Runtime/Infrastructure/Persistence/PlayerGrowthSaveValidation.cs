using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkFlare
{
    public static partial class SaveDataValidator
    {
        static void ValidateGrowth(ProfileSaveData profile, IReadOnlyDictionary<ItemInstanceId, ItemInstanceDto> items,
            string path, List<SaveDataIssue> issues)
        {
            PlayerGrowthState state = RuntimeStateMapper.ReadPlayerGrowth(profile.Growth, out string error);
            if (error != null) { Add(issues, SaveDataIssueCode.InvalidValue, path, error); return; }
            if (!RuntimeStateMapper.GrowthOwnerMatches(profile.Growth, state, profile.PlayerId))
            {
                Add(issues, SaveDataIssueCode.InvalidOwnership, path, "成长属于其他玩家");
            }
            if (state.Activations.Count == 0) { return; }
            SkillBoardSaveDto board = profile.SkillBoard;
            if (board?.Firewoods == null || board.Connections == null) { return; }
            var placements = new List<FirewoodPlacement>();
            try
            {
                foreach (InstalledFirewoodDto wood in board.Firewoods)
                {
                    if (wood == null || !ItemInstanceId.TryParse(wood.ItemInstanceId, out ItemInstanceId id)
                        || !items.TryGetValue(id, out ItemInstanceDto item)) { return; }
                    FirewoodLayout layout = ReadSavedLayout(item.SkillBoard?.Layout, path, issues);
                    if (layout == null) { return; }
                    placements.Add(new FirewoodPlacement(wood.ItemInstanceId, layout, new HexCoordinate(wood.Q, wood.R), wood.Rotation));
                }
                if (board.Connections.Any(c => c == null)) { return; }
                var connections = board.Connections.Select(c => new FirewoodConnection(
                    new FirewoodPortReference(c.FirstFirewoodId, c.FirstNodeId),
                    new FirewoodPortReference(c.SecondFirewoodId, c.SecondNodeId))).ToArray();
                error = PlayerGrowthGraph.Validate(state, placements, connections);
                if (error != null) { Add(issues, SaveDataIssueCode.InvalidValue, path, error); }
            }
            catch (ArgumentException) { Add(issues, SaveDataIssueCode.InvalidValue, path, "成长引用的棋盘损坏"); }
            catch (OverflowException) { Add(issues, SaveDataIssueCode.InvalidValue, path, "成长棋盘坐标溢出"); }
        }
    }
}
