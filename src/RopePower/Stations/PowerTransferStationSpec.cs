using Timberborn.BlueprintSystem;
using UnityEngine;

namespace RopePower.Stations
{
    /// <summary>Blueprint data for a Power Transfer Station.</summary>
    public record PowerTransferStationSpec : ComponentSpec
    {
        /// <summary>Pulley top in local world space (Y up), same convention as ZiplineTowerSpec.CableAnchorPoint.</summary>
        [Serialize]
        public Vector3 RopeAnchorPoint { get; init; }

        /// <summary>
        /// Block coordinates of the rope-slot transputs (TransputProviderSpec entries at these coordinates facing Bottom).
        /// They face into the station itself, so vanilla never connects them; the GetFacingTransput patch pairs them.
        /// </summary>
        [Serialize]
        public Vector3Int RopeSlotCoordinates { get; init; }
    }
}
