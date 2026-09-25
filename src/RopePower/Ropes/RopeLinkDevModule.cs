using System.Collections.Generic;
using System.Linq;
using RopePower.Stations;
using Timberborn.Debugging;
using Timberborn.InputSystem;
using Timberborn.QuickNotificationSystem;
using Timberborn.SingletonSystem;

namespace RopePower.Ropes
{
    /// <summary>
    /// Temporary Phase 2 debug trigger. Remove once the connection tool exists (Phase 4).
    /// Ctrl+Alt shortcuts (mod/KeyBindings/Dev) work with or without dev mode; the same actions are in the dev panel
    /// (bottom-left in dev mode, click its title to expand). Results are shown as on-screen notifications.
    /// Pattern: Timberborn.ZiplineSystemUI.ZiplineConnectionDevModule + Timberborn.Debugging.DevModeController.
    /// </summary>
    public class RopeLinkDevModule : IDevModule, ILoadableSingleton, IPriorityInputProcessor
    {
        private const string LinkTwoNewestKey = "RopePowerLinkTwoNewest";
        private const string LinkNewestToAllKey = "RopePowerLinkNewestToAll";
        private const string UnlinkNewestKey = "RopePowerUnlinkNewest";
        private const string LogLinksKey = "RopePowerLogLinks";

        private readonly RopeConnectionService _ropeConnectionService;
        private readonly PowerTransferStationRegistry _registry;
        private readonly InputService _inputService;
        private readonly QuickNotificationService _quickNotificationService;

        private bool _keysAvailable = true;
        private bool _keysVerified;

        public RopeLinkDevModule(RopeConnectionService ropeConnectionService,
                                 PowerTransferStationRegistry registry,
                                 InputService inputService,
                                 QuickNotificationService quickNotificationService)
        {
            _ropeConnectionService = ropeConnectionService;
            _registry = registry;
            _inputService = inputService;
            _quickNotificationService = quickNotificationService;
        }

        public void Load()
        {
            _inputService.AddInputProcessor(this);
        }

        public DevModuleDefinition GetDefinition()
        {
            return new DevModuleDefinition.Builder()
                .AddMethod(DevMethod.CreateBindable("Rope Power: link two newest stations", LinkTwoNewestKey, LinkTwoNewest))
                .AddMethod(DevMethod.CreateBindable("Rope Power: link newest station to all others", LinkNewestToAllKey, LinkNewestToAll))
                .AddMethod(DevMethod.CreateBindable("Rope Power: unlink newest station", UnlinkNewestKey, UnlinkNewest))
                .AddMethod(DevMethod.CreateBindable("Rope Power: log all rope links", LogLinksKey, LogAllLinks))
                .Build();
        }

        public void ProcessInput()
        {
            // Deliberately not gated on dev mode (first Phase 2 retest: dev mode was off and the keys silently did nothing).
            if (!_keysAvailable)
            {
                return;
            }
            try
            {
                if (!_keysVerified)
                {
                    // Touch every binding once so a missing one is reported immediately, not on first use.
                    _inputService.IsKeyDown(LinkNewestToAllKey);
                    _inputService.IsKeyDown(UnlinkNewestKey);
                    _inputService.IsKeyDown(LogLinksKey);
                    _inputService.IsKeyDown(LinkTwoNewestKey);
                    _keysVerified = true;
                    ModLog.Info("dev: debug keys active (Ctrl+Alt+L link two newest, J link newest to all, U unlink newest, K count links)");
                }
                if (_inputService.IsKeyDown(LinkTwoNewestKey))
                {
                    LinkTwoNewest();
                }
                else if (_inputService.IsKeyDown(LinkNewestToAllKey))
                {
                    LinkNewestToAll();
                }
                else if (_inputService.IsKeyDown(UnlinkNewestKey))
                {
                    UnlinkNewest();
                }
                else if (_inputService.IsKeyDown(LogLinksKey))
                {
                    LogAllLinks();
                }
            }
            catch (KeyNotFoundException)
            {
                // KeyBindingRegistry throws for unknown ids, e.g. if the KeyBindings blueprints failed to load.
                _keysAvailable = false;
                ModLog.Warn("dev: Rope Power key bindings not found; use the dev panel instead.");
            }
        }

        private void LinkTwoNewest()
        {
            PowerTransferStation[] newest = _registry.MostRecentlyFinished().Take(2).ToArray();
            if (newest.Length < 2)
            {
                Notify($"Need two finished stations (have {newest.Length}).");
                return;
            }
            Notify(Describe(newest[1], newest[0], _ropeConnectionService.Link(newest[1], newest[0])));
        }

        private void LinkNewestToAll()
        {
            PowerTransferStation[] stations = _registry.MostRecentlyFinished().ToArray();
            if (stations.Length < 2)
            {
                Notify($"Need two finished stations (have {stations.Length}).");
                return;
            }
            int linked = 0;
            var failures = new List<RopeLinkError>();
            for (int i = 1; i < stations.Length; i++)
            {
                RopeLinkError error = _ropeConnectionService.Link(stations[0], stations[i]);
                if (error == RopeLinkError.None)
                {
                    linked++;
                }
                else
                {
                    failures.Add(error);
                }
            }
            string failureText = failures.Count == 0 ? "" : $", rejected: {string.Join(", ", failures)}";
            Notify($"{stations[0].DebugName}: {linked} new link(s){failureText}");
        }

        private void UnlinkNewest()
        {
            PowerTransferStation newest = _registry.MostRecentlyFinished().FirstOrDefault();
            if (!newest)
            {
                Notify("No finished station.");
                return;
            }
            int count = newest.RopePartners.Count;
            _ropeConnectionService.UnlinkAll(newest);
            Notify($"{newest.DebugName}: removed {count} link(s)");
        }

        private void LogAllLinks()
        {
            var links = _ropeConnectionService.AllLinks(_registry.Stations).ToList();
            Notify($"{_registry.Stations.Count} station(s), {links.Count} rope link(s) (details in Player.log)");
            foreach ((PowerTransferStation a, PowerTransferStation b) in links)
            {
                bool sameNetwork = a.PowerGraph != null && a.PowerGraph == b.PowerGraph;
                ModLog.Info($"dev:   {a.DebugName} <-> {b.DebugName} | rope connected: {a.IsRopeConnectedTo(b)}/{b.IsRopeConnectedTo(a)}"
                            + $" | same network: {sameNetwork} | network supply {a.PowerGraph?.PowerSupply ?? 0} hp, demand {a.PowerGraph?.PowerDemand ?? 0} hp");
            }
        }

        private static string Describe(PowerTransferStation a, PowerTransferStation b, RopeLinkError error)
        {
            return error == RopeLinkError.None
                ? $"Linked {a.DebugName} <-> {b.DebugName}"
                : $"Not linked ({error}): {a.DebugName} -> {b.DebugName}";
        }

        private void Notify(string text)
        {
            ModLog.Info("dev: " + text);
            _quickNotificationService.SendNotification("Rope Power: " + text);
        }
    }
}
