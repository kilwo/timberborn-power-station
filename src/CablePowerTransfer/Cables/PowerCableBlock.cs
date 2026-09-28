using Timberborn.BaseComponentSystem;
using Timberborn.BlueprintSystem;

namespace CablePowerTransfer.Cables
{
    /// <summary>Marks an invisible block entity reserving one cell along a cable (our equivalent of Timberborn.ZiplineSystem.CableBlock).</summary>
    public record PowerCableBlockSpec : ComponentSpec;

    public class PowerCableBlock : BaseComponent
    {
    }
}
