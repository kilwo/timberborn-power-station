using RopePower.Ropes;
using RopePower.Stations;
using Timberborn.ConstructionMode;
using Timberborn.InputSystem;
using Timberborn.Localization;
using Timberborn.SelectionSystem;
using Timberborn.ToolSystem;
using Timberborn.ToolSystemUI;
using Timberborn.UISound;

namespace RopePower.UI
{
    /// <summary>
    /// "Add rope" picking mode: hover a station to preview (green = valid, red + reason = invalid), click to link,
    /// Esc cancels (ToolService handles InputService.Cancel). Mirrors Timberborn.ZiplineSystemUI.ZiplineConnectionAddingTool.
    /// </summary>
    public class RopeConnectionAddingTool : ITool, IToolDescriptor, IInputProcessor, IConstructionModeEnabler
    {
        private static readonly string DescriptionLocKey = "RopePower.PickDestination";
        private static readonly string CursorKey = "PickObjectCursor";

        private readonly InputService _inputService;
        private readonly SelectableObjectRaycaster _selectableObjectRaycaster;
        private readonly EntitySelectionService _entitySelectionService;
        private readonly ToolService _toolService;
        private readonly RopeConnectionService _ropeConnectionService;
        private readonly CursorService _cursorService;
        private readonly ILoc _loc;
        private readonly RopePreviewRenderer _previewRenderer;
        private readonly UISoundController _uiSoundController;
        private readonly Highlighter _highlighter;

        private PowerTransferStation _origin;

        public RopeConnectionAddingTool(InputService inputService, SelectableObjectRaycaster selectableObjectRaycaster,
                                        EntitySelectionService entitySelectionService, ToolService toolService,
                                        RopeConnectionService ropeConnectionService, CursorService cursorService, ILoc loc,
                                        RopePreviewRenderer previewRenderer, UISoundController uiSoundController,
                                        Highlighter highlighter)
        {
            _inputService = inputService;
            _selectableObjectRaycaster = selectableObjectRaycaster;
            _entitySelectionService = entitySelectionService;
            _toolService = toolService;
            _ropeConnectionService = ropeConnectionService;
            _cursorService = cursorService;
            _loc = loc;
            _previewRenderer = previewRenderer;
            _uiSoundController = uiSoundController;
            _highlighter = highlighter;
        }

        public void SwitchTo(PowerTransferStation origin)
        {
            _origin = origin;
        }

        public void Enter()
        {
            if (!_origin)
            {
                ModLog.Warn("rope tool entered without an origin station");
                return;
            }
            _highlighter.HighlightPrimary(_origin, RopeColors.Origin);
            _inputService.AddInputProcessor(this);
            _cursorService.SetCursor(CursorKey);
        }

        public void Exit()
        {
            _previewRenderer.Hide();
            _inputService.RemoveInputProcessor(this);
            _cursorService.ResetCursor();
            if (_origin)
            {
                _highlighter.UnhighlightPrimary(_origin);
                if (!_origin.IsDeleted)
                {
                    _entitySelectionService.Select(_origin);
                }
            }
            _origin = null;
        }

        public ToolDescription DescribeTool()
        {
            return new ToolDescription.Builder().AddPrioritizedSection(_loc.T(DescriptionLocKey)).Build();
        }

        public bool ProcessInput()
        {
            if (!_origin || _origin.IsDeleted)
            {
                _toolService.SwitchToDefaultTool();
                return true;
            }
            PowerTransferStation target = _selectableObjectRaycaster.TryHitSelectableObject(out SelectableObject hitObject)
                ? hitObject.GetComponent<PowerTransferStation>()
                : null;
            RopeLinkError error = _ropeConnectionService.Validate(_origin, target);
            if (_inputService.MainMouseButtonDown && !_inputService.MouseOverUI)
            {
                if (error == RopeLinkError.None)
                {
                    Link(target);
                    _uiSoundController.PlayClickSound();
                    return true;
                }
                _uiSoundController.PlayCantDoSound();
            }
            _previewRenderer.Draw(_origin, target, error);
            return false;
        }

        private void Link(PowerTransferStation target)
        {
            PowerTransferStation origin = _origin;
            _ropeConnectionService.Link(origin, target);
            _toolService.SwitchToDefaultTool();
            _entitySelectionService.Select(target);
            // Like ziplines: keep chaining from the new station while it has a free slot.
            if (_ropeConnectionService.HasFreeSlot(target))
            {
                SwitchTo(target);
                _toolService.SwitchTool(this);
            }
        }
    }
}
