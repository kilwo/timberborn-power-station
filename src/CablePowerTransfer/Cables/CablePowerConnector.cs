using System.Collections.Generic;
using CablePowerTransfer.Stations;
using Timberborn.SingletonSystem;

namespace CablePowerTransfer.Cables
{
    /// <summary>
    /// Turns cable link changes into mechanical graph rebuilds. The actual connection is made by the game's
    /// MechanicalGraphManager via the TransputMap.GetFacingTransput patch; this only asks both ends to re-query it.
    /// </summary>
    public class CablePowerConnector : ILoadableSingleton, IUpdatableSingleton
    {
        private readonly CableConnectionService _cableConnectionService;
        private readonly PowerTransferStationRegistry _registry;
        // Links restored on load are logged on the first frame instead of as they're restored: restoring runs in
        // PostInitializeEntity, before the stations' nodes join their graphs, so the state would always read
        // "cable connected False/False" there. Entities are all loaded before the first UpdateSingleton, even while
        // the game is paused (a game tick may never come).
        private bool _loadLogged;
        // Stations that finished since the last frame; their cables are logged once their nodes have joined a graph.
        private readonly List<PowerTransferStation> _finishedStations = new List<PowerTransferStation>();

        public CablePowerConnector(CableConnectionService cableConnectionService, PowerTransferStationRegistry registry)
        {
            _cableConnectionService = cableConnectionService;
            _registry = registry;
        }

        public void Load()
        {
            _cableConnectionService.LinksChanged += OnLinksChanged;
            _registry.StationFinished += OnStationFinished;
        }

        public void UpdateSingleton()
        {
            if (!_loadLogged)
            {
                _loadLogged = true;
                _finishedStations.Clear();
                LogLoadedLinks();
                return;
            }
            foreach (PowerTransferStation station in _finishedStations)
            {
                if (station && !station.IsBeingDeleted)
                {
                    foreach (PowerTransferStation partner in station.CablePartners)
                    {
                        LogPowerState(station, partner);
                    }
                }
            }
            _finishedStations.Clear();
        }

        private void LogLoadedLinks()
        {
            var links = new HashSet<PowerCableKey>();
            int waiting = 0;
            foreach (PowerTransferStation station in _registry.Stations)
            {
                foreach (PowerTransferStation partner in station.CablePartners)
                {
                    if (links.Add(new PowerCableKey(station, partner)))
                    {
                        if (station.IsFinished && partner.IsFinished)
                        {
                            LogPowerState(station, partner);
                        }
                        else
                        {
                            waiting++;
                        }
                    }
                }
            }
            ModLog.Info($"after load: {links.Count} cable link(s) between {_registry.Stations.Count} station(s), " +
                        $"{waiting} waiting for a station to be built");
        }

        private void OnStationFinished(PowerTransferStation station)
        {
            if (_loadLogged && station.CablePartners.Count > 0)
            {
                _finishedStations.Add(station);
            }
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
