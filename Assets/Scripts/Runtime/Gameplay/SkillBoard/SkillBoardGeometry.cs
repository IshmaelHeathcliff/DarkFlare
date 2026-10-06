using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace DarkFlare
{
    public enum FirewoodSize
    {
        [LabelText("大柴薪")] Large,
        [LabelText("中柴薪")] Medium,
        [LabelText("小柴薪")] Small,
    }

    [Flags]
    public enum HexDirectionMask
    {
        [LabelText("无")] None = 0,
        [LabelText("东")] East = 1,
        [LabelText("东南")] SouthEast = 2,
        [LabelText("西南")] SouthWest = 4,
        [LabelText("西")] West = 8,
        [LabelText("西北")] NorthWest = 16,
        [LabelText("东北")] NorthEast = 32,
    }

    public static class SkillBoardGeometry
    {
        public const int RootDistance = 16;
        public const string CenterNodeId = "center";
        static readonly IReadOnlyList<HexCoordinate> Directions = Array.AsReadOnly(new[]
        {
            new HexCoordinate(1, 0), new HexCoordinate(0, 1), new HexCoordinate(-1, 1),
            new HexCoordinate(-1, 0), new HexCoordinate(0, -1), new HexCoordinate(1, -1),
        });
        static readonly IReadOnlyList<HexCoordinate> LargeBody = BuildDisc(2);
        static readonly IReadOnlyList<HexCoordinate> MediumBody = BuildDisc(1);
        static readonly IReadOnlyList<HexCoordinate> SmallBody = Array.AsReadOnly(new[]
        {
            new HexCoordinate(0, 0), new HexCoordinate(1, 0), new HexCoordinate(0, 1),
        });
        static readonly IReadOnlyList<HexCoordinate> LargeRing = BuildRing(2);
        static readonly IReadOnlyList<HexCoordinate> MediumRing = BuildRing(1);
        static readonly IReadOnlyList<HexCoordinate> RootPositions = BuildRoots();
        static readonly IReadOnlyList<HexDirectionMask> SmallDirections = Array.AsReadOnly(new[]
        {
            HexDirectionMask.West | HexDirectionMask.NorthWest,
            HexDirectionMask.East | HexDirectionMask.NorthEast,
            HexDirectionMask.SouthEast | HexDirectionMask.SouthWest,
        });

        public static IReadOnlyList<HexCoordinate> NeighborDirections => Directions;
        public static IReadOnlyList<HexCoordinate> CenterReservation => MediumBody;
        public static IReadOnlyList<HexCoordinate> Roots => RootPositions;
        public static IReadOnlyList<HexDirectionMask> SmallOutwardDirections => SmallDirections;

        public static IReadOnlyList<HexCoordinate> Body(FirewoodSize size)
        {
            switch (size)
            {
                case FirewoodSize.Large: return LargeBody;
                case FirewoodSize.Medium: return MediumBody;
                case FirewoodSize.Small: return SmallBody;
                default: throw new ArgumentOutOfRangeException(nameof(size));
            }
        }

        public static IReadOnlyList<HexCoordinate> Ring(FirewoodSize size)
        {
            switch (size)
            {
                case FirewoodSize.Large: return LargeRing;
                case FirewoodSize.Medium: return MediumRing;
                case FirewoodSize.Small: return SmallBody;
                default: throw new ArgumentOutOfRangeException(nameof(size));
            }
        }

        public static bool CanConnect(FirewoodSize first, FirewoodSize second)
        {
            return first != second || first == FirewoodSize.Small;
        }

        public static int OutwardDirection(FirewoodSize size, HexCoordinate position)
        {
            int radius = size == FirewoodSize.Large ? 2 : 1;
            for (int i = 0; i < Directions.Count; i++)
            {
                if (Directions[i].Scale(radius).Equals(position)) { return i; }
            }
            return -1;
        }

        static IReadOnlyList<HexCoordinate> BuildDisc(int radius)
        {
            List<HexCoordinate> result = new List<HexCoordinate>();
            for (int q = -radius; q <= radius; q++)
            {
                for (int r = -radius; r <= radius; r++)
                {
                    var coordinate = new HexCoordinate(q, r);
                    if (coordinate.DistanceTo(default) <= radius) { result.Add(coordinate); }
                }
            }
            return result.AsReadOnly();
        }

        static IReadOnlyList<HexCoordinate> BuildRing(int radius)
        {
            List<HexCoordinate> result = new List<HexCoordinate>();
            for (int i = 0; i < 6; i++)
            {
                HexCoordinate start = Directions[i].Scale(radius);
                HexCoordinate step = Directions[(i + 2) % 6];
                for (int j = 0; j < radius; j++) { result.Add(start.Add(step.Scale(j))); }
            }
            return result.AsReadOnly();
        }

        static IReadOnlyList<HexCoordinate> BuildRoots()
        {
            List<HexCoordinate> result = new List<HexCoordinate>();
            foreach (HexCoordinate direction in Directions) { result.Add(direction.Scale(RootDistance)); }
            return result.AsReadOnly();
        }
    }
}
