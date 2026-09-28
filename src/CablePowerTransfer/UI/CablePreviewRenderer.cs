using System.Collections.Generic;
using CablePowerTransfer.Rendering;
using CablePowerTransfer.Cables;
using CablePowerTransfer.Stations;
using Timberborn.BlockSystem;
using Timberborn.SelectionSystem;

namespace CablePowerTransfer.UI
{
    /// <summary>
    /// Green/red preview cable while picking a target, with blocking objects highlighted and a reason tooltip.
    /// Mirrors Timberborn.ZiplineSystemUI.ZiplinePreviewCableRenderer (1.1.2.4).
    /// </summary>
    public class CablePreviewRenderer
    {
        private readonly CableRenderer _cableRenderer;
        private readonly PowerCableBlockService _powerCableBlockService;
        private readonly RollingHighlighter _rollingHighlighter;
        private readonly CablePreviewTooltip _tooltip;
        private readonly List<BlockObject> _blockingObjects = new List<BlockObject>();

        private CableLoopModel _preview;

        public CablePreviewRenderer(CableRenderer cableRenderer, PowerCableBlockService powerCableBlockService,
                                   RollingHighlighter rollingHighlighter, CablePreviewTooltip tooltip)
        {
            _cableRenderer = cableRenderer;
            _powerCableBlockService = powerCableBlockService;
            _rollingHighlighter = rollingHighlighter;
            _tooltip = tooltip;
        }

        public void Draw(PowerTransferStation station, PowerTransferStation other, CableLinkError error)
        {
            if (!other || other == station || error == CableLinkError.AlreadyLinked || error == CableLinkError.NoTarget)
            {
                Hide();
                return;
            }
            _rollingHighlighter.UnhighlightAllPrimary();
            bool connectable = error == CableLinkError.None;
            _preview = _preview ?? _cableRenderer.CreateModel();
            if (_preview != null)
            {
                _preview.SetVisible(true);
                _preview.Update(station.CableAnchorPoint, other.CableAnchorPoint, station.PulleyRadius);
                _preview.Highlight(connectable ? CableColors.Connectable : CableColors.NotConnectable);
            }
            if (error == CableLinkError.Obstructed)
            {
                _powerCableBlockService.GetBlockingObjects(station, other, _blockingObjects);
                _rollingHighlighter.HighlightPrimary(_blockingObjects, CableColors.NotConnectable);
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
