using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DarkFlare
{
    [CreateAssetMenu(menuName = "DarkFlare/技能面板/柴薪生成配置", fileName = "FirewoodGeneration")]
    public sealed class FirewoodGenerationDefinition : ScriptableObject
    {
        [SerializeField, LabelText("柴薪尺寸")] FirewoodSize _size;
        [SerializeField, LabelText("小柴薪顶点外伸方向"), ShowIf(nameof(IsSmall))]
        HexDirectionMask[] _smallPortDirections =
        {
            HexDirectionMask.West | HexDirectionMask.NorthWest,
            HexDirectionMask.East | HexDirectionMask.NorthEast,
            HexDirectionMask.SouthEast | HexDirectionMask.SouthWest,
        };

        public FirewoodSize Size => _size;
        bool IsSmall => _size == FirewoodSize.Small;

        public FirewoodGenerationResult Generate(int seed, int nodes = 0, int cracks = 0, int inner = 0)
        {
            IReadOnlyList<SkillBoardIssue> issues = ValidateConfiguration();
            if (issues.Count > 0) { return new FirewoodGenerationResult(null, issues); }
            return FirewoodGenerator.Generate(_size, seed, nodes, cracks, inner, _smallPortDirections);
        }

        public IReadOnlyList<SkillBoardIssue> ValidateConfiguration()
        {
            List<SkillBoardIssue> issues = new List<SkillBoardIssue>();
            if (!Enum.IsDefined(typeof(FirewoodSize), _size)) { issues.Add(new SkillBoardIssue("size", "柴薪尺寸无效", "_size")); }
            if (_size == FirewoodSize.Small)
            {
                if (_smallPortDirections == null || _smallPortDirections.Length != 3)
                {
                    issues.Add(new SkillBoardIssue("small_template", "小柴薪必须配置三个顶点的外伸方向", "_smallPortDirections"));
                }
                else
                {
                    for (int i = 0; i < 3; i++)
                    {
                        if (_smallPortDirections[i] == HexDirectionMask.None
                            || ((int)_smallPortDirections[i] & ~(int)SkillBoardGeometry.SmallOutwardDirections[i]) != 0)
                        {
                            issues.Add(new SkillBoardIssue("small_template", "方向必须向三角形外伸", $"_smallPortDirections[{i}]"));
                        }
                    }
                }
            }
            return issues.AsReadOnly();
        }

        void OnValidate()
        {
            foreach (SkillBoardIssue issue in ValidateConfiguration()) { ApplicationLog.Warning(LogEventIds.DataValidation, $"[{name}] {issue}", this); }
        }
    }
}
