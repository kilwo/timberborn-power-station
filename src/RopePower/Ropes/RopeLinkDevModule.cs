using System.Linq;
using RopePower.Stations;
using Timberborn.Debugging;

namespace RopePower.Ropes
{
    /// <summary>
    /// Temporary Phase 2 debug trigger, shown in the game's dev menu. Remove once the connection tool exists (Phase 4).
    /// Pattern copied from Timberborn.ZiplineSystemUI.ZiplineConnectionDevModule.
    /// </summary>
    public class RopeLinkDevModule : IDevModule
    {
        private readonly RopeConnectionService _ropeConnectionService;
        private readonly PowerTransferStationRegistry _registry;

        public RopeLinkDevModule(RopeConnectionService ropeConnectionService, PowerTransferStationRegistry registry)
        {
            _ropeConnectionService = ropeConnectionService;
            _registry = registry;
        }

        public DevModuleDefinition GetDefinition()
        {
            return new DevModuleDefinition.Builder()
                .AddMethod(DevMethod.Create("Rope Power: link two newest stations", LinkTwoNewest))
                .AddMethod(DevMethod.Create("Rope Power: link newest station to all others", LinkNewestToAll))
                .AddMethod(DevMethod.Create("Rope Power: unlink newest station", UnlinkNewest))
                .AddMethod(DevMethod.Create("Rope Power: log all rope links", LogAllLinks))
                .Build();
        }

        private void LinkTwoNewest()
        {
            PowerTransferStation[] newest = _registry.MostRecentlyFinished().Take(2).ToArray();
            if (newest.Length < 2)
            {
                ModLog.Info("dev: need at least two finished stations to link.");
                return;
            }
            _ropeConnectionService.Link(newest[1], newest[0]);
        }

        private void LinkNewestToAll()
        {
            PowerTransferStation[] stations = _registry.MostRecentlyFinished().ToArray();
            if (stations.Length < 2)
            {
                ModLog.Info("dev: need at least two finished stations to link.");
                return;
            }
            for (int i = 1; i < stations.Length; i++)
            {
                _ropeConnectionService.Link(stations[0], stations[i]);
            }
        }

        private void UnlinkNewest()
        {
            PowerTransferStation newest = _registry.MostRecentlyFinished().FirstOrDefault();
            if (!newest)
            {
                ModLog.Info("dev: no finished station.");
                return;
            }
            _ropeConnectionService.UnlinkAll(newest);
        }

        private void LogAllLinks()
        {
            var links = _ropeConnectionService.AllLinks(_registry.Stations).ToList();
            ModLog.Info($"dev: {_registry.Stations.Count} station(s), {links.Count} rope link(s)");
            foreach ((PowerTransferStation a, PowerTransferStation b) in links)
            {
                ModLog.Info($"dev:   {a.DebugName} <-> {b.DebugName}");
            }
        }
    }
}
