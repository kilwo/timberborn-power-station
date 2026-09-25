using System;
using System.Collections.Generic;
using RopePower.Stations;
using Timberborn.BlockSystem;
using Timberborn.BlueprintSystem;
using Timberborn.Common;
using Timberborn.Coordinates;
using Timberborn.RootProviders;
using Timberborn.SingletonSystem;
using Timberborn.ZiplineSystem;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RopePower.Ropes
{
    /// <summary>
    /// Clearance along a rope and the invisible blocks that keep it clear. Mirrors the cable-block logic of
    /// Timberborn.ZiplineSystem.ZiplineConnectionService (1.1.2.4), but uses our own RopeBlock so ropes and ziplines
    /// never delete each other's blocks. Ropes may share cells with other ropes; ropes and ziplines block each other.
    /// </summary>
    public class RopeBlockService : ILoadableSingleton
    {
        private static readonly string RopeBlockBlueprintPath = "Models/RopeBlock/RopeBlock.blueprint";

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
        private readonly Dictionary<RopeKey, List<Vector3Int>> _cellsByRope = new Dictionary<RopeKey, List<Vector3Int>>();
        private readonly HashSet<Vector3Int> _cellCache = new HashSet<Vector3Int>();

        private BlockObjectSpec _ropeBlockSpec;
        private Transform _root;

        public RopeBlockService(IBlockService blockService, BlockValidator blockValidator, BlockObjectFactory blockObjectFactory,
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
            _root = _rootObjectProvider.CreateRootObject("RopePowerRopeBlocks").transform;
            try
            {
                _ropeBlockSpec = _specService.GetBlueprint(RopeBlockBlueprintPath).GetSpec<BlockObjectSpec>();
            }
            catch (Exception e)
            {
                // Fail safe: without the blueprint ropes still link and carry power, they just don't reserve cells.
                ModLog.Error($"rope block blueprint '{RopeBlockBlueprintPath}' could not be loaded, ropes won't reserve cells: {e.Message}");
            }
        }

        /// <summary>True if every cell along the rope can take a rope block (or already has one).</summary>
        public bool PathIsClear(PowerTransferStation station, PowerTransferStation other)
        {
            if (_ropeBlockSpec == null)
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

        /// <summary>Objects in the way of a rope, for highlighting in the connection tool (Phase 4).</summary>
        public void GetBlockingObjects(PowerTransferStation station, PowerTransferStation other, ICollection<BlockObject> blockingObjects)
        {
            CollectCells(station, other, _cellCache);
            foreach (Vector3Int cell in _cellCache)
            {
                if (!_blockService.AnyObjectAt(cell) || IsRopeBlockAt(cell))
                {
                    continue;
                }
                foreach (BlockObject blockObject in _blockService.GetObjectsAt(cell))
                {
                    if (!blockingObjects.Contains(blockObject))
                    {
                        blockingObjects.Add(blockObject);
                    }
                }
            }
            _cellCache.Clear();
        }

        /// <summary>
        /// Places rope blocks along a new rope. With <paramref name="skipBlockedCells"/> (restoring a saved rope), cells that
        /// are no longer free are left unreserved with a warning instead of overlapping another object.
        /// </summary>
        public void Reserve(PowerTransferStation station, PowerTransferStation other, bool skipBlockedCells)
        {
            var key = new RopeKey(station, other);
            if (_ropeBlockSpec == null || _cellsByRope.ContainsKey(key))
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
                    BlockObject blockObject = _blockObjectFactory.CreateAsPreview(_ropeBlockSpec, _root, new Placement(cell));
                    blockObject.GameObject.name = $"RopeBlock {cell}";
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
            _cellsByRope[key] = cells;
            if (skipped > 0)
            {
                ModLog.Warn($"rope {station.DebugName} <-> {other.DebugName}: {skipped} cell(s) along the rope are occupied and were not reserved.");
            }
        }

        public void Release(PowerTransferStation station, PowerTransferStation other)
        {
            var key = new RopeKey(station, other);
            if (!_cellsByRope.TryGetValue(key, out List<Vector3Int> cells))
            {
                return;
            }
            _cellsByRope.Remove(key);
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
            return IsRopeBlockAt(cell) || _blockValidator.BlocksValid(_ropeBlockSpec, new Placement(cell));
        }

        private bool IsRopeBlockAt(Vector3Int cell)
        {
            return _blockService.GetBottomObjectComponentAt<RopeBlock>(cell) != null;
        }

        /// <summary>Voxel line between the anchors, minus both stations' own cells (as zipline towers' anchors/unobstructed cells).</summary>
        private void CollectCells(PowerTransferStation station, PowerTransferStation other, HashSet<Vector3Int> cells)
        {
            // Draw from the lower-sorted end so both directions of the same rope give identical cells.
            RopeKey key = new RopeKey(station, other);
            _bresenhamLineDrawer.DrawLine(key.First.RopeAnchorPoint.FloorToInt(), key.Second.RopeAnchorPoint.FloorToInt(), cells);
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
