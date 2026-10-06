using System;
using System.Globalization;

namespace DarkFlare
{
    public readonly struct HexCoordinate : IEquatable<HexCoordinate>, IComparable<HexCoordinate>
    {
        public int Q { get; }
        public int R { get; }
        public int S => checked((int)(-(long)Q - R));

        public HexCoordinate(int q, int r)
        {
            checked { _ = (int)(-(long)q - r); }
            Q = q;
            R = r;
        }

        public int DistanceTo(HexCoordinate other)
        {
            long q = (long)Q - other.Q;
            long r = (long)R - other.R;
            return checked((int)Math.Max(Math.Max(Math.Abs(q), Math.Abs(r)), Math.Abs(q + r)));
        }

        public HexCoordinate Rotate(int steps)
        {
            int normalized = (steps % 6 + 6) % 6;
            HexCoordinate result = this;
            for (int i = 0; i < normalized; i++)
            {
                result = new HexCoordinate(checked(-result.R), checked(result.Q + result.R));
            }
            return result;
        }

        public HexCoordinate Add(HexCoordinate other)
        {
            return new HexCoordinate(checked(Q + other.Q), checked(R + other.R));
        }

        public HexCoordinate Subtract(HexCoordinate other)
        {
            return new HexCoordinate(checked(Q - other.Q), checked(R - other.R));
        }

        public HexCoordinate Scale(int multiplier)
        {
            return new HexCoordinate(checked(Q * multiplier), checked(R * multiplier));
        }

        public bool Equals(HexCoordinate other)
        {
            return Q == other.Q && R == other.R;
        }

        public override bool Equals(object obj)
        {
            return obj is HexCoordinate other && Equals(other);
        }

        public override int GetHashCode()
        {
            return unchecked(Q * 397 ^ R);
        }

        public int CompareTo(HexCoordinate other)
        {
            int result = Q.CompareTo(other.Q);
            return result != 0 ? result : R.CompareTo(other.R);
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "({0}, {1})", Q, R);
        }
    }
}
