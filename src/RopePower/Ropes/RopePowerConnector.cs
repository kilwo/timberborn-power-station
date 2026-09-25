using RopePower.Stations;
using Timberborn.SingletonSystem;

namespace RopePower.Ropes
{
    /// <summary>
    /// Turns rope link changes into mechanical graph rebuilds. The actual connection is made by the game's
    /// MechanicalGraphManager via the TransputMap.GetFacingTransput patch; this only asks both ends to re-query it.
    /// </summary>
    public class RopePowerConnector : ILoadableSingleton
    {
        private readonly RopeConnectionService _ropeConnectionService;

        public RopePowerConnector(RopeConnectionService ropeConnectionService)
        {
            _ropeConnectionService = ropeConnectionService;
        }

        public void Load()
        {
            _ropeConnectionService.LinksChanged += OnLinksChanged;
        }

        private static void OnLinksChanged(PowerTransferStation station, PowerTransferStation other)
        {
            station.RefreshPowerConnections();
            other.RefreshPowerConnections();
        }
    }
}
