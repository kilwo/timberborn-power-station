using System;
using System.Collections.Generic;
using System.Linq;
using CablePowerTransfer.Cables;
using CablePowerTransfer.Stations;
using Timberborn.BlueprintSystem;
using Timberborn.LevelVisibilitySystem;
using Timberborn.Rendering;
using Timberborn.RootProviders;
using Timberborn.SelectionSystem;
using Timberborn.SingletonSystem;
using Timberborn.TemplateInstantiation;
using UnityEngine;

namespace CablePowerTransfer.Rendering
{
    /// <summary>
    /// Keeps one CableLoopModel per cable link, plus a model factory for the connection tool preview.
    /// Cables to unfinished stations are greyscale; cables on a powered network move (zipline shader _IsOperative);
    /// cables to a station hidden by the level slider cast shadows only.
    /// Mirrors Timberborn.ZiplineSystem.ZiplineCableRenderer (1.1.2.4).
    /// </summary>
    public class CableRenderer : ILoadableSingleton, IUpdatableSingleton
    {
        private static readonly string CableTemplatePath = "Models/ZiplineCable/ZiplineCable.blueprint";
        private const float PowerCheckInterval = 0.5f;

        private static readonly CableRendererSpec DefaultSpec = new CableRendererSpec
        {
            SagPerLength = 0.015f,
            MaxSag = 0.45f,
            SegmentsPerStrand = 8
        };

        private readonly CableConnectionService _cableConnectionService;
        private readonly PowerTransferStationRegistry _registry;
        private readonly TemplateInstantiator _templateInstantiator;
        private readonly ISpecService _specService;
        private readonly RootObjectProvider _rootObjectProvider;
        private readonly MaterialColorer _materialColorer;
        private readonly Highlighter _highlighter;
        private readonly EventBus _eventBus;

        private readonly Dictionary<PowerCableKey, CableLoopModel> _models = new Dictionary<PowerCableKey, CableLoopModel>();
        private CableRendererSpec _spec;
        private Blueprint _cableTemplate;
        private Transform _root;
        private bool _templateMissing;
        private bool _layerVisibilityChanged;
        private float _nextPowerCheck;

        public CableRenderer(CableConnectionService cableConnectionService, PowerTransferStationRegistry registry,
                            TemplateInstantiator templateInstantiator, ISpecService specService,
                            RootObjectProvider rootObjectProvider, MaterialColorer materialColorer, Highlighter highlighter,
                            EventBus eventBus)
        {
            _cableConnectionService = cableConnectionService;
            _registry = registry;
            _templateInstantiator = templateInstantiator;
            _specService = specService;
            _rootObjectProvider = rootObjectProvider;
            _materialColorer = materialColorer;
            _highlighter = highlighter;
            _eventBus = eventBus;
        }

        public void Load()
        {
            _cableConnectionService.LinksChanged += OnLinksChanged;
            _registry.StationFinished += OnStationFinished;
            _eventBus.Register(this);
        }

        public void UpdateSingleton()
        {
            if (_layerVisibilityChanged)
            {
                _layerVisibilityChanged = false;
                foreach (KeyValuePair<PowerCableKey, CableLoopModel> entry in _models)
                {
                    UpdateShadowOnly(entry.Key, entry.Value);
                }
            }
            if (Time.unscaledTime >= _nextPowerCheck)
            {
                _nextPowerCheck = Time.unscaledTime + PowerCheckInterval;
                foreach (KeyValuePair<PowerCableKey, CableLoopModel> entry in _models)
                {
                    bool operative = IsOperative(entry.Key.First, entry.Key.Second);
                    if (operative != entry.Value.IsOperative)
                    {
                        entry.Value.SetOperative(operative);
                    }
                }
            }
        }

        [OnEvent]
        public void OnMaxVisibleLevelChanged(MaxVisibleLevelChangedEvent maxVisibleLevelChangedEvent)
        {
            // Applied in UpdateSingleton, like ZiplineCableRenderer, once model visibility has been updated.
            _layerVisibilityChanged = true;
        }

        /// <summary>A free-standing model (for previews). Returns null if the template is unavailable.</summary>
        public CableLoopModel CreateModel()
        {
            // Lazy: callers (links restored on load, the preview) may run before or after our Load().
            if (_root == null)
            {
                _root = _rootObjectProvider.CreateRootObject("CablePowerTransferCables").transform;
            }
            if (_spec == null)
            {
                _spec = _specService.GetSpecs<CableRendererSpec>().FirstOrDefault();
                if (_spec == null || _spec.SegmentsPerStrand < 1)
                {
                    ModLog.Warn("CableRenderer blueprint not found or invalid; using defaults.");
                    _spec = DefaultSpec;
                }
            }
            if (_cableTemplate == null && !_templateMissing)
            {
                try
                {
                    _cableTemplate = _specService.GetBlueprint(CableTemplatePath);
                }
                catch (Exception e)
                {
                    _templateMissing = true;
                    ModLog.Error($"cable template '{CableTemplatePath}' not found, cables will be invisible: {e.Message}");
                }
            }
            if (_cableTemplate == null)
            {
                return null;
            }
            return new CableLoopModel(_materialColorer, _highlighter, _spec,
                                      () => _templateInstantiator.Instantiate(_cableTemplate, _root));
        }

        public void Highlight(PowerTransferStation station, PowerTransferStation other, Color color)
        {
            if (_models.TryGetValue(new PowerCableKey(station, other), out CableLoopModel model))
            {
                model.Highlight(color);
            }
        }

        public void Unhighlight(PowerTransferStation station, PowerTransferStation other)
        {
            if (_models.TryGetValue(new PowerCableKey(station, other), out CableLoopModel model))
            {
                model.Unhighlight();
            }
        }

        private void OnLinksChanged(PowerTransferStation station, PowerTransferStation other)
        {
            var key = new PowerCableKey(station, other);
            bool linked = station.IsLinkedTo(other);
            if (linked && !_models.ContainsKey(key))
            {
                CableLoopModel model = CreateModel();
                if (model == null)
                {
                    return;
                }
                model.Update(key.First.CableAnchorPoint, key.Second.CableAnchorPoint, key.First.PulleyRadius);
                model.SetGreyscale(!station.IsFinished || !other.IsFinished);
                model.SetOperative(IsOperative(station, other));
                _models.Add(key, model);
                UpdateShadowOnly(key, model);
            }
            else if (!linked && _models.TryGetValue(key, out CableLoopModel existing))
            {
                existing.Destroy();
                _models.Remove(key);
            }
        }

        private void OnStationFinished(PowerTransferStation station)
        {
            foreach (PowerTransferStation partner in station.CablePartners)
            {
                if (_models.TryGetValue(new PowerCableKey(station, partner), out CableLoopModel model))
                {
                    model.SetGreyscale(!station.IsFinished || !partner.IsFinished);
                }
            }
        }

        private static bool IsOperative(PowerTransferStation station, PowerTransferStation other)
        {
            // Same idea as vanilla MechanicalNodeAnimator for intermediary nodes: move while active and powered.
            return station.IsPowered && other.IsPowered && station.IsCableConnectedTo(other);
        }

        private static void UpdateShadowOnly(PowerCableKey key, CableLoopModel model)
        {
            model.SetShadowOnly(!key.First.IsAnyModelShown || !key.Second.IsAnyModelShown);
        }
    }
}
