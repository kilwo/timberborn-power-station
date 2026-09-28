using System;
using System.Collections.Generic;
using CablePowerTransfer.Stations;
using Timberborn.BlockObjectModelSystem;
using Timberborn.BlockSystem;
using Timberborn.BlueprintSystem;
using Timberborn.Common;
using Timberborn.Coordinates;
using Timberborn.RootProviders;
using Timberborn.SelectionSystem;
using Timberborn.SingletonSystem;
using Timberborn.ZiplineSystem;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CablePowerTransfer.Cables
{
    /// <summary>
    /// Clearance along a cable and the invisible blocks that keep it clear. Mirrors the cable-block logic of
    /// Timberborn.ZiplineSystem.ZiplineConnectionService (1.1.2.4), but uses our own PowerCableBlock so cables and ziplines
    /// never delete each other's blocks. Cables may share cells with other cables; cables and ziplines block each other.
    /// </summary>
    public class PowerCableBlockService : ILoadableSingleton
    {
        private static readonly string PowerCableBlockBlueprintPath = "Models/PowerCableBlock/PowerCableBlock.blueprint";

        private class ReservedCell
        {
            public BlockObject BlockObject;
            public int Users;
        }

        private readonly IBlockService _blockService;
        private readonly BlockValidator _blockValidator;
        private readonly BlockObjectFactory _blockObjectFactory;
        private readonly BresenhamLineDrawer _bresenhamLineDrawer;
        private readonly RootObjectProvider _rootObjectProvider;
        private readonly ISpecService _specService;

        private readonly Dictionary<Vector3Int, ReservedCell> _reservedCells = new Dictionary<Vector3Int, ReservedCell>();
        private readonly Dictionary<PowerCableKey, List<Vector3Int>> _cellsByCable = new Dictionary<PowerCableKey, List<Vector3Int>>();
        private readonly HashSet<Vector3Int> _cellCache = new HashSet<Vector3Int>();

        private BlockObjectSpec _powerCableBlockSpec;
        private Transform _root;

        public PowerCableBlockService(IBlockService blockService, BlockValidator blockValidator, BlockObjectFactory blockObjectFactory,
                                BresenhamLineDrawer bresenhamLineDrawer, RootObjectProvider rootObjectProvider, ISpecService specService)
        {
            _blockService = blockService;
            _blockValidator = blockValidator;
            _blockObjectFactory = blockObjectFactory;
            _bresenhamLineDrawer = bresenhamLineDrawer;
            _rootObjectProvider = rootObjectProvider;
            _specService = specService;
        }

        public void Load()
        {
            _root = _rootObjectProvider.CreateRootObject("CablePowerTransferPowerCableBlocks").transform;
            try
            {
                _powerCableBlockSpec = _specService.GetBlueprint(PowerCableBlockBlueprintPath).GetSpec<BlockObjectSpec>();
            }
            catch (Exception e)
            {
                // Fail safe: without the blueprint cables still link and carry power, they just don't reserve cells.
                ModLog.Error($"cable block blueprint '{PowerCableBlockBlueprintPath}' could not be loaded, cables won't reserve cells: {e.Message}");
            }
        }

        /// <summary>True if every cell along the cable can take a cable block (or already has one).</summary>
        public bool PathIsClear(PowerTransferStation station, PowerTransferStation other)
        {
            if (_powerCableBlockSpec == null)
            {
                return true;
            }
            CollectCells(station, other, _cellCache);
            bool clear = true;
            foreach (Vector3Int cell in _cellCache)
            {
                if (!CellIsFree(cell))
                {
                    clear = false;
                    break;
                }
            }
            _cellCache.Clear();
            return clear;
        }

        /// <summary>Objects in the way of a cable, for highlighting in the connection tool (Phase 4).</summary>
        public void GetBlockingObjects(PowerTransferStation station, PowerTransferStation other, ICollection<BlockObject> blockingObjects)
        {
            CollectCells(station, other, _cellCache);
            foreach (Vector3Int cell in _cellCache)
            {
                if (!_blockService.AnyObjectAt(cell) || IsPowerCableBlockAt(cell))
                {
                    continue;
                }
                foreach (BlockObject blockObject in _blockService.GetObjectsAt(cell))
                {
                    // Only objects that can be highlighted. Invisible blockers such as vanilla zipline cable blocks have
                    // no model/HighlightableObject, and Highlighter.HighlightPrimary throws on them (seen in-game when a
                    // cable crossed a zipline). ZiplineConnectionService.GetBlockingObjects filters on IBlockObjectModel too.
                    if (blockObject.HasComponent<IBlockObjectModel>()
                        && blockObject.HasComponent<HighlightableObject>()
                        && !blockingObjects.Contains(blockObject))
                    {
                        blockingObjects.Add(blockObject);
                    }
                }
            }
            _cellCache.Clear();
        }

        /// <summary>
        /// Places cable blocks along a new cable. With <paramref name="skipBlockedCells"/> (restoring a saved cable), cells that
        /// are no longer free are left unreserved with a warning instead of overlapping another object.
        /// </summary>
        public void Reserve(PowerTransferStation station, PowerTransferStation other, bool skipBlockedCells)
        {
            var key = new PowerCableKey(station, other);
            if (_powerCableBlockSpec == null || _cellsByCable.ContainsKey(key))
            {
                return;
            }
            var cells = new List<Vector3Int>();
            CollectCells(station, other, _cellCache);
            int skipped = 0;
            foreach (Vector3Int cell in _cellCache)
            {
                if (_reservedCells.TryGetValue(cell, out ReservedCell reserved))
                {
                    reserved.Users++;
                    cells.Add(cell);
                }
                else if (!skipBlockedCells || CellIsFree(cell))
                {
                    BlockObject blockObject = _blockObjectFactory.CreateAsPreview(_powerCableBlockSpec, _root, new Placement(cell));
                    blockObject.GameObject.name = $"PowerCableBlock {cell}";
                    blockObject.MarkAsFinishedAndAddToServices();
                    _reservedCells[cell] = new ReservedCell { BlockObject = blockObject, Users = 1 };
                    cells.Add(cell);
                }
                else
                {
                    skipped++;
                }
            }
            _cellCache.Clear();
            _cellsByCable[key] = cells;
            if (skipped > 0)
            {
                ModLog.Warn($"cable {station.DebugName} <-> {other.DebugName}: {skipped} cell(s) along the cable are occupied and were not reserved.");
            }
        }

        public void Release(PowerTransferStation station, PowerTransferStation other)
        {
            var key = new PowerCableKey(station, other);
            if (!_cellsByCable.TryGetValue(key, out List<Vector3Int> cells))
            {
                return;
            }
            _cellsByCable.Remove(key);
            foreach (Vector3Int cell in cells)
            {
                if (!_reservedCells.TryGetValue(cell, out ReservedCell reserved) || --reserved.Users > 0)
                {
                    continue;
                }
                _reservedCells.Remove(cell);
                reserved.BlockObject.DeleteEntity();
                Object.Destroy(reserved.BlockObject.GameObject);
            }
        }

        public IEnumerable<Vector3Int> ReservedCells => _reservedCells.Keys;

        private bool CellIsFree(Vector3Int cell)
        {
            return IsPowerCableBlockAt(cell) || _blockValidator.BlocksValid(_powerCableBlockSpec, new Placement(cell));
        }

        private bool IsPowerCableBlockAt(Vector3Int cell)
        {
            return _blockService.GetBottomObjectComponentAt<PowerCableBlock>(cell) != null;
        }

        /// <summary>Voxel line between the anchors, minus both stations' own cells (as zipline towers' anchors/unobstructed cells).</summary>
        private void CollectCells(PowerTransferStation station, PowerTransferStation other, HashSet<Vector3Int> cells)
        {
            // Draw from the lower-sorted end so both directions of the same cable give identical cells.
            PowerCableKey key = new PowerCableKey(station, other);
            _bresenhamLineDrawer.DrawLine(key.First.CableAnchorPoint.FloorToInt(), key.Second.CableAnchorPoint.FloorToInt(), cells);
            foreach (Vector3Int cell in station.OccupiedCoordinates)
            {
                cells.Remove(cell);
            }
            foreach (Vector3Int cell in other.OccupiedCoordinates)
            {
                cells.Remove(cell);
            }
        }
    }
}
