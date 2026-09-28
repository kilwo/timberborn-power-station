using Timberborn.BlueprintSystem;

namespace CablePowerTransfer.Cables
{
    /// <summary>Global cable rules, read from Configurations/CableConnectionService.blueprint.json.</summary>
    public record CableConnectionServiceSpec : ComponentSpec
    {
        /// <summary>Max anchor-to-anchor distance in blocks (vanilla zipline MaxDistance is 30).</summary>
        [Serialize]
        public int MaxCableSpan { get; init; }

        /// <summary>Must not exceed the number of cable-slot transputs in the station blueprint.</summary>
        [Serialize]
        public int MaxCablesPerStation { get; init; }

        /// <summary>Degrees; same rule as ZiplineConnectionService (vanilla MaxCableInclination is 50).</summary>
        [Serialize]
        public int MaxInclination { get; init; }
    }
}
