using System.Collections.Generic;
using CablePowerTransfer.Cables;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockObjectModelSystem;
using Timberborn.BlockSystem;
using Timberborn.Coordinates;
using Timberborn.EntitySystem;
using Timberborn.MechanicalSystem;
using Timberborn.Persistence;
using Timberborn.WorldPersistence;
using UnityEngine;

namespace CablePowerTransfer.Stations
{
    /// <summary>
    /// A station that can hold cable links to other stations. Links are stored on both ends;
    /// only CableConnectionService may change them. Structure mirrors Timberborn.ZiplineSystem.ZiplineTower.
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
        // Save keys keep their pre-rename ("Rope Power") names so existing saves still load their links. Don't rename.
        private static readonly ListKey<PowerTransferStation> CablePartnersKey = new ListKey<PowerTransferStation>("RopePartners");
        private static readonly PropertyKey<int> CablePartnerCountKey = new PropertyKey<int>("RopePartnerCount");

        private readonly CableConnectionService _cableConnectionService;
        private readonly PowerTransferStationRegistry _registry;
        private readonly ReferenceSerializer _referenceSerializer;

        private PowerTransferStationSpec _spec;
        private BlockObject _blockObject;
        private MechanicalNode _mechanicalNode;
        private readonly List<PowerTransferStation> _cablePartners = new List<PowerTransferStation>();
        // Which cable-slot transput (index among the slot transputs) each link uses on this end. Not persisted:
        // slots are reassigned when links are restored, before the mechanical node joins a graph.
        private readonly Dictionary<PowerTransferStation, int> _slotByPartner = new Dictionary<PowerTransferStation, int>();
        private List<PowerTransferStation> _loadedCablePartners;

        public PowerTransferStation(CableConnectionService cableConnectionService,
                                    PowerTransferStationRegistry registry,
                                    ReferenceSerializer referenceSerializer)
        {
            _cableConnectionService = cableConnectionService;
            _registry = registry;
            _referenceSerializer = referenceSerializer;
        }

        public IReadOnlyList<PowerTransferStation> CablePartners => _cablePartners;

        public bool IsFinished => _blockObject.IsFinished;

        public bool IsDeleted => GetComponent<EntityComponent>().Deleted;

        /// <summary>Base block coordinates.</summary>
        public Vector3Int Coordinates => _blockObject.Coordinates;

        /// <summary>Pulley top in grid space (Z up), for validation and rendering.</summary>
        public Vector3 CableAnchorPoint => _blockObject.TransformCoordinates(CoordinateSystem.WorldToGrid(_spec.CableAnchorPoint));

        public string DebugName => $"station {Coordinates}";

        public IEnumerable<Vector3Int> OccupiedCoordinates => _blockObject.PositionedBlocks.GetOccupiedCoordinates();

        /// <summary>Number of cable-slot transputs in the blueprint; hard cap on cables for this station.</summary>
        public int CableSlotCapacity { get; private set; }

        /// <summary>Set while DeleteEntity runs so the power refresh leaves this station alone.</summary>
        public bool IsBeingDeleted { get; private set; }

        public MechanicalGraph PowerGraph => _mechanicalNode.Graph;

        /// <summary>
        /// True when shafts on this network turn: active (not blocked, e.g. flooded), powered, and efficiency above 0.
        /// ActiveAndPowered alone stays true on a network with a charged battery but no supply or demand, where shafts
        /// stand still. Same test as Timberborn.ModularShafts.ModularShaftAnimator (1.1.2.4) and StationAnimator.
        /// Drives the moving-cable visual.
        /// </summary>
        public bool IsTurning => _mechanicalNode.ActiveAndPowered && _mechanicalNode.PowerEfficiency > 0f;

        public float PulleyRadius => _spec.PulleyRadius > 0f ? _spec.PulleyRadius : 0.175f;

        /// <summary>False while the level visibility slider hides this station.</summary>
        public bool IsAnyModelShown
        {
            get
            {
                BlockObjectModelController controller = GetComponent<BlockObjectModelController>();
                return controller == null || controller.IsAnyModelShown;
            }
        }

        public void Awake()
        {
            _spec = GetComponent<PowerTransferStationSpec>();
            _blockObject = GetComponent<BlockObject>();
            _mechanicalNode = GetComponent<MechanicalNode>();
            CableSlotCapacity = CountCableSlotSpecs();
        }

        public void InitializeEntity()
        {
            _registry.Add(this);
        }

        public void PostInitializeEntity()
        {
            if (_loadedCablePartners == null)
            {
                return;
            }
            foreach (PowerTransferStation partner in _loadedCablePartners)
            {
                _cableConnectionService.RestoreLink(this, partner);
            }
            _loadedCablePartners = null;
        }

        public void DeleteEntity()
        {
            IsBeingDeleted = true;
            _cableConnectionService.UnlinkAll(this);
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
            saver.Set(CablePartnersKey, _cablePartners, _referenceSerializer.Of<PowerTransferStation>());
            saver.Set(CablePartnerCountKey, _cablePartners.Count);
        }

        public void Load(IEntityLoader entityLoader)
        {
            if (!entityLoader.TryGetComponent(PowerTransferStationKey, out IObjectLoader loader))
            {
                return;
            }
            // The game's list deserializer silently skips references to entities that no longer exist,
            // so compare against the saved count to report dropped links.
            _loadedCablePartners = loader.Has(CablePartnersKey)
                ? loader.Get(CablePartnersKey, _referenceSerializer.Of<PowerTransferStation>())
                : new List<PowerTransferStation>();
            if (loader.Has(CablePartnerCountKey))
            {
                int savedCount = loader.Get(CablePartnerCountKey);
                int missing = savedCount - _loadedCablePartners.Count;
                if (missing > 0)
                {
                    ModLog.Warn($"{DebugName}: dropped {missing} cable link(s) to stations missing from the save.");
                }
            }
        }

        public bool IsLinkedTo(PowerTransferStation other)
        {
            return _cablePartners.Contains(other);
        }

        internal void AddPartner(PowerTransferStation other)
        {
            _cablePartners.Add(other);
            _slotByPartner[other] = LowestFreeSlot();
        }

        internal void RemovePartner(PowerTransferStation other)
        {
            _cablePartners.Remove(other);
            _slotByPartner.Remove(other);
        }

        /// <summary>
        /// Called from the TransputMap.GetFacingTransput patch. If <paramref name="transput"/> is one of this station's
        /// cable slots and the slot is in use, returns the partner's paired cable-slot transput; otherwise null.
        /// </summary>
        public Transput GetCablePartnerTransput(Transput transput)
        {
            int slot = GetCableSlotIndex(transput);
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
                        ? partner.GetCableSlotTransput(partnerSlot)
                        : null;
                }
            }
            return null;
        }

        /// <summary>
        /// Rebuilds this station's mechanical connections so cable changes take effect: detach + reattach runs the game's own
        /// MechanicalGraphManager.RemoveNode/AddNode (same public path the vanilla Clutch uses).
        /// </summary>
        public void RefreshPowerConnections()
        {
            if (IsBeingDeleted || !_mechanicalNode.Enabled || _mechanicalNode.IsDetached)
            {
                // Not in a graph yet (unfinished / loading): the node picks up its cables when it joins a graph.
                return;
            }
            _mechanicalNode.SetDetached(true);
            _mechanicalNode.SetDetached(false);
        }

        /// <summary>True if the cable to <paramref name="other"/> is an actual transput connection right now.</summary>
        public bool IsCableConnectedTo(PowerTransferStation other)
        {
            if (!_slotByPartner.TryGetValue(other, out int slot))
            {
                return false;
            }
            Transput transput = GetCableSlotTransput(slot);
            return transput != null && transput.ConnectedNode == other._mechanicalNode;
        }

        private Transput GetCableSlotTransput(int slot)
        {
            int index = 0;
            foreach (Transput transput in CableSlotTransputs())
            {
                if (index++ == slot)
                {
                    return transput;
                }
            }
            return null;
        }

        private int GetCableSlotIndex(Transput transput)
        {
            int index = 0;
            foreach (Transput slotTransput in CableSlotTransputs())
            {
                if (slotTransput == transput)
                {
                    return index;
                }
                index++;
            }
            return -1;
        }

        private IEnumerable<Transput> CableSlotTransputs()
        {
            // Transputs are created when the building enters the unfinished or finished state.
            if (_mechanicalNode.Transputs.IsDefault)
            {
                yield break;
            }
            foreach (Transput transput in _mechanicalNode.Transputs)
            {
                if (transput.BaseDirection == Direction3D.Bottom && transput.Offset == _spec.CableSlotCoordinates)
                {
                    yield return transput;
                }
            }
        }

        private int CountCableSlotSpecs()
        {
            int count = 0;
            foreach (TransputSpec transputSpec in GetComponent<TransputProviderSpec>().Transputs)
            {
                if (transputSpec.Coordinates == _spec.CableSlotCoordinates)
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
