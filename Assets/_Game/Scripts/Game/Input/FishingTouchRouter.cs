using UnityEngine;

namespace Fishing.Game
{
    /// <summary>Classifies unzoned touch gestures while retaining each accepted finger by identity.</summary>
    public sealed class FishingTouchRouter
    {
        private const int MaxPointers = 16;
        private const float HoldSeconds = 0.1f;

        private enum PointerIntent : byte
        {
            Pending,
            Holding,
            Dragging
        }

        private struct PointerState
        {
            public bool Active;
            public bool Seen;
            public bool Ended;
            public bool Canceled;
            public int Id;
            public Vector2 Origin;
            public Vector2 Position;
            public float StartedAt;
            public PointerIntent Intent;
        }

        private readonly PointerState[] _pointers = new PointerState[MaxPointers];
        private Rect _area;
        private bool _allowReel;
        private bool _dragSteering;
        private float _now;
        private float _dragThresholdSquared;
        private int _steeringSlot = -1;

        /// <summary>Whether an accepted finger currently owns horizontal steering.</summary>
        public bool Steering => _steeringSlot >= 0;
        /// <summary>Whether steering ownership was acquired during this frame.</summary>
        public bool SteeringStarted { get; private set; }
        /// <summary>Current horizontal position of the steering finger in screen pixels.</summary>
        public float PointerX { get; private set; }
        /// <summary>Initial horizontal position of the steering gesture in screen pixels.</summary>
        public float SteeringOriginX { get; private set; }
        /// <summary>Whether an accepted touch is holding or pulsing the reel during this frame.</summary>
        public bool Reeling { get; private set; }

        /// <summary>Starts a frame using the safe area, current phase, control mode, time and drag threshold.</summary>
        public void BeginFrame(
            Rect area,
            bool allowReel,
            bool dragSteering,
            float now,
            float dragThresholdPixels)
        {
            _area = area;
            _allowReel = allowReel;
            _dragSteering = dragSteering;
            _now = now;
            float threshold = Mathf.Max(0f, dragThresholdPixels);
            _dragThresholdSquared = threshold * threshold;
            SteeringStarted = false;
            Reeling = false;

            for (int i = 0; i < _pointers.Length; i++)
            {
                if (!_pointers[i].Active)
                    continue;

                _pointers[i].Seen = false;
                _pointers[i].Ended = false;
                _pointers[i].Canceled = false;
            }
        }

        /// <summary>Samples one pointer, admitting only non-UI beginnings inside the safe area.</summary>
        public void Process(int id, Vector2 position, TouchPhase phase, bool overUi)
        {
            int slot = FindPointer(id);
            if (slot >= 0)
            {
                _pointers[slot].Seen = true;
                _pointers[slot].Position = position;
                _pointers[slot].Ended = phase == TouchPhase.Ended;
                _pointers[slot].Canceled = phase == TouchPhase.Canceled;
                return;
            }

            if (phase != TouchPhase.Began || overUi || !_area.Contains(position))
                return;

            slot = FindFreeSlot();
            if (slot < 0)
                return;

            _pointers[slot] = new PointerState
            {
                Active = true,
                Seen = true,
                Id = id,
                Origin = position,
                Position = position,
                StartedAt = _now,
                Intent = _dragSteering && !_allowReel
                    ? PointerIntent.Dragging
                    : PointerIntent.Pending
            };
        }

        /// <summary>Resolves gestures and drops ended, canceled or missing pointers without synthetic taps.</summary>
        public void EndFrame()
        {
            bool reelPulse = false;

            for (int i = 0; i < _pointers.Length; i++)
            {
                if (!_pointers[i].Active)
                    continue;

                if (!_pointers[i].Seen || _pointers[i].Canceled)
                {
                    Release(i);
                    continue;
                }

                float distanceSquared = (_pointers[i].Position - _pointers[i].Origin).sqrMagnitude;
                bool crossedDragThreshold = distanceSquared > _dragThresholdSquared;

                if (_pointers[i].Ended)
                {
                    // Release can arrive before the first hold-confirmation sample; elapsed time alone must not drop a tap.
                    reelPulse |= _allowReel && _dragSteering &&
                        _pointers[i].Intent == PointerIntent.Pending && !crossedDragThreshold;
                    Release(i);
                    continue;
                }

                if (!_dragSteering)
                {
                    _pointers[i].Intent = _allowReel
                        ? PointerIntent.Holding
                        : PointerIntent.Pending;
                }
                else if (crossedDragThreshold || _pointers[i].Intent == PointerIntent.Dragging)
                {
                    _pointers[i].Intent = PointerIntent.Dragging;
                }
                else if (_now - _pointers[i].StartedAt >= HoldSeconds)
                {
                    _pointers[i].Intent = PointerIntent.Holding;
                }
            }

            ResolveSteering();
            Reeling = _allowReel && (reelPulse || HasHoldingPointer());
        }

        /// <summary>Clears all pointer ownership on pause, state changes and restart.</summary>
        public void Reset()
        {
            for (int i = 0; i < _pointers.Length; i++)
                _pointers[i] = default;

            _steeringSlot = -1;
            SteeringStarted = false;
            PointerX = 0f;
            SteeringOriginX = 0f;
            Reeling = false;
        }

        private int FindPointer(int id)
        {
            for (int i = 0; i < _pointers.Length; i++)
            {
                if (_pointers[i].Active && _pointers[i].Id == id)
                    return i;
            }

            return -1;
        }

        private int FindFreeSlot()
        {
            for (int i = 0; i < _pointers.Length; i++)
            {
                if (!_pointers[i].Active)
                    return i;
            }

            return -1;
        }

        private void ResolveSteering()
        {
            if (!_dragSteering || !IsSteeringCandidate(_steeringSlot))
                _steeringSlot = -1;

            if (_steeringSlot < 0)
            {
                int selectedId = int.MaxValue;
                for (int i = 0; i < _pointers.Length; i++)
                {
                    if (!IsSteeringCandidate(i) || _pointers[i].Id >= selectedId)
                        continue;

                    _steeringSlot = i;
                    selectedId = _pointers[i].Id;
                }

                SteeringStarted = _steeringSlot >= 0;
            }

            if (_steeringSlot < 0)
                return;

            PointerX = _pointers[_steeringSlot].Position.x;
            SteeringOriginX = _pointers[_steeringSlot].Origin.x;
        }

        private bool IsSteeringCandidate(int slot)
        {
            return slot >= 0 &&
                _pointers[slot].Active &&
                _pointers[slot].Seen &&
                _pointers[slot].Intent == PointerIntent.Dragging;
        }

        private bool HasHoldingPointer()
        {
            for (int i = 0; i < _pointers.Length; i++)
            {
                if (_pointers[i].Active && _pointers[i].Seen &&
                    _pointers[i].Intent == PointerIntent.Holding)
                    return true;
            }

            return false;
        }

        private void Release(int slot)
        {
            if (_steeringSlot == slot)
                _steeringSlot = -1;

            _pointers[slot] = default;
        }
    }
}
