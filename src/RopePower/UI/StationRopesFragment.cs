using RopePower.Ropes;
using RopePower.Stations;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using Timberborn.Localization;
using UnityEngine.UIElements;

namespace RopePower.UI
{
    /// <summary>
    /// Entity panel section listing a station's ropes (partner icon + length, remove button) and an "Add connection" button.
    /// Reuses the vanilla "Game/EntityPanel/ZiplineTowerFragment" view with our own title.
    /// Mirrors Timberborn.ZiplineSystemUI.ZiplineTowerFragment (1.1.2.4).
    /// </summary>
    public class StationRopesFragment : IEntityPanelFragment
    {
        private static readonly string TitleLocKey = "RopePower.Ropes";

        private readonly VisualElementLoader _visualElementLoader;
        private readonly RopeConnectionButtonFactory _buttonFactory;
        private readonly RopeConnectionService _ropeConnectionService;
        private readonly ILoc _loc;

        private VisualElement _root;
        private VisualElement _buttons;
        private PowerTransferStation _station;
        private int _shownRopeCount;

        public StationRopesFragment(VisualElementLoader visualElementLoader, RopeConnectionButtonFactory buttonFactory,
                                    RopeConnectionService ropeConnectionService, ILoc loc)
        {
            _visualElementLoader = visualElementLoader;
            _buttonFactory = buttonFactory;
            _ropeConnectionService = ropeConnectionService;
            _loc = loc;
        }

        public VisualElement InitializeFragment()
        {
            _root = _visualElementLoader.LoadVisualElement("Game/EntityPanel/ZiplineTowerFragment");
            _root.Q<Label>("Title").text = _loc.T(TitleLocKey);
            _buttons = _root.Q<VisualElement>("Buttons");
            _root.ToggleDisplayStyle(false);
            return _root;
        }

        public void ShowFragment(BaseComponent entity)
        {
            _station = entity.GetComponent<PowerTransferStation>();
            if (_station)
            {
                CreateButtons();
                _root.ToggleDisplayStyle(true);
            }
        }

        public void UpdateFragment()
        {
            // Rebuild if ropes changed while selected (e.g. the partner was demolished).
            if (_station && _station.RopePartners.Count != _shownRopeCount)
            {
                _buttons.Clear();
                CreateButtons();
            }
        }

        public void ClearFragment()
        {
            _station = null;
            _buttons.Clear();
            _root.ToggleDisplayStyle(false);
        }

        private void CreateButtons()
        {
            _shownRopeCount = _station.RopePartners.Count;
            foreach (PowerTransferStation partner in _station.RopePartners)
            {
                _buttonFactory.CreateConnection(_buttons, _station, partner);
            }
            int maxRopes = System.Math.Min(_ropeConnectionService.MaxRopesPerStation, _station.RopeSlotCapacity);
            int slot = _shownRopeCount;
            if (_ropeConnectionService.HasFreeSlot(_station))
            {
                _buttonFactory.CreateAddConnection(_buttons, _station);
                slot++;
            }
            for (; slot < maxRopes; slot++)
            {
                _buttonFactory.CreateEmpty(_buttons);
            }
        }
    }
}
