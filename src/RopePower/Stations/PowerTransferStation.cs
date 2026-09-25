using System.Collections.Generic;
using RopePower.Ropes;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.Coordinates;
using Timberborn.EntitySystem;
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
        private readonly List<PowerTransferStation> _ropePartners = new List<PowerTransferStation>();
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

        public void Awake()
        {
            _spec = GetComponent<PowerTransferStationSpec>();
            _blockObject = GetComponent<BlockObject>();
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
            _ropeConnectionService.UnlinkAll(this);
            _registry.Remove(this);
        }

        public void OnEnterFinishedState()
        {
            _registry.MarkFinished(this);
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
        }

        internal void RemovePartner(PowerTransferStation other)
        {
            _ropePartners.Remove(other);
        }
    }
}
