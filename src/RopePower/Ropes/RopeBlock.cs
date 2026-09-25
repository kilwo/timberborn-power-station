using Timberborn.BaseComponentSystem;
using Timberborn.BlueprintSystem;

namespace RopePower.Ropes
{
    /// <summary>Marks an invisible block entity reserving one cell along a rope (our equivalent of Timberborn.ZiplineSystem.CableBlock).</summary>
    public record RopeBlockSpec : ComponentSpec;

    public class RopeBlock : BaseComponent
    {
    }
}
