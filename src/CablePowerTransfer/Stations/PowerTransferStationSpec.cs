using Timberborn.BlueprintSystem;
using UnityEngine;

namespace CablePowerTransfer.Stations
{
    /// <summary>Blueprint data for a Power Transfer Station.</summary>
    public record PowerTransferStationSpec : ComponentSpec
    {
        /// <summary>Pulley top in local world space (Y up), same convention as ZiplineTowerSpec.CableAnchorPoint.</summary>
        [Serialize]
        public Vector3 CableAnchorPoint { get; init; }

        /// <summary>
        /// Block coordinates of the cable-slot transputs (TransputProviderSpec entries at these coordinates facing Bottom).
        /// They face into the station itself, so vanilla never connects them; the GetFacingTransput patch pairs them.
        /// </summary>
        [Serialize]
        public Vector3Int CableSlotCoordinates { get; init; }

        /// <summary>Horizontal distance from the pulley centre to each cable strand, in blocks (0 = default 0.175, the zipline strand offset).</summary>
        [Serialize]
        public float PulleyRadius { get; init; }
    }
}
