using CablePowerTransfer.Cables;
using CablePowerTransfer.Stations;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using Timberborn.Localization;
using UnityEngine.UIElements;

namespace CablePowerTransfer.UI
{
    /// <summary>
    /// Entity panel section listing a station's cables (partner icon + length, remove button) and an "Add connection" button.
    /// Reuses the vanilla "Game/EntityPanel/ZiplineTowerFragment" view with our own title.
    /// Mirrors Timberborn.ZiplineSystemUI.ZiplineTowerFragment (1.1.2.4).
    /// </summary>
    public class StationCablesFragment : IEntityPanelFragment
    {
        private static readonly string TitleLocKey = "CablePowerTransfer.Cables";

        private readonly VisualElementLoader _visualElementLoader;
        private readonly CableConnectionButtonFactory _buttonFactory;
        private readonly CableConnectionService _cableConnectionService;
        private readonly ILoc _loc;

        private VisualElement _root;
        private VisualElement _buttons;
        private PowerTransferStation _station;
        private int _shownCableCount;

        public StationCablesFragment(VisualElementLoader visualElementLoader, CableConnectionButtonFactory buttonFactory,
                                    CableConnectionService cableConnectionService, ILoc loc)
        {
            _visualElementLoader = visualElementLoader;
            _buttonFactory = buttonFactory;
            _cableConnectionService = cableConnectionService;
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
            // Rebuild if cables changed while selected (e.g. the partner was demolished).
            if (_station && _station.CablePartners.Count != _shownCableCount)
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
            _shownCableCount = _station.CablePartners.Count;
            foreach (PowerTransferStation partner in _station.CablePartners)
            {
                _buttonFactory.CreateConnection(_buttons, _station, partner);
            }
            int maxCables = System.Math.Min(_cableConnectionService.MaxCablesPerStation, _station.CableSlotCapacity);
            int slot = _shownCableCount;
            if (_cableConnectionService.HasFreeSlot(_station))
            {
                _buttonFactory.CreateAddConnection(_buttons, _station);
                slot++;
            }
            for (; slot < maxCables; slot++)
            {
                _buttonFactory.CreateEmpty(_buttons);
            }
        }
    }
}
