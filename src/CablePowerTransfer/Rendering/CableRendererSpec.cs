using Timberborn.BlueprintSystem;

namespace CablePowerTransfer.Rendering
{
    /// <summary>Cable look, read from Configurations/CableRenderer.blueprint.json (tunable without rebuilding).</summary>
    public record CableRendererSpec : ComponentSpec
    {
        /// <summary>Sag at mid-span as a fraction of the span length.</summary>
        [Serialize]
        public float SagPerLength { get; init; }

        /// <summary>Cap on sag in blocks. Kept under 0.5 so the drawn cable stays close to the cells reserved by PowerCableBlockService.</summary>
        [Serialize]
        public float MaxSag { get; init; }

        /// <summary>Straight cable pieces per strand used to approximate the curve.</summary>
        [Serialize]
        public int SegmentsPerStrand { get; init; }
    }
}
