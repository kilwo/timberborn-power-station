using System.Collections.Generic;
using CablePowerTransfer.Stations;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;

namespace CablePowerTransfer.Cables
{
    /// <summary>
    /// Turns cable link changes into mechanical graph rebuilds. The actual connection is made by the game's
    /// MechanicalGraphManager via the TransputMap.GetFacingTransput patch; this only asks both ends to re-query it.
    /// </summary>
    public class CablePowerConnector : ILoadableSingleton, ITickableSingleton
    {
        private readonly CableConnectionService _cableConnectionService;
        private readonly PowerTransferStationRegistry _registry;
        // Links restored on load are logged on the first tick instead of as they're restored: restoring runs in
        // PostInitializeEntity, before the stations' nodes join their graphs, so the state would always read
        // "cable connected False/False" there. By the first tick the graphs are built and supply/demand updated.
        private bool _loadLogged;

        public CablePowerConnector(CableConnectionService cableConnectionService, PowerTransferStationRegistry registry)
        {
            _cableConnectionService = cableConnectionService;
            _registry = registry;
        }

        public void Load()
        {
            _cableConnectionService.LinksChanged += OnLinksChanged;
        }

        public void Tick()
        {
            if (_loadLogged)
            {
                return;
            }
            _loadLogged = true;
            var logged = new HashSet<PowerCableKey>();
            foreach (PowerTransferStation station in _registry.Stations)
            {
                foreach (PowerTransferStation partner in station.CablePartners)
                {
                    if (logged.Add(new PowerCableKey(station, partner)))
                    {
                        LogPowerState(station, partner);
                    }
                }
            }
            ModLog.Info($"after load: {logged.Count} cable link(s) between {_registry.Stations.Count} station(s)");
        }

        private void OnLinksChanged(PowerTransferStation station, PowerTransferStation other)
        {
            station.RefreshPowerConnections();
            other.RefreshPowerConnections();
            if (_loadLogged)
            {
                LogPowerState(station, other);
            }
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
                        $"cable connected {station.IsCableConnectedTo(other)}/{other.IsCableConnectedTo(station)}, same network {sameNetwork}, " +
                        $"{station.DebugName} network supply {station.PowerGraph?.PowerSupply ?? 0} hp / demand {station.PowerGraph?.PowerDemand ?? 0} hp, " +
                        $"{other.DebugName} network supply {other.PowerGraph?.PowerSupply ?? 0} hp / demand {other.PowerGraph?.PowerDemand ?? 0} hp");
        }
    }
}
