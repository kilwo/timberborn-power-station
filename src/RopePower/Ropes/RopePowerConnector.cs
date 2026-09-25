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
            LogPowerState(station, other);
        }

        /// <summary>Test evidence in Player.log (replaces the Phase 2/3 Ctrl+Alt+K diagnostic).</summary>
        private static void LogPowerState(PowerTransferStation station, PowerTransferStation other)
        {
            if (!station.IsFinished || !other.IsFinished || station.IsBeingDeleted || other.IsBeingDeleted)
            {
                return;
            }
            bool sameNetwork = station.PowerGraph != null && station.PowerGraph == other.PowerGraph;
            ModLog.Info($"power {station.DebugName} <-> {other.DebugName}: linked {station.IsLinkedTo(other)}, " +
                        $"rope connected {station.IsRopeConnectedTo(other)}/{other.IsRopeConnectedTo(station)}, same network {sameNetwork}, " +
                        $"{station.DebugName} network supply {station.PowerGraph?.PowerSupply ?? 0} hp / demand {station.PowerGraph?.PowerDemand ?? 0} hp, " +
                        $"{other.DebugName} network supply {other.PowerGraph?.PowerSupply ?? 0} hp / demand {other.PowerGraph?.PowerDemand ?? 0} hp");
        }
    }
}
