using System;
using System.Collections.Generic;
using System.Linq;
using RopePower.Ropes;
using RopePower.Stations;
using Timberborn.BlueprintSystem;
using Timberborn.LevelVisibilitySystem;
using Timberborn.Rendering;
using Timberborn.RootProviders;
using Timberborn.SelectionSystem;
using Timberborn.SingletonSystem;
using Timberborn.TemplateInstantiation;
using UnityEngine;

namespace RopePower.Rendering
{
    /// <summary>
    /// Keeps one RopeCableModel per rope link, plus a model factory for the connection tool preview.
    /// Ropes to unfinished stations are greyscale; ropes on a powered network move (zipline shader _IsOperative);
    /// ropes to a station hidden by the level slider cast shadows only.
    /// Mirrors Timberborn.ZiplineSystem.ZiplineCableRenderer (1.1.2.4).
    /// </summary>
    public class RopeRenderer : ILoadableSingleton, IUpdatableSingleton
    {
        private static readonly string CableTemplatePath = "Models/ZiplineCable/ZiplineCable.blueprint";
        private const float PowerCheckInterval = 0.5f;

        private static readonly RopeRendererSpec DefaultSpec = new RopeRendererSpec
        {
            SagPerLength = 0.015f,
            MaxSag = 0.45f,
            SegmentsPerStrand = 8
        };

        private readonly RopeConnectionService _ropeConnectionService;
        private readonly PowerTransferStationRegistry _registry;
        private readonly TemplateInstantiator _templateInstantiator;
        private readonly ISpecService _specService;
        private readonly RootObjectProvider _rootObjectProvider;
        private readonly MaterialColorer _materialColorer;
        private readonly Highlighter _highlighter;
        private readonly EventBus _eventBus;

        private readonly Dictionary<RopeKey, RopeCableModel> _models = new Dictionary<RopeKey, RopeCableModel>();
        private RopeRendererSpec _spec;
        private Blueprint _cableTemplate;
        private Transform _root;
        private bool _templateMissing;
        private bool _layerVisibilityChanged;
        private float _nextPowerCheck;

        public RopeRenderer(RopeConnectionService ropeConnectionService, PowerTransferStationRegistry registry,
                            TemplateInstantiator templateInstantiator, ISpecService specService,
                            RootObjectProvider rootObjectProvider, MaterialColorer materialColorer, Highlighter highlighter,
                            EventBus eventBus)
        {
            _ropeConnectionService = ropeConnectionService;
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
            _ropeConnectionService.LinksChanged += OnLinksChanged;
            _registry.StationFinished += OnStationFinished;
            _eventBus.Register(this);
        }

        public void UpdateSingleton()
        {
            if (_layerVisibilityChanged)
            {
                _layerVisibilityChanged = false;
                foreach (KeyValuePair<RopeKey, RopeCableModel> entry in _models)
                {
                    UpdateShadowOnly(entry.Key, entry.Value);
                }
            }
            if (Time.unscaledTime >= _nextPowerCheck)
            {
                _nextPowerCheck = Time.unscaledTime + PowerCheckInterval;
                foreach (KeyValuePair<RopeKey, RopeCableModel> entry in _models)
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
        public RopeCableModel CreateModel()
        {
            // Lazy: callers (links restored on load, the preview) may run before or after our Load().
            if (_root == null)
            {
                _root = _rootObjectProvider.CreateRootObject("RopePowerRopes").transform;
            }
            if (_spec == null)
            {
                _spec = _specService.GetSpecs<RopeRendererSpec>().FirstOrDefault();
                if (_spec == null || _spec.SegmentsPerStrand < 1)
                {
                    ModLog.Warn("RopeRenderer blueprint not found or invalid; using defaults.");
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
                    ModLog.Error($"cable template '{CableTemplatePath}' not found, ropes will be invisible: {e.Message}");
                }
            }
            if (_cableTemplate == null)
            {
                return null;
            }
            return new RopeCableModel(_materialColorer, _highlighter, _spec,
                                      () => _templateInstantiator.Instantiate(_cableTemplate, _root));
        }

        public void Highlight(PowerTransferStation station, PowerTransferStation other, Color color)
        {
            if (_models.TryGetValue(new RopeKey(station, other), out RopeCableModel model))
            {
                model.Highlight(color);
            }
        }

        public void Unhighlight(PowerTransferStation station, PowerTransferStation other)
        {
            if (_models.TryGetValue(new RopeKey(station, other), out RopeCableModel model))
            {
                model.Unhighlight();
            }
        }

        private void OnLinksChanged(PowerTransferStation station, PowerTransferStation other)
        {
            var key = new RopeKey(station, other);
            bool linked = station.IsLinkedTo(other);
            if (linked && !_models.ContainsKey(key))
            {
                RopeCableModel model = CreateModel();
                if (model == null)
                {
                    return;
                }
                model.Update(key.First.RopeAnchorPoint, key.Second.RopeAnchorPoint, key.First.PulleyRadius);
                model.SetGreyscale(!station.IsFinished || !other.IsFinished);
                model.SetOperative(IsOperative(station, other));
                _models.Add(key, model);
                UpdateShadowOnly(key, model);
            }
            else if (!linked && _models.TryGetValue(key, out RopeCableModel existing))
            {
                existing.Destroy();
                _models.Remove(key);
            }
        }

        private void OnStationFinished(PowerTransferStation station)
        {
            foreach (PowerTransferStation partner in station.RopePartners)
            {
                if (_models.TryGetValue(new RopeKey(station, partner), out RopeCableModel model))
                {
                    model.SetGreyscale(!station.IsFinished || !partner.IsFinished);
                }
            }
        }

        private static bool IsOperative(PowerTransferStation station, PowerTransferStation other)
        {
            // Same idea as vanilla MechanicalNodeAnimator for intermediary nodes: move while active and powered.
            return station.IsPowered && other.IsPowered && station.IsRopeConnectedTo(other);
        }

        private static void UpdateShadowOnly(RopeKey key, RopeCableModel model)
        {
            model.SetShadowOnly(!key.First.IsAnyModelShown || !key.Second.IsAnyModelShown);
        }
    }
}
