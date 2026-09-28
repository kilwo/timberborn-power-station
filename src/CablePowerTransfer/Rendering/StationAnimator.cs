using System.Collections.Generic;
using Timberborn.BaseComponentSystem;
using Timberborn.BlueprintSystem;
using Timberborn.BlockSystem;
using Timberborn.Coordinates;
using Timberborn.MechanicalSystem;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;
using Timberborn.TimbermeshAnimations;
using Timberborn.TimeSystem;
using UnityEngine;

namespace CablePowerTransfer.Rendering
{
    public record StationAnimatorSpec : ComponentSpec;

    /// <summary>
    /// Runs the station's animations while it's powered: the pulley (main model) and the four input stubs, which are
    /// separate models ("Stub" + base Direction3D in the blueprint) so each can turn the same way as the shaft or
    /// generator it's connected to. Replaces the vanilla MechanicalNodeAnimator, which only drives one animator.
    /// Enable/speed follow Timberborn.ModularShafts.ModularShaftAnimator (1.1.2.4).
    /// </summary>
    public class StationAnimator : TickableComponent, IAwakableComponent, IFinishedStateListener, IUnfinishedStateListener
    {
        private static readonly string StubPrefix = "Stub";

        private readonly NonlinearAnimationManager _nonlinearAnimationManager;
        private readonly EventBus _eventBus;
        private readonly List<IAnimator> _animators = new List<IAnimator>();
        // By base (unrotated) face direction. Transputs themselves are rebuilt on every finished/unfinished change,
        // so they're looked up fresh on each update rather than cached.
        private readonly Dictionary<Direction3D, IAnimator> _stubs = new Dictionary<Direction3D, IAnimator>();
        private readonly Dictionary<IAnimator, bool> _playingBackwards = new Dictionary<IAnimator, bool>();
        private MechanicalNode _mechanicalNode;
        private bool _collected;

        public StationAnimator(NonlinearAnimationManager nonlinearAnimationManager, EventBus eventBus)
        {
            _nonlinearAnimationManager = nonlinearAnimationManager;
            _eventBus = eventBus;
        }

        public void Awake()
        {
            _mechanicalNode = GetComponent<MechanicalNode>();
            DisableComponent();
        }

        public void OnEnterFinishedState()
        {
            EnableComponent();
            _eventBus.Register(this);
            UpdateAnimation();
        }

        public void OnExitFinishedState()
        {
            DisableComponent();
            _eventBus.Unregister(this);
            StopAnimators();
        }

        public void OnEnterUnfinishedState()
        {
            StopAnimators();
        }

        public void OnExitUnfinishedState()
        {
        }

        public override void Tick()
        {
            UpdateAnimation();
        }

        [OnEvent]
        public void OnCurrentSpeedChanged(CurrentSpeedChangedEvent currentSpeedChangedEvent)
        {
            UpdateAnimation();
        }

        private void UpdateAnimation()
        {
            CollectAnimators();
            UpdateStubDirections();
            bool animated = _mechanicalNode.ActiveAndPowered && _mechanicalNode.PowerEfficiency > 0f;
            foreach (IAnimator animator in _animators)
            {
                animator.Enabled = animated;
                if (animated)
                {
                    animator.Speed = _mechanicalNode.PowerEfficiency * _nonlinearAnimationManager.SpeedMultiplier;
                }
            }
        }

        // Each stub turns so that its face flag is the opposite of the neighbour's (connected faces turn together).
        // The stub models turn "Normal" when played forwards (see tools/TimbermeshGen PowerTransferStationModel), so
        // a stub plays backwards when its face must be Reversed. Unconnected stubs follow the first connected one.
        private void UpdateStubDirections()
        {
            // Our finished-state callback can run before MechanicalNode's has built its transputs (e.g. on load).
            if (_mechanicalNode.Transputs.IsDefault)
            {
                return;
            }
            bool? reference = null;
            foreach (Transput transput in _mechanicalNode.Transputs)
            {
                bool? neighbourReversed = _stubs.ContainsKey(transput.BaseDirection)
                    ? ShaftFaceRotation.NeighbourFaceReversed(transput)
                    : null;
                if (neighbourReversed.HasValue)
                {
                    reference = !neighbourReversed.Value;
                    break;
                }
            }
            foreach (Transput transput in _mechanicalNode.Transputs)
            {
                if (!_stubs.TryGetValue(transput.BaseDirection, out IAnimator animator))
                {
                    continue;
                }
                bool? neighbourReversed = ShaftFaceRotation.NeighbourFaceReversed(transput);
                bool reversed = neighbourReversed.HasValue ? !neighbourReversed.Value : reference ?? false;
                if (!_playingBackwards.TryGetValue(animator, out bool current) || current != reversed)
                {
                    animator.PlayBackwards = reversed;
                    _playingBackwards[animator] = reversed;
                }
            }
        }

        // Models are imported with the building's children, so look them up lazily rather than in Awake.
        private void CollectAnimators()
        {
            if (_collected)
            {
                return;
            }
            _animators.AddRange(GameObject.GetComponentsInChildren<IAnimator>(true));
            foreach (Direction3D face in new[] { Direction3D.Down, Direction3D.Left, Direction3D.Up, Direction3D.Right })
            {
                Transform stub = FindChild(GameObject.transform, StubPrefix + face);
                IAnimator animator = stub ? stub.GetComponent<IAnimator>() : null;
                if (animator != null)
                {
                    _stubs[face] = animator;
                }
            }
            _collected = _animators.Count > 0;
            if (_collected && _stubs.Count != 4)
            {
                ModLog.Warn($"station animator found {_stubs.Count} of 4 input stubs; missing stubs won't match shafts");
            }
        }

        private void StopAnimators()
        {
            foreach (IAnimator animator in _animators)
            {
                animator.Enabled = false;
            }
        }

        private static Transform FindChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                {
                    return child;
                }
                Transform found = FindChild(child, name);
                if (found)
                {
                    return found;
                }
            }
            return null;
        }
    }
}
