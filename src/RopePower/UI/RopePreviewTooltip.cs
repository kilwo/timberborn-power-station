using RopePower.Ropes;
using RopePower.Stations;
using Timberborn.CoreUI;
using Timberborn.Localization;
using Timberborn.SingletonSystem;
using Timberborn.TooltipSystem;
using Timberborn.UIFormatters;
using UnityEngine;
using UnityEngine.UIElements;

namespace RopePower.UI
{
    /// <summary>
    /// Distance / inclination / reason tooltip while picking a rope target. Reuses the vanilla
    /// "Game/ZiplineConnectionTooltip" view and its generic texts; logic mirrors Timberborn.ZiplineSystemUI.ZiplinePreviewTooltip.
    /// </summary>
    public class RopePreviewTooltip : ILoadableSingleton
    {
        private static readonly string CrossClass = "cross-red";

        private readonly RopeConnectionService _ropeConnectionService;
        private readonly VisualElementLoader _visualElementLoader;
        private readonly ITooltipRegistrar _tooltipRegistrar;
        private readonly ILoc _loc;

        private readonly Phrase _distancePhrase = Phrase.New("Zipline.Distance").FormatDistance<int>().FormatDistance<int>();
        private readonly Phrase _inclinationPhrase = Phrase.New("Zipline.Inclination").FormatAngle<int>().FormatAngle<int>();

        private VisualElement _root;
        private Label _distanceLabel;
        private VisualElement _distanceWarning;
        private VisualElement _distanceIcon;
        private Label _inclinationLabel;
        private VisualElement _inclinationWarning;
        private VisualElement _inclinationIcon;
        private VisualElement _warnings;
        private VisualElement _districtsWarning;
        private VisualElement _blockedWarning;
        private VisualElement _tooManyConnectionsWarning;

        public RopePreviewTooltip(RopeConnectionService ropeConnectionService, VisualElementLoader visualElementLoader,
                                  ITooltipRegistrar tooltipRegistrar, ILoc loc)
        {
            _ropeConnectionService = ropeConnectionService;
            _visualElementLoader = visualElementLoader;
            _tooltipRegistrar = tooltipRegistrar;
            _loc = loc;
        }

        public void Load()
        {
            _root = _visualElementLoader.LoadVisualElement("Game/ZiplineConnectionTooltip");
            _distanceLabel = _root.Q<Label>("Distance");
            _distanceWarning = _root.Q<VisualElement>("DistanceWarning");
            _distanceIcon = _root.Q<VisualElement>("DistanceIcon");
            _inclinationLabel = _root.Q<Label>("Inclination");
            _inclinationWarning = _root.Q<VisualElement>("InclinationWarning");
            _inclinationIcon = _root.Q<VisualElement>("InclinationIcon");
            _warnings = _root.Q<VisualElement>("WarningsWrapper");
            _districtsWarning = _root.Q<VisualElement>("DistrictsWarning");
            _blockedWarning = _root.Q<VisualElement>("BlockedWarning");
            _tooManyConnectionsWarning = _root.Q<VisualElement>("TooManyConnectionsWarning");
        }

        public void Show(PowerTransferStation station, PowerTransferStation other, RopeLinkError error)
        {
            bool distanceValid = _ropeConnectionService.DistanceIsValid(station, other, out float distance, out float maxDistance);
            bool inclinationValid = _ropeConnectionService.InclinationIsValid(station, other, out float inclination, out float maxInclination);
            _distanceLabel.text = _loc.T(_distancePhrase, Mathf.CeilToInt(distance), (int)maxDistance);
            _distanceWarning.ToggleDisplayStyle(!distanceValid);
            _distanceIcon.EnableInClassList(CrossClass, !distanceValid);
            _inclinationLabel.text = _loc.T(_inclinationPhrase, Mathf.CeilToInt(inclination), (int)maxInclination);
            _inclinationWarning.ToggleDisplayStyle(!inclinationValid);
            _inclinationIcon.EnableInClassList(CrossClass, !inclinationValid);
            HideWarnings();
            switch (error)
            {
                case RopeLinkError.DifferentDistricts:
                    ShowWarning(_districtsWarning);
                    break;
                case RopeLinkError.SourceFull:
                case RopeLinkError.TargetFull:
                    ShowWarning(_tooManyConnectionsWarning);
                    break;
                case RopeLinkError.Obstructed:
                    ShowWarning(_blockedWarning);
                    break;
            }
            _tooltipRegistrar.ShowPriority(_root);
        }

        public void Hide()
        {
            _tooltipRegistrar.HidePriority();
        }

        private void ShowWarning(VisualElement warning)
        {
            _warnings.ToggleDisplayStyle(true);
            warning.ToggleDisplayStyle(true);
        }

        private void HideWarnings()
        {
            _warnings.ToggleDisplayStyle(false);
            _districtsWarning.ToggleDisplayStyle(false);
            _blockedWarning.ToggleDisplayStyle(false);
            _tooManyConnectionsWarning.ToggleDisplayStyle(false);
        }
    }
}
