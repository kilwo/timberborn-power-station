using System;
using System.Collections.Generic;
using RopePower.Ropes;
using RopePower.Stations;
using Timberborn.BlueprintSystem;
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
    /// Mirrors Timberborn.ZiplineSystem.ZiplineCableRenderer (1.1.2.4). Ropes to unfinished stations are drawn greyscale.
    /// </summary>
    public class RopeRenderer : ILoadableSingleton
    {
        private static readonly string CableTemplatePath = "Models/ZiplineCable/ZiplineCable.blueprint";

        private readonly RopeConnectionService _ropeConnectionService;
        private readonly PowerTransferStationRegistry _registry;
        private readonly TemplateInstantiator _templateInstantiator;
        private readonly ISpecService _specService;
        private readonly RootObjectProvider _rootObjectProvider;
        private readonly MaterialColorer _materialColorer;
        private readonly Highlighter _highlighter;

        private readonly Dictionary<RopeKey, RopeCableModel> _models = new Dictionary<RopeKey, RopeCableModel>();
        private Blueprint _cableTemplate;
        private Transform _root;
        private bool _templateMissing;

        public RopeRenderer(RopeConnectionService ropeConnectionService, PowerTransferStationRegistry registry,
                            TemplateInstantiator templateInstantiator, ISpecService specService,
                            RootObjectProvider rootObjectProvider, MaterialColorer materialColorer, Highlighter highlighter)
        {
            _ropeConnectionService = ropeConnectionService;
            _registry = registry;
            _templateInstantiator = templateInstantiator;
            _specService = specService;
            _rootObjectProvider = rootObjectProvider;
            _materialColorer = materialColorer;
            _highlighter = highlighter;
        }

        public void Load()
        {
            _ropeConnectionService.LinksChanged += OnLinksChanged;
            _registry.StationFinished += OnStationFinished;
        }

        /// <summary>A free-standing model (for previews). Returns null if the template is unavailable.</summary>
        public RopeCableModel CreateModel()
        {
            // Lazy: callers (links restored on load, the preview) may run before or after our Load().
            if (_root == null)
            {
                _root = _rootObjectProvider.CreateRootObject("RopePowerRopes").transform;
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
            return new RopeCableModel(_materialColorer, _highlighter,
                                      _templateInstantiator.Instantiate(_cableTemplate, _root),
                                      _templateInstantiator.Instantiate(_cableTemplate, _root));
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
                model.Update(station.RopeAnchorPoint, other.RopeAnchorPoint);
                model.SetGreyscale(!station.IsFinished || !other.IsFinished);
                _models.Add(key, model);
            }
            else if (!linked && _models.TryGetValue(key, out RopeCableModel model))
            {
                model.Destroy();
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
    }
}
