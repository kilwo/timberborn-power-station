using System;
using System.Collections.Generic;
using System.Linq;
using RopePower.Stations;
using Timberborn.BlueprintSystem;
using Timberborn.Coordinates;
using Timberborn.GameDistricts;
using Timberborn.SingletonSystem;
using Timberborn.ZiplineSystem;
using UnityEngine;

namespace RopePower.Ropes
{
    /// <summary>
    /// The only place that adds or removes rope links, keeping both ends in sync.
    /// Validation mirrors Timberborn.ZiplineSystem.ZiplineConnectionService (1.1.2.4).
    /// TODO(after Phase 3): clearance along the rope + RopeBlock reservation (see docs/game-api-notes.md §7).
    /// </summary>
    public class RopeConnectionService : ILoadableSingleton
    {
        private static readonly RopeConnectionServiceSpec DefaultSpec = new RopeConnectionServiceSpec
        {
            MaxRopeSpan = 30,
            MaxRopesPerStation = 3,
            MaxInclination = 50
        };

        private static readonly Vector3Int[] HorizontalNeighbours =
        {
            new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0)
        };

        private readonly ISpecService _specService;
        private readonly DistrictCenterRegistry _districtCenterRegistry;

        private RopeConnectionServiceSpec _spec = DefaultSpec;

        public RopeConnectionService(ISpecService specService, DistrictCenterRegistry districtCenterRegistry)
        {
            _specService = specService;
            _districtCenterRegistry = districtCenterRegistry;
        }

        public int MaxRopesPerStation => _spec.MaxRopesPerStation;

        public int MaxRopeSpan => _spec.MaxRopeSpan;

        public int MaxInclination => _spec.MaxInclination;

        /// <summary>Raised after any link is added or removed, with both ends. Phase 3 hooks the power rebuild here.</summary>
        public event Action<PowerTransferStation, PowerTransferStation> LinksChanged;

        public void Load()
        {
            // ISpecService.GetSingleSpec throws (NRE) when no blueprint has the spec, so read it defensively.
            RopeConnectionServiceSpec spec = _specService.GetSpecs<RopeConnectionServiceSpec>().FirstOrDefault();
            if (spec == null)
            {
                ModLog.Warn("RopeConnectionService blueprint not found; using defaults.");
            }
            else
            {
                _spec = spec;
            }
            ModLog.Info($"loaded (max span {MaxRopeSpan}, max ropes {MaxRopesPerStation}, max inclination {MaxInclination})");
        }

        public bool CanLink(PowerTransferStation station, PowerTransferStation other)
        {
            return Validate(station, other) == RopeLinkError.None;
        }

        public RopeLinkError Validate(PowerTransferStation station, PowerTransferStation other)
        {
            if (!other || other.IsDeleted)
            {
                return RopeLinkError.NoTarget;
            }
            if (other == station)
            {
                return RopeLinkError.SameStation;
            }
            if (station.IsLinkedTo(other))
            {
                return RopeLinkError.AlreadyLinked;
            }
            if (!HasFreeSlot(station))
            {
                return RopeLinkError.SourceFull;
            }
            if (!HasFreeSlot(other))
            {
                return RopeLinkError.TargetFull;
            }
            if (!DistanceIsValid(station, other, out _, out _))
            {
                return RopeLinkError.TooLong;
            }
            if (!InclinationIsValid(station, other, out _, out _))
            {
                return RopeLinkError.TooSteep;
            }
            if (!DistrictsAreCompatible(station, other))
            {
                return RopeLinkError.DifferentDistricts;
            }
            return RopeLinkError.None;
        }

        /// <summary>Validates, then links. Returns the reason on failure.</summary>
        public RopeLinkError Link(PowerTransferStation station, PowerTransferStation other)
        {
            RopeLinkError error = Validate(station, other);
            if (error != RopeLinkError.None)
            {
                ModLog.Info($"link {station.DebugName} -> {(other ? other.DebugName : "none")} rejected: {error}");
                return error;
            }
            AddLink(station, other);
            ModLog.Info($"linked {station.DebugName} <-> {other.DebugName} " +
                        $"(span {Vector3.Distance(station.RopeAnchorPoint, other.RopeAnchorPoint):0.0})");
            return RopeLinkError.None;
        }

        /// <summary>
        /// Recreates a saved link. Only structural checks: a link that was valid when made is not dropped
        /// because terrain, roads or settings changed since. Invalid links are dropped with a warning.
        /// </summary>
        public void RestoreLink(PowerTransferStation station, PowerTransferStation other)
        {
            if (!other || other.IsDeleted || other == station)
            {
                ModLog.Warn($"{station.DebugName}: dropped invalid saved rope link.");
                return;
            }
            if (station.IsLinkedTo(other))
            {
                // Already restored from the partner's side.
                return;
            }
            if (!HasFreeSlot(station) || !HasFreeSlot(other))
            {
                ModLog.Warn($"dropped saved rope link {station.DebugName} <-> {other.DebugName}: no free rope slot.");
                return;
            }
            AddLink(station, other);
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
            ModLog.Info($"unlinked {station.DebugName} <-> {other.DebugName}");
            LinksChanged?.Invoke(station, other);
        }

        public void UnlinkAll(PowerTransferStation station)
        {
            while (station.RopePartners.Count > 0)
            {
                Unlink(station, station.RopePartners[0]);
            }
        }

        public bool HasFreeSlot(PowerTransferStation station)
        {
            return station.RopePartners.Count < MaxRopesPerStation;
        }

        public bool DistanceIsValid(PowerTransferStation station, PowerTransferStation other,
                                    out float distance, out float maxDistance)
        {
            distance = Vector3.Distance(station.RopeAnchorPoint, other.RopeAnchorPoint);
            maxDistance = MaxRopeSpan;
            return distance <= maxDistance;
        }

        /// <summary>Same computation as ZiplineConnectionService.InclinationIsValid.</summary>
        public bool InclinationIsValid(PowerTransferStation station, PowerTransferStation other,
                                       out float inclination, out float maxInclination)
        {
            (Vector3 start, Vector3 end) = ZiplineCalculator.CalculateGridAnchors(station.RopeAnchorPoint, other.RopeAnchorPoint);
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
                foreach (PowerTransferStation partner in station.RopePartners)
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
