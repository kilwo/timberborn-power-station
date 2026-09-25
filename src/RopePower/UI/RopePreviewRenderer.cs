using System.Collections.Generic;
using RopePower.Rendering;
using RopePower.Ropes;
using RopePower.Stations;
using Timberborn.BlockSystem;
using Timberborn.SelectionSystem;

namespace RopePower.UI
{
    /// <summary>
    /// Green/red preview rope while picking a target, with blocking objects highlighted and a reason tooltip.
    /// Mirrors Timberborn.ZiplineSystemUI.ZiplinePreviewCableRenderer (1.1.2.4).
    /// </summary>
    public class RopePreviewRenderer
    {
        private readonly RopeRenderer _ropeRenderer;
        private readonly RopeBlockService _ropeBlockService;
        private readonly RollingHighlighter _rollingHighlighter;
        private readonly RopePreviewTooltip _tooltip;
        private readonly List<BlockObject> _blockingObjects = new List<BlockObject>();

        private RopeCableModel _preview;

        public RopePreviewRenderer(RopeRenderer ropeRenderer, RopeBlockService ropeBlockService,
                                   RollingHighlighter rollingHighlighter, RopePreviewTooltip tooltip)
        {
            _ropeRenderer = ropeRenderer;
            _ropeBlockService = ropeBlockService;
            _rollingHighlighter = rollingHighlighter;
            _tooltip = tooltip;
        }

        public void Draw(PowerTransferStation station, PowerTransferStation other, RopeLinkError error)
        {
            if (!other || other == station || error == RopeLinkError.AlreadyLinked || error == RopeLinkError.NoTarget)
            {
                Hide();
                return;
            }
            _rollingHighlighter.UnhighlightAllPrimary();
            bool connectable = error == RopeLinkError.None;
            _preview = _preview ?? _ropeRenderer.CreateModel();
            if (_preview != null)
            {
                _preview.SetVisible(true);
                _preview.Update(station.RopeAnchorPoint, other.RopeAnchorPoint);
                _preview.Highlight(connectable ? RopeColors.Connectable : RopeColors.NotConnectable);
            }
            if (error == RopeLinkError.Obstructed)
            {
                _ropeBlockService.GetBlockingObjects(station, other, _blockingObjects);
                _rollingHighlighter.HighlightPrimary(_blockingObjects, RopeColors.NotConnectable);
                _blockingObjects.Clear();
            }
            _tooltip.Show(station, other, error);
        }

        public void Hide()
        {
            _preview?.SetVisible(false);
            _rollingHighlighter.UnhighlightAllPrimary();
            _tooltip.Hide();
        }
    }
}
