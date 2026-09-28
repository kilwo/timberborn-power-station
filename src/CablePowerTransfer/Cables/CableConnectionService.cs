using System;
using System.Collections.Generic;
using System.Linq;
using CablePowerTransfer.Stations;
using Timberborn.BlueprintSystem;
using Timberborn.Coordinates;
using Timberborn.GameDistricts;
using Timberborn.SingletonSystem;
using Timberborn.ZiplineSystem;
using UnityEngine;

namespace CablePowerTransfer.Cables
{
    /// <summary>
    /// The only place that adds or removes cable links, keeping both ends in sync.
    /// Validation mirrors Timberborn.ZiplineSystem.ZiplineConnectionService (1.1.2.4).
    /// Clearance and cell reservation along the cable are delegated to PowerCableBlockService.
    /// </summary>
    public class CableConnectionService : ILoadableSingleton
    {
        private static readonly CableConnectionServiceSpec DefaultSpec = new CableConnectionServiceSpec
        {
            MaxCableSpan = 30,
            MaxCablesPerStation = 3,
            MaxInclination = 50
        };

        private static readonly Vector3Int[] HorizontalNeighbours =
        {
            new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0)
        };

        private readonly ISpecService _specService;
        private readonly DistrictCenterRegistry _districtCenterRegistry;
        private readonly PowerCableBlockService _powerCableBlockService;

        private CableConnectionServiceSpec _spec = DefaultSpec;

        public CableConnectionService(ISpecService specService, DistrictCenterRegistry districtCenterRegistry,
                                     PowerCableBlockService powerCableBlockService)
        {
            _specService = specService;
            _districtCenterRegistry = districtCenterRegistry;
            _powerCableBlockService = powerCableBlockService;
        }

        public int MaxCablesPerStation => _spec.MaxCablesPerStation;

        public int MaxCableSpan => _spec.MaxCableSpan;

        public int MaxInclination => _spec.MaxInclination;

        /// <summary>Raised after any link is added or removed, with both ends. CablePowerConnector rebuilds power from it.</summary>
        public event Action<PowerTransferStation, PowerTransferStation> LinksChanged;

        public void Load()
        {
            // ISpecService.GetSingleSpec throws (NRE) when no blueprint has the spec, so read it defensively.
            CableConnectionServiceSpec spec = _specService.GetSpecs<CableConnectionServiceSpec>().FirstOrDefault();
            if (spec == null)
            {
                ModLog.Warn("CableConnectionService blueprint not found; using defaults.");
            }
            else
            {
                _spec = spec;
            }
            ModLog.Info($"loaded (max span {MaxCableSpan}, max cables {MaxCablesPerStation}, max inclination {MaxInclination})");
        }

        public bool CanLink(PowerTransferStation station, PowerTransferStation other)
        {
            return Validate(station, other) == CableLinkError.None;
        }

        public CableLinkError Validate(PowerTransferStation station, PowerTransferStation other)
        {
            if (!other || other.IsDeleted)
            {
                return CableLinkError.NoTarget;
            }
            if (other == station)
            {
                return CableLinkError.SameStation;
            }
            if (station.IsLinkedTo(other))
            {
                return CableLinkError.AlreadyLinked;
            }
            if (!HasFreeSlot(station))
            {
                return CableLinkError.SourceFull;
            }
            if (!HasFreeSlot(other))
            {
                return CableLinkError.TargetFull;
            }
            if (!DistanceIsValid(station, other, out _, out _))
            {
                return CableLinkError.TooLong;
            }
            if (!InclinationIsValid(station, other, out _, out _))
            {
                return CableLinkError.TooSteep;
            }
            if (!DistrictsAreCompatible(station, other))
            {
                return CableLinkError.DifferentDistricts;
            }
            if (!_powerCableBlockService.PathIsClear(station, other))
            {
                return CableLinkError.Obstructed;
            }
            return CableLinkError.None;
        }

        /// <summary>Validates, then links. Returns the reason on failure.</summary>
        public CableLinkError Link(PowerTransferStation station, PowerTransferStation other)
        {
            CableLinkError error = Validate(station, other);
            if (error != CableLinkError.None)
            {
                ModLog.Info($"link {station.DebugName} -> {(other ? other.DebugName : "none")} rejected: {error}");
                return error;
            }
            AddLink(station, other);
            _powerCableBlockService.Reserve(station, other, skipBlockedCells: false);
            ModLog.Info($"linked {station.DebugName} <-> {other.DebugName} " +
                        $"(span {Vector3.Distance(station.CableAnchorPoint, other.CableAnchorPoint):0.0})");
            return CableLinkError.None;
        }

        /// <summary>
        /// Recreates a saved link. Only structural checks: a link that was valid when made is not dropped
        /// because terrain, roads or settings changed since. Invalid links are dropped with a warning.
        /// </summary>
        public void RestoreLink(PowerTransferStation station, PowerTransferStation other)
        {
            if (!other || other.IsDeleted || other == station)
            {
                ModLog.Warn($"{station.DebugName}: dropped invalid saved cable link.");
                return;
            }
            if (station.IsLinkedTo(other))
            {
                // Already restored from the partner's side.
                return;
            }
            if (!HasFreeSlot(station) || !HasFreeSlot(other))
            {
                ModLog.Warn($"dropped saved cable link {station.DebugName} <-> {other.DebugName}: no free cable slot.");
                return;
            }
            AddLink(station, other);
            // Saves from before cable blocks existed (or odd edits) may have objects in the way: keep the link, skip those cells.
            _powerCableBlockService.Reserve(station, other, skipBlockedCells: true);
            ModLog.Info($"restored link {station.DebugName} <-> {other.DebugName}");
        }

        public void Unlink(PowerTransferStation station, PowerTransferStation other)
        {
            if (!station.IsLinkedTo(other))
            {
                return;
            }
            station.RemovePartner(other);
            other.RemovePartner(station);
            _powerCableBlockService.Release(station, other);
            ModLog.Info($"unlinked {station.DebugName} <-> {other.DebugName}");
            LinksChanged?.Invoke(station, other);
        }

        public void UnlinkAll(PowerTransferStation station)
        {
            while (station.CablePartners.Count > 0)
            {
                Unlink(station, station.CablePartners[0]);
            }
        }

        public bool HasFreeSlot(PowerTransferStation station)
        {
            // The blueprint's cable-slot transput count is a hard cap regardless of settings.
            return station.CablePartners.Count < Math.Min(MaxCablesPerStation, station.CableSlotCapacity);
        }

        public bool DistanceIsValid(PowerTransferStation station, PowerTransferStation other,
                                    out float distance, out float maxDistance)
        {
            distance = Vector3.Distance(station.CableAnchorPoint, other.CableAnchorPoint);
            maxDistance = MaxCableSpan;
            return distance <= maxDistance;
        }

        /// <summary>Same computation as ZiplineConnectionService.InclinationIsValid.</summary>
        public bool InclinationIsValid(PowerTransferStation station, PowerTransferStation other,
                                       out float inclination, out float maxInclination)
        {
            (Vector3 start, Vector3 end) = ZiplineCalculator.CalculateGridAnchors(station.CableAnchorPoint, other.CableAnchorPoint);
            Vector3 direction = (end - start).normalized;
            inclination = Math.Abs(Vector3.Angle(new Vector3(0f, 0f, 1f), direction) - 90f);
            maxInclination = MaxInclination;
            return inclination < maxInclination;
        }

        /// <summary>Lenient, like ziplines: only fails if both ends have a district and they differ.</summary>
        public bool DistrictsAreCompatible(PowerTransferStation station, PowerTransferStation other)
        {
            DistrictCenter district = GetDistrict(station);
            if (!district)
            {
                return true;
            }
            DistrictCenter otherDistrict = GetDistrict(other);
            return !otherDistrict || district == otherDistrict;
        }

        /// <summary>District of any road beside the station base; mirrors PathDistrictRetriever.GetAnyDistrictCenter.</summary>
        public DistrictCenter GetDistrict(PowerTransferStation station)
        {
            foreach (Vector3Int offset in HorizontalNeighbours)
            {
                Vector3 position = CoordinateSystem.GridToWorld(station.Coordinates + offset);
                foreach (DistrictCenter districtCenter in _districtCenterRegistry.AllDistrictCenters)
                {
                    if (districtCenter.IsOnPreviewDistrictRoad(position) || districtCenter.IsOnInstantDistrictRoad(position))
                    {
                        return districtCenter;
                    }
                }
            }
            return null;
        }

        public IEnumerable<(PowerTransferStation, PowerTransferStation)> AllLinks(IEnumerable<PowerTransferStation> stations)
        {
            var seen = new HashSet<PowerTransferStation>();
            foreach (PowerTransferStation station in stations)
            {
                seen.Add(station);
                foreach (PowerTransferStation partner in station.CablePartners)
                {
                    if (!seen.Contains(partner))
                    {
                        yield return (station, partner);
                    }
                }
            }
        }

        private void AddLink(PowerTransferStation station, PowerTransferStation other)
        {
            station.AddPartner(other);
            other.AddPartner(station);
            LinksChanged?.Invoke(station, other);
        }
    }
}
