using Timberborn.BlueprintSystem;

namespace RopePower.Ropes
{
    /// <summary>Global rope rules, read from Configurations/RopeConnectionService.blueprint.json.</summary>
    public record RopeConnectionServiceSpec : ComponentSpec
    {
        /// <summary>Max anchor-to-anchor distance in blocks (vanilla zipline MaxDistance is 30).</summary>
        [Serialize]
        public int MaxRopeSpan { get; init; }

        /// <summary>Must not exceed the number of rope-slot transputs in the station blueprint.</summary>
        [Serialize]
        public int MaxRopesPerStation { get; init; }

        /// <summary>Degrees; same rule as ZiplineConnectionService (vanilla MaxCableInclination is 50).</summary>
        [Serialize]
        public int MaxInclination { get; init; }
    }
}
