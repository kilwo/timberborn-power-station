using System;
using CablePowerTransfer.Stations;
using UnityEngine;

namespace CablePowerTransfer.Cables
{
    /// <summary>Order-independent identity of a cable between two stations (same idea as Timberborn.ZiplineSystem.CableKey).</summary>
    public readonly struct PowerCableKey : IEquatable<PowerCableKey>
    {
        public PowerTransferStation First { get; }
        public PowerTransferStation Second { get; }

        public PowerCableKey(PowerTransferStation a, PowerTransferStation b)
        {
            bool swap = Compare(a.Coordinates, b.Coordinates) > 0;
            First = swap ? b : a;
            Second = swap ? a : b;
        }

        public bool Equals(PowerCableKey other)
        {
            return First == other.First && Second == other.Second;
        }

        public override bool Equals(object obj)
        {
            return obj is PowerCableKey other && Equals(other);
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
