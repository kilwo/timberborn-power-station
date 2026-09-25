using System;
using RopePower.Rendering;
using RopePower.Ropes;
using RopePower.Stations;
using Timberborn.CoreUI;
using Timberborn.EntitySystem;
using Timberborn.Localization;
using Timberborn.SelectionSystem;
using Timberborn.ToolSystem;
using Timberborn.TooltipSystem;
using Timberborn.UIFormatters;
using UnityEngine;
using UnityEngine.UIElements;

namespace RopePower.UI
{
    /// <summary>
    /// Connection buttons for the station panel, reusing the vanilla "Game/EntityPanel/ZiplineConnectionButton" view.
    /// Mirrors Timberborn.ZiplineSystemUI.ZiplineConnectionButtonFactory (1.1.2.4).
    /// </summary>
    public class RopeConnectionButtonFactory
    {
        private static readonly string ButtonElementName = "Game/EntityPanel/ZiplineConnectionButton";
        private static readonly string PlusIconClass = "icon--plus";
        private static readonly string AddConnectionLocKey = "Zipline.AddConnection";
        private static readonly string RemoveConnectionLocKey = "Zipline.RemoveConnection";

        private readonly VisualElementLoader _visualElementLoader;
        private readonly RopeConnectionAddingTool _addingTool;
        private readonly ToolService _toolService;
        private readonly EntitySelectionService _entitySelectionService;
        private readonly RopeConnectionService _ropeConnectionService;
        private readonly RopeRenderer _ropeRenderer;
        private readonly Highlighter _highlighter;
        private readonly ITooltipRegistrar _tooltipRegistrar;
        private readonly ILoc _loc;

        private readonly Phrase _lengthPhrase = Phrase.New("RopePower.RopeLength").FormatDistance<int>();

        public RopeConnectionButtonFactory(VisualElementLoader visualElementLoader, RopeConnectionAddingTool addingTool,
                                           ToolService toolService, EntitySelectionService entitySelectionService,
                                           RopeConnectionService ropeConnectionService, RopeRenderer ropeRenderer,
                                           Highlighter highlighter, ITooltipRegistrar tooltipRegistrar, ILoc loc)
        {
            _visualElementLoader = visualElementLoader;
            _addingTool = addingTool;
            _toolService = toolService;
            _entitySelectionService = entitySelectionService;
            _ropeConnectionService = ropeConnectionService;
            _ropeRenderer = ropeRenderer;
            _highlighter = highlighter;
            _tooltipRegistrar = tooltipRegistrar;
            _loc = loc;
        }

        public void CreateConnection(VisualElement root, PowerTransferStation owner, PowerTransferStation partner)
        {
            Button button = Create(root);
            button.RegisterCallback<MouseEnterEvent>(_ => Highlight(owner, partner));
            button.RegisterCallback<MouseLeaveEvent>(_ => Unhighlight(owner, partner));
            button.RegisterCallback<DetachFromPanelEvent>(_ => Unhighlight(owner, partner));
            button.RegisterCallback<ClickEvent>(_ => _entitySelectionService.SelectAndFocusOn(partner));
            _ropeConnectionService.DistanceIsValid(owner, partner, out float distance, out _);
            SetName(button, _loc.T(_lengthPhrase, Mathf.CeilToInt(distance)));
            SetIcon(button, partner.GetComponent<LabeledEntity>().Image);
            SetRemoveButton(button, () => RemoveConnection(owner, partner));
        }

        public void CreateAddConnection(VisualElement root, PowerTransferStation owner)
        {
            Button button = Create(root);
            button.RegisterCallback<ClickEvent>(_ => AddConnection(owner));
            SetName(button, _loc.T(AddConnectionLocKey));
            SetIcon(button, null, PlusIconClass);
            SetRemoveButton(button);
        }

        public void CreateEmpty(VisualElement root)
        {
            Button button = Create(root);
            SetName(button);
            SetRemoveButton(button);
            button.SetEnabled(false);
        }

        private Button Create(VisualElement root)
        {
            Button button = _visualElementLoader.LoadVisualElement(ButtonElementName).Q<Button>();
            root.Add(button);
            return button;
        }

        private void Highlight(PowerTransferStation owner, PowerTransferStation partner)
        {
            _highlighter.HighlightPrimary(partner, RopeColors.Connectable);
            _ropeRenderer.Highlight(owner, partner, RopeColors.Connectable);
        }

        private void Unhighlight(PowerTransferStation owner, PowerTransferStation partner)
        {
            if (owner && partner)
            {
                _ropeRenderer.Unhighlight(owner, partner);
            }
            if (partner)
            {
                _highlighter.UnhighlightPrimary(partner);
            }
        }

        private void RemoveConnection(PowerTransferStation owner, PowerTransferStation partner)
        {
            Unhighlight(owner, partner);
            _ropeConnectionService.Unlink(owner, partner);
            // Reselect to rebuild the panel, as the zipline panel does.
            _entitySelectionService.Unselect();
            _entitySelectionService.Select(owner);
        }

        private void AddConnection(PowerTransferStation owner)
        {
            _addingTool.SwitchTo(owner);
            _toolService.SwitchTool(_addingTool);
        }

        private static void SetName(VisualElement root, string text = null)
        {
            Label label = root.Q<Label>("Name");
            label.text = text;
            label.ToggleDisplayStyle(text != null);
        }

        private static void SetIcon(VisualElement root, Sprite sprite, string className = null)
        {
            Image image = root.Q<Image>("Icon");
            if (sprite != null)
            {
                image.sprite = sprite;
            }
            if (className != null)
            {
                image.AddToClassList(className);
            }
        }

        private void SetRemoveButton(VisualElement root, Action onClick = null)
        {
            Button button = root.Q<Button>("RemoveConnection");
            if (onClick != null)
            {
                button.RegisterCallback<ClickEvent>(_ => onClick());
                _tooltipRegistrar.RegisterLocalizable(button, RemoveConnectionLocKey);
            }
            else
            {
                button.ToggleDisplayStyle(false);
            }
        }
    }
}
