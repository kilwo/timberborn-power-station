using System;
using RopePower.Stations;
using UnityEngine;

namespace RopePower.Ropes
{
    /// <summary>Order-independent identity of a rope between two stations (same idea as Timberborn.ZiplineSystem.CableKey).</summary>
    public readonly struct RopeKey : IEquatable<RopeKey>
    {
        public PowerTransferStation First { get; }
        public PowerTransferStation Second { get; }

        public RopeKey(PowerTransferStation a, PowerTransferStation b)
        {
            bool swap = Compare(a.Coordinates, b.Coordinates) > 0;
            First = swap ? b : a;
            Second = swap ? a : b;
        }

        public bool Equals(RopeKey other)
        {
            return First == other.First && Second == other.Second;
        }

        public override bool Equals(object obj)
        {
            return obj is RopeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (First.GetHashCode() * 397) ^ Second.GetHashCode();
        }

        private static int Compare(Vector3Int a, Vector3Int b)
        {
            if (a.x != b.x) return a.x.CompareTo(b.x);
            if (a.y != b.y) return a.y.CompareTo(b.y);
            return a.z.CompareTo(b.z);
        }
    }
}
