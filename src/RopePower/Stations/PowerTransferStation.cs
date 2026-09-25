using System.Collections.Generic;
using RopePower.Ropes;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.Coordinates;
using Timberborn.EntitySystem;
using Timberborn.MechanicalSystem;
using Timberborn.Persistence;
using Timberborn.WorldPersistence;
using UnityEngine;

namespace RopePower.Stations
{
    /// <summary>
    /// A station that can hold rope links to other stations. Links are stored on both ends;
    /// only RopeConnectionService may change them. Structure mirrors Timberborn.ZiplineSystem.ZiplineTower.
    /// </summary>
    public class PowerTransferStation : BaseComponent,
                                        IAwakableComponent,
                                        IInitializableEntity,
                                        IPostInitializableEntity,
                                        IDeletableEntity,
                                        IFinishedStateListener,
                                        IPersistentEntity
    {
        private static readonly ComponentKey PowerTransferStationKey = new ComponentKey("PowerTransferStation");
        private static readonly ListKey<PowerTransferStation> RopePartnersKey = new ListKey<PowerTransferStation>("RopePartners");
        private static readonly PropertyKey<int> RopePartnerCountKey = new PropertyKey<int>("RopePartnerCount");

        private readonly RopeConnectionService _ropeConnectionService;
        private readonly PowerTransferStationRegistry _registry;
        private readonly ReferenceSerializer _referenceSerializer;

        private PowerTransferStationSpec _spec;
        private BlockObject _blockObject;
        private MechanicalNode _mechanicalNode;
        private readonly List<PowerTransferStation> _ropePartners = new List<PowerTransferStation>();
        // Which rope-slot transput (index among the slot transputs) each link uses on this end. Not persisted:
        // slots are reassigned when links are restored, before the mechanical node joins a graph.
        private readonly Dictionary<PowerTransferStation, int> _slotByPartner = new Dictionary<PowerTransferStation, int>();
        private List<PowerTransferStation> _loadedRopePartners;

        public PowerTransferStation(RopeConnectionService ropeConnectionService,
                                    PowerTransferStationRegistry registry,
                                    ReferenceSerializer referenceSerializer)
        {
            _ropeConnectionService = ropeConnectionService;
            _registry = registry;
            _referenceSerializer = referenceSerializer;
        }

        public IReadOnlyList<PowerTransferStation> RopePartners => _ropePartners;

        public bool IsFinished => _blockObject.IsFinished;

        public bool IsDeleted => GetComponent<EntityComponent>().Deleted;

        /// <summary>Base block coordinates.</summary>
        public Vector3Int Coordinates => _blockObject.Coordinates;

        /// <summary>Pulley top in grid space (Z up), for validation and rendering.</summary>
        public Vector3 RopeAnchorPoint => _blockObject.TransformCoordinates(CoordinateSystem.WorldToGrid(_spec.RopeAnchorPoint));

        public string DebugName => $"station {Coordinates}";

        /// <summary>Number of rope-slot transputs in the blueprint; hard cap on ropes for this station.</summary>
        public int RopeSlotCapacity { get; private set; }

        /// <summary>Set while DeleteEntity runs so the power refresh leaves this station alone.</summary>
        public bool IsBeingDeleted { get; private set; }

        public MechanicalGraph PowerGraph => _mechanicalNode.Graph;

        public void Awake()
        {
            _spec = GetComponent<PowerTransferStationSpec>();
            _blockObject = GetComponent<BlockObject>();
            _mechanicalNode = GetComponent<MechanicalNode>();
            RopeSlotCapacity = CountRopeSlotSpecs();
        }

        public void InitializeEntity()
        {
            _registry.Add(this);
        }

        public void PostInitializeEntity()
        {
            if (_loadedRopePartners == null)
            {
                return;
            }
            foreach (PowerTransferStation partner in _loadedRopePartners)
            {
                _ropeConnectionService.RestoreLink(this, partner);
            }
            _loadedRopePartners = null;
        }

        public void DeleteEntity()
        {
            IsBeingDeleted = true;
            _ropeConnectionService.UnlinkAll(this);
            _registry.Remove(this);
        }

        public void OnEnterFinishedState()
        {
            _registry.MarkFinished(this);
            ModLog.Info($"{DebugName} finished ({_registry.Stations.Count} station(s) total)");
        }

        public void OnExitFinishedState()
        {
        }

        public void Save(IEntitySaver entitySaver)
        {
            IObjectSaver saver = entitySaver.GetComponent(PowerTransferStationKey);
            saver.Set(RopePartnersKey, _ropePartners, _referenceSerializer.Of<PowerTransferStation>());
            saver.Set(RopePartnerCountKey, _ropePartners.Count);
        }

        public void Load(IEntityLoader entityLoader)
        {
            if (!entityLoader.TryGetComponent(PowerTransferStationKey, out IObjectLoader loader))
            {
                return;
            }
            // The game's list deserializer silently skips references to entities that no longer exist,
            // so compare against the saved count to report dropped links.
            _loadedRopePartners = loader.Has(RopePartnersKey)
                ? loader.Get(RopePartnersKey, _referenceSerializer.Of<PowerTransferStation>())
                : new List<PowerTransferStation>();
            if (loader.Has(RopePartnerCountKey))
            {
                int savedCount = loader.Get(RopePartnerCountKey);
                int missing = savedCount - _loadedRopePartners.Count;
                if (missing > 0)
                {
                    ModLog.Warn($"{DebugName}: dropped {missing} rope link(s) to stations missing from the save.");
                }
            }
        }

        public bool IsLinkedTo(PowerTransferStation other)
        {
            return _ropePartners.Contains(other);
        }

        internal void AddPartner(PowerTransferStation other)
        {
            _ropePartners.Add(other);
            _slotByPartner[other] = LowestFreeSlot();
        }

        internal void RemovePartner(PowerTransferStation other)
        {
            _ropePartners.Remove(other);
            _slotByPartner.Remove(other);
        }

        /// <summary>
        /// Called from the TransputMap.GetFacingTransput patch. If <paramref name="transput"/> is one of this station's
        /// rope slots and the slot is in use, returns the partner's paired rope-slot transput; otherwise null.
        /// </summary>
        public Transput GetRopePartnerTransput(Transput transput)
        {
            int slot = GetRopeSlotIndex(transput);
            if (slot < 0)
            {
                return null;
            }
            foreach (KeyValuePair<PowerTransferStation, int> entry in _slotByPartner)
            {
                if (entry.Value == slot)
                {
                    PowerTransferStation partner = entry.Key;
                    return partner._slotByPartner.TryGetValue(this, out int partnerSlot)
                        ? partner.GetRopeSlotTransput(partnerSlot)
                        : null;
                }
            }
            return null;
        }

        /// <summary>
        /// Rebuilds this station's mechanical connections so rope changes take effect: detach + reattach runs the game's own
        /// MechanicalGraphManager.RemoveNode/AddNode (same public path the vanilla Clutch uses).
        /// </summary>
        public void RefreshPowerConnections()
        {
            if (IsBeingDeleted || !_mechanicalNode.Enabled || _mechanicalNode.IsDetached)
            {
                // Not in a graph yet (unfinished / loading): the node picks up its ropes when it joins a graph.
                return;
            }
            _mechanicalNode.SetDetached(true);
            _mechanicalNode.SetDetached(false);
        }

        /// <summary>True if the rope to <paramref name="other"/> is an actual transput connection right now.</summary>
        public bool IsRopeConnectedTo(PowerTransferStation other)
        {
            if (!_slotByPartner.TryGetValue(other, out int slot))
            {
                return false;
            }
            Transput transput = GetRopeSlotTransput(slot);
            return transput != null && transput.ConnectedNode == other._mechanicalNode;
        }

        private Transput GetRopeSlotTransput(int slot)
        {
            int index = 0;
            foreach (Transput transput in RopeSlotTransputs())
            {
                if (index++ == slot)
                {
                    return transput;
                }
            }
            return null;
        }

        private int GetRopeSlotIndex(Transput transput)
        {
            int index = 0;
            foreach (Transput slotTransput in RopeSlotTransputs())
            {
                if (slotTransput == transput)
                {
                    return index;
                }
                index++;
            }
            return -1;
        }

        private IEnumerable<Transput> RopeSlotTransputs()
        {
            // Transputs are created when the building enters the unfinished or finished state.
            if (_mechanicalNode.Transputs.IsDefault)
            {
                yield break;
            }
            foreach (Transput transput in _mechanicalNode.Transputs)
            {
                if (transput.BaseDirection == Direction3D.Bottom && transput.Offset == _spec.RopeSlotCoordinates)
                {
                    yield return transput;
                }
            }
        }

        private int CountRopeSlotSpecs()
        {
            int count = 0;
            foreach (TransputSpec transputSpec in GetComponent<TransputProviderSpec>().Transputs)
            {
                if (transputSpec.Coordinates == _spec.RopeSlotCoordinates)
                {
                    count++;
                }
            }
            return count;
        }

        private int LowestFreeSlot()
        {
            for (int slot = 0; ; slot++)
            {
                if (!_slotByPartner.ContainsValue(slot))
                {
                    return slot;
                }
            }
        }
    }
}
