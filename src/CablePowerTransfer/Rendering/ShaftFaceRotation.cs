using Timberborn.Coordinates;
using Timberborn.MechanicalSystem;

namespace CablePowerTransfer.Rendering
{
    /// <summary>
    /// Works out which way a neighbour visibly turns at the face touching one of our station's transputs, in the
    /// game's transput convention (<see cref="Transput.ReversedRotation"/>: false = Normal, true = Reversed, as seen
    /// from outside that face). Connected faces turn together when their flags are opposite.
    ///
    /// Version-sensitive: mirrors Timberborn.ModularShafts.ModularShaftVariantFinder.GetRotationFromTransputs and
    /// OptimizeForIgnoredRotation, plus TransputRotationExtensions.ReverseOrSetNormal (game 1.1.2.4). A modular shaft
    /// ignores faces touching a node that is neither a shaft nor a generator (our station), and shows such a face
    /// as the reverse of the face across from it (Normal if that one is unset).
    /// </summary>
    public static class ShaftFaceRotation
    {
        private enum Rotation
        {
            None,
            Normal,
            Reversed,
            Ignored
        }

        /// <summary>
        /// True if the neighbour's face is Reversed, false if Normal, null if the neighbour doesn't constrain us
        /// (not connected, not a shaft or generator, or a generator that ignores rotation).
        /// </summary>
        public static bool? NeighbourFaceReversed(Transput ours)
        {
            Transput theirs = ours.ConnectedTransput;
            if (theirs == null)
            {
                return null;
            }
            MechanicalNode neighbour = theirs.ParentNode;
            if (neighbour.IgnoreRotation)
            {
                return null;
            }
            if (neighbour.IsGenerator)
            {
                // Generator flags are fixed by their blueprints; shafts next to them adapt to them.
                return theirs.ReversedRotation;
            }
            if (!neighbour.IsShaft)
            {
                return null;
            }
            return ShaftFaceRotationTowardsUs(neighbour, theirs.BaseDirection) == Rotation.Reversed;
        }

        // OptimizeForIgnoredRotation resolves Down before Up and Right before Left, using the partner's raw value for
        // the first of each pair and its already-resolved value for the second.
        private static Rotation ShaftFaceRotationTowardsUs(MechanicalNode shaft, Direction3D face)
        {
            switch (face)
            {
                case Direction3D.Down:
                    return ReverseOrSetNormal(Raw(shaft, Direction3D.Up));
                case Direction3D.Right:
                    return ReverseOrSetNormal(Raw(shaft, Direction3D.Left));
                case Direction3D.Up:
                    return ReverseOrSetNormal(Resolved(Raw(shaft, Direction3D.Down), Raw(shaft, Direction3D.Up)));
                case Direction3D.Left:
                    return ReverseOrSetNormal(Resolved(Raw(shaft, Direction3D.Right), Raw(shaft, Direction3D.Left)));
                default:
                    return Raw(shaft, face);
            }
        }

        private static Rotation Resolved(Rotation value, Rotation partnerRaw)
        {
            return value == Rotation.Ignored ? ReverseOrSetNormal(partnerRaw) : value;
        }

        // GetRotationFromTransputs. (The vanilla GetRotation also maps an unconnected face next to a connectable block
        // to Ignored; we treat it as None, which only matters for shafts next to unfinished buildings.)
        private static Rotation Raw(MechanicalNode shaft, Direction3D direction)
        {
            foreach (Transput transput in shaft.Transputs)
            {
                if (transput.BaseDirection != direction)
                {
                    continue;
                }
                if (!transput.Connected)
                {
                    return Rotation.None;
                }
                MechanicalNode other = transput.ConnectedTransput.ParentNode;
                if (other.IgnoreRotation || (!other.IsGenerator && !other.IsShaft))
                {
                    return Rotation.Ignored;
                }
                return transput.ReversedRotation ? Rotation.Reversed : Rotation.Normal;
            }
            return Rotation.None;
        }

        private static Rotation ReverseOrSetNormal(Rotation rotation)
        {
            return rotation == Rotation.Normal ? Rotation.Reversed : Rotation.Normal;
        }
    }
}
