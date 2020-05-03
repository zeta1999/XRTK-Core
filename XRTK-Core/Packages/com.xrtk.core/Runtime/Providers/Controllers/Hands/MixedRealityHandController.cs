﻿// Copyright (c) XRTK. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using UnityEngine;
using XRTK.Definitions.Controllers;
using XRTK.Definitions.Controllers.Hands;
using XRTK.Definitions.Devices;
using XRTK.Definitions.InputSystem;
using XRTK.Definitions.Utilities;
using XRTK.Extensions;
using XRTK.Interfaces.Providers.Controllers;
using XRTK.Interfaces.Providers.Controllers.Hands;
using XRTK.Services;

namespace XRTK.Providers.Controllers.Hands
{
    /// <summary>
    /// Platform agnostic hand controller type.
    /// </summary>
    [System.Runtime.InteropServices.Guid("B18A9A6C-E5FD-40AE-89E9-9822415EC62B")]
    public class MixedRealityHandController : BaseController, IMixedRealityHandController
    {
        /// <inheritdoc />
        public MixedRealityHandController() : base() { }

        /// <inheritdoc />
        public MixedRealityHandController(IMixedRealityControllerDataProvider controllerDataProvider, TrackingState trackingState, Handedness controllerHandedness, MixedRealityControllerMappingProfile controllerMappingProfile)
            : base(controllerDataProvider, trackingState, controllerHandedness, controllerMappingProfile)
        { }

        ~MixedRealityHandController()
        {
            if (LastPose != null)
            {
                MixedRealityToolkit.InputSystem?.RaiseOnInputUp(InputSource, ControllerHandedness, new MixedRealityInputAction(int.MaxValue, LastPose.Id, AxisType.Digital));
                LastPose = null;
            }
        }

        private const float NEW_VELOCITY_WEIGHT = .2f;
        private const float CURRENT_VELOCITY_WEIGHT = .8f;
        private const int POSE_FRAME_BUFFER_SIZE = 5;

        private readonly int velocityUpdateFrameInterval = 9;
        private readonly Dictionary<TrackedHandBounds, Bounds[]> bounds = new Dictionary<TrackedHandBounds, Bounds[]>();
        private readonly Dictionary<TrackedHandJoint, MixedRealityPose> jointPoses = new Dictionary<TrackedHandJoint, MixedRealityPose>();
        private readonly Queue<bool> isPinchingBuffer = new Queue<bool>(POSE_FRAME_BUFFER_SIZE);
        private readonly Queue<bool> isPointingBuffer = new Queue<bool>(POSE_FRAME_BUFFER_SIZE);

        private int velocityUpdateFrame = 0;
        private float deltaTimeStart = 0;

        private Vector3 lastPalmNormal = Vector3.zero;
        private Vector3 lastPalmPosition = Vector3.zero;

        /// <inheritdoc />
        public override MixedRealityInteractionMapping[] DefaultInteractions { get; } =
        {
            new MixedRealityInteractionMapping("Spatial Pointer", AxisType.SixDof, DeviceInputType.SpatialPointer),
            new MixedRealityInteractionMapping("Select", AxisType.Digital, DeviceInputType.Select),
            new MixedRealityInteractionMapping("Point", AxisType.Digital, DeviceInputType.ButtonPress),
            new MixedRealityInteractionMapping("Grab", AxisType.SingleAxis, DeviceInputType.TriggerPress),
            new MixedRealityInteractionMapping("Index Finger Pose", AxisType.SixDof, DeviceInputType.IndexFinger),
            new MixedRealityInteractionMapping("Tracked Pose", AxisType.Raw, DeviceInputType.Hand)
        };

        /// <inheritdoc />
        public override MixedRealityInteractionMapping[] DefaultLeftHandedInteractions => DefaultInteractions;

        /// <inheritdoc />
        public override MixedRealityInteractionMapping[] DefaultRightHandedInteractions => DefaultInteractions;

        /// <summary>
        /// Gets the current palm normal of the hand controller.
        /// </summary>
        private Vector3 PalmNormal => TryGetJointPose(TrackedHandJoint.Palm, out var pose) ? -pose.Up : Vector3.zero;

        /// <summary>
        /// Is pinching state from the previous update frame.
        /// </summary>
        private bool LastIsPinching { get; set; }

        /// <inheritdoc />
        public bool IsPinching { get; private set; }

        /// <summary>
        /// Is pointing state from the previous update frame.
        /// </summary>
        private bool LastIsPointing { get; set; }

        /// <inheritdoc />
        public bool IsPointing { get; private set; }

        /// <summary>
        /// Is grabbing state from the previous update frame.
        /// </summary>
        private bool LastIsGrabbing { get; set; }

        /// <inheritdoc />
        public bool IsGrabbing { get; private set; }

        /// <summary>
        /// The last pose recognized for this hand controller.
        /// </summary>
        private HandControllerPoseDefinition LastPose { get; set; }

        /// <inheritdoc />
        public HandControllerPoseDefinition Pose { get; private set; }

        /// <summary>
        /// The hand's pointer pose.
        /// </summary>
        private MixedRealityPose PointerPose { get; set; }

        /// <summary>
        /// Updates the hand controller with new hand data input.
        /// </summary>
        /// <param name="handData">Updated hand data.</param>
        public void UpdateController(HandData handData)
        {
            if (!Enabled) { return; }

            var lastTrackingState = TrackingState;
            LastIsPinching = IsPinching;
            LastIsPointing = IsPointing;
            LastPose = Pose;

            // Update internals.
            UpdateJoints(handData);
            UpdateIsPinching(handData);
            UpdateIsIsPointing(handData);
            UpdateBounds();
            UpdateVelocity();
            PointerPose = handData.PointerPose;
            Pose = handData.TrackedPose;

            // We assume hand controller position and roation to be available
            // if we can successfully retrieve the wrist pose.
            if (TryGetJointPose(TrackedHandJoint.Wrist, out var wristPose))
            {
                IsPositionAvailable = true;
                IsPositionApproximate = false;
                IsRotationAvailable = true;
                TrackingState = handData.IsTracked ? TrackingState.Tracked : TrackingState.NotTracked;
            }

            // Update controller tracking state.
            if (lastTrackingState != TrackingState)
            {
                MixedRealityToolkit.InputSystem?.RaiseSourceTrackingStateChanged(InputSource, this, TrackingState);
            }

            // Update controller pose.
            if (TrackingState == TrackingState.Tracked)
            {
                MixedRealityToolkit.InputSystem?.RaiseSourcePoseChanged(InputSource, this, wristPose);
            }

            // Update hand controller interaction mappings.
            UpdateInteractionMappings();

            // Raise general hand data update for visualizers.
            MixedRealityToolkit.InputSystem?.RaiseHandDataInputChanged(InputSource, ControllerHandedness, handData);
        }

        /// <summary>
        /// Updates the controller's joint poses using provided hand data.
        /// </summary>
        /// <param name="handData">The updated hand data for this controller.</param>
        private void UpdateJoints(HandData handData)
        {
            for (int i = 0; i < HandData.JointCount; i++)
            {
                var handJoint = (TrackedHandJoint)i;

                if (TryGetJointPose(handJoint, out _))
                {
                    jointPoses[handJoint] = handData.Joints[i];
                }
                else
                {
                    jointPoses.Add(handJoint, handData.Joints[i]);
                }
            }
        }

        #region Hand Bounds Implementation

        /// <inheritdoc />
        public bool TryGetBounds(TrackedHandBounds handBounds, out Bounds[] newBounds)
        {
            if (bounds.ContainsKey(handBounds))
            {
                newBounds = bounds[handBounds];
                return true;
            }

            newBounds = null;
            return false;
        }

        private void UpdateBounds()
        {
            var handControllerDataProvider = (IMixedRealityHandControllerDataProvider)ControllerDataProvider;

            if (handControllerDataProvider.HandPhysicsEnabled && handControllerDataProvider.BoundsMode == HandBoundsMode.Hand)
            {
                UpdateHandBounds();
            }
            else if (handControllerDataProvider.HandPhysicsEnabled && handControllerDataProvider.BoundsMode == HandBoundsMode.Fingers)
            {
                UpdatePalmBounds();
                UpdateThumbBounds();
                UpdateIndexFingerBounds();
                UpdateMiddleFingerBounds();
                UpdateRingFingerBounds();
                UpdatePinkyFingerBounds();
            }
        }

        private void UpdatePalmBounds()
        {
            if (TryGetJointPose(TrackedHandJoint.PinkyMetacarpal, out var pinkyMetacarpalPose) &&
                TryGetJointPose(TrackedHandJoint.PinkyKnuckle, out var pinkyKnucklePose) &&
                TryGetJointPose(TrackedHandJoint.RingMetacarpal, out var ringMetacarpalPose) &&
                TryGetJointPose(TrackedHandJoint.RingKnuckle, out var ringKnucklePose) &&
                TryGetJointPose(TrackedHandJoint.MiddleMetacarpal, out var middleMetacarpalPose) &&
                TryGetJointPose(TrackedHandJoint.MiddleKnuckle, out var middleKnucklePose) &&
                TryGetJointPose(TrackedHandJoint.IndexMetacarpal, out var indexMetacarpalPose) &&
                TryGetJointPose(TrackedHandJoint.IndexKnuckle, out var indexKnucklePose))
            {
                // Palm bounds are a composite of each finger's metacarpal -> knuckle joint bounds.
                // Excluding the thumb here.
                var palmBounds = new Bounds[4];

                // Index
                var indexPalmBounds = new Bounds(indexMetacarpalPose.Position, Vector3.zero);
                indexPalmBounds.Encapsulate(indexKnucklePose.Position);
                palmBounds[0] = indexPalmBounds;

                // Middle
                var middlePalmBounds = new Bounds(middleMetacarpalPose.Position, Vector3.zero);
                middlePalmBounds.Encapsulate(middleKnucklePose.Position);
                palmBounds[1] = middlePalmBounds;

                // Ring
                var ringPalmBounds = new Bounds(ringMetacarpalPose.Position, Vector3.zero);
                ringPalmBounds.Encapsulate(ringKnucklePose.Position);
                palmBounds[2] = ringPalmBounds;

                // Pinky
                var pinkyPalmBounds = new Bounds(pinkyMetacarpalPose.Position, Vector3.zero);
                pinkyPalmBounds.Encapsulate(pinkyKnucklePose.Position);
                palmBounds[3] = pinkyPalmBounds;

                // Update cached bounds entry.
                if (bounds.ContainsKey(TrackedHandBounds.Palm))
                {
                    bounds[TrackedHandBounds.Palm] = palmBounds;
                }
                else
                {
                    bounds.Add(TrackedHandBounds.Palm, palmBounds);
                }
            }
        }

        private void UpdateHandBounds()
        {
            if (TryGetJointPose(TrackedHandJoint.Palm, out var palmPose))
            {
                var newHandBounds = new Bounds(palmPose.Position, Vector3.zero);

                foreach (var kvp in jointPoses)
                {
                    if (kvp.Key == TrackedHandJoint.None ||
                        kvp.Key == TrackedHandJoint.Palm)
                    {
                        continue;
                    }

                    newHandBounds.Encapsulate(kvp.Value.Position);
                }

                if (bounds.ContainsKey(TrackedHandBounds.Hand))
                {
                    bounds[TrackedHandBounds.Hand] = new[] { newHandBounds };
                }
                else
                {
                    bounds.Add(TrackedHandBounds.Hand, new[] { newHandBounds });
                }
            }
        }

        private void UpdateThumbBounds()
        {
            if (TryGetJointPose(TrackedHandJoint.ThumbMetacarpalJoint, out var knucklePose) &&
                TryGetJointPose(TrackedHandJoint.ThumbProximalJoint, out var middlePose) &&
                TryGetJointPose(TrackedHandJoint.ThumbTip, out var tipPose))
            {
                // Thumb bounds include metacarpal -> proximal and proximal -> tip bounds.
                var thumbBounds = new Bounds[2];

                // Knuckle to middle joint bounds.
                var knuckleToMiddleBounds = new Bounds(knucklePose.Position, Vector3.zero);
                knuckleToMiddleBounds.Encapsulate(middlePose.Position);
                thumbBounds[0] = knuckleToMiddleBounds;

                // Middle to tip joint bounds.
                var middleToTipBounds = new Bounds(middlePose.Position, Vector3.zero);
                middleToTipBounds.Encapsulate(tipPose.Position);
                thumbBounds[1] = middleToTipBounds;

                // Update cached bounds entry.
                if (bounds.ContainsKey(TrackedHandBounds.Thumb))
                {
                    bounds[TrackedHandBounds.Thumb] = thumbBounds;
                }
                else
                {
                    bounds.Add(TrackedHandBounds.Thumb, thumbBounds);
                }
            }
        }

        private void UpdateIndexFingerBounds()
        {
            if (TryGetJointPose(TrackedHandJoint.IndexKnuckle, out var knucklePose) &&
                TryGetJointPose(TrackedHandJoint.IndexMiddleJoint, out var middlePose) &&
                TryGetJointPose(TrackedHandJoint.IndexTip, out var tipPose))
            {
                // Index finger bounds include knuckle -> middle and middle -> tip bounds.
                var indexFingerBounds = new Bounds[2];

                // Knuckle to middle joint bounds.
                var knuckleToMiddleBounds = new Bounds(knucklePose.Position, Vector3.zero);
                knuckleToMiddleBounds.Encapsulate(middlePose.Position);
                indexFingerBounds[0] = knuckleToMiddleBounds;

                // Middle to tip joint bounds.
                var middleToTipBounds = new Bounds(middlePose.Position, Vector3.zero);
                middleToTipBounds.Encapsulate(tipPose.Position);
                indexFingerBounds[1] = middleToTipBounds;

                // Update cached bounds entry.
                if (bounds.ContainsKey(TrackedHandBounds.IndexFinger))
                {
                    bounds[TrackedHandBounds.IndexFinger] = indexFingerBounds;
                }
                else
                {
                    bounds.Add(TrackedHandBounds.IndexFinger, indexFingerBounds);
                }
            }
        }

        private void UpdateMiddleFingerBounds()
        {
            if (TryGetJointPose(TrackedHandJoint.MiddleKnuckle, out var knucklePose) &&
                TryGetJointPose(TrackedHandJoint.MiddleMiddleJoint, out var middlePose) &&
                TryGetJointPose(TrackedHandJoint.MiddleTip, out var tipPose))
            {
                // Middle finger bounds include knuckle -> middle and middle -> tip bounds.
                var middleFingerBounds = new Bounds[2];

                // Knuckle to middle joint bounds.
                var knuckleToMiddleBounds = new Bounds(knucklePose.Position, Vector3.zero);
                knuckleToMiddleBounds.Encapsulate(middlePose.Position);
                middleFingerBounds[0] = knuckleToMiddleBounds;

                // Middle to tip joint bounds.
                var middleToTipBounds = new Bounds(middlePose.Position, Vector3.zero);
                middleToTipBounds.Encapsulate(tipPose.Position);
                middleFingerBounds[1] = middleToTipBounds;

                // Update cached bounds entry.
                if (bounds.ContainsKey(TrackedHandBounds.MiddleFinger))
                {
                    bounds[TrackedHandBounds.MiddleFinger] = middleFingerBounds;
                }
                else
                {
                    bounds.Add(TrackedHandBounds.MiddleFinger, middleFingerBounds);
                }
            }
        }

        private void UpdateRingFingerBounds()
        {
            if (TryGetJointPose(TrackedHandJoint.RingKnuckle, out var knucklePose) &&
                TryGetJointPose(TrackedHandJoint.RingMiddleJoint, out var middlePose) &&
                TryGetJointPose(TrackedHandJoint.RingTip, out var tipPose))
            {
                // Ring finger bounds include knuckle -> middle and middle -> tip bounds.
                var ringFingerBounds = new Bounds[2];

                // Knuckle to middle joint bounds.
                var knuckleToMiddleBounds = new Bounds(knucklePose.Position, Vector3.zero);
                knuckleToMiddleBounds.Encapsulate(middlePose.Position);
                ringFingerBounds[0] = knuckleToMiddleBounds;

                // Middle to tip joint bounds.
                var middleToTipBounds = new Bounds(middlePose.Position, Vector3.zero);
                middleToTipBounds.Encapsulate(tipPose.Position);
                ringFingerBounds[1] = middleToTipBounds;

                // Update cached bounds entry.
                if (bounds.ContainsKey(TrackedHandBounds.RingFinger))
                {
                    bounds[TrackedHandBounds.RingFinger] = ringFingerBounds;
                }
                else
                {
                    bounds.Add(TrackedHandBounds.RingFinger, ringFingerBounds);
                }
            }
        }

        private void UpdatePinkyFingerBounds()
        {
            if (TryGetJointPose(TrackedHandJoint.PinkyKnuckle, out var knucklePose) &&
                TryGetJointPose(TrackedHandJoint.PinkyMiddleJoint, out var middlePose) &&
                TryGetJointPose(TrackedHandJoint.PinkyTip, out var tipPose))
            {
                // Pinky finger bounds include knuckle -> middle and middle -> tip bounds.
                var pinkyBounds = new Bounds[2];

                // Knuckle to middle joint bounds.
                var knuckleToMiddleBounds = new Bounds(knucklePose.Position, Vector3.zero);
                knuckleToMiddleBounds.Encapsulate(middlePose.Position);
                pinkyBounds[0] = knuckleToMiddleBounds;

                // Middle to tip joint bounds.
                var middleToTipBounds = new Bounds(middlePose.Position, Vector3.zero);
                middleToTipBounds.Encapsulate(tipPose.Position);
                pinkyBounds[1] = middleToTipBounds;

                // Update cached bounds entry.
                if (bounds.ContainsKey(TrackedHandBounds.Pinky))
                {
                    bounds[TrackedHandBounds.Pinky] = pinkyBounds;
                }
                else
                {
                    bounds.Add(TrackedHandBounds.Pinky, pinkyBounds);
                }
            }
        }

        #endregion

        #region Interaction Mappings

        protected virtual void UpdateInteractionMappings()
        {
            for (int i = 0; i < Interactions.Length; i++)
            {
                var interactionMapping = Interactions[i];
                switch (interactionMapping.InputType)
                {
                    case DeviceInputType.SpatialPointer:
                        UpdateSpatialPointerMapping(interactionMapping);
                        break;
                    case DeviceInputType.Select:
                        UpdateSelectMapping(interactionMapping);
                        break;
                    case DeviceInputType.ButtonPress:
                        UpdatePointingMapping(interactionMapping);
                        break;
                    case DeviceInputType.SpatialGrip:
                        UpdateSpatialGripMapping(interactionMapping);
                        break;
                    case DeviceInputType.IndexFinger:
                        UpdateIndexFingerMapping(interactionMapping);
                        break;
                    case DeviceInputType.Hand:
                        UpdateHandPoseMapping(interactionMapping);
                        break;
                }

                interactionMapping.RaiseInputAction(InputSource, ControllerHandedness);
            }
        }

        private void UpdateSpatialGripMapping(MixedRealityInteractionMapping interactionMapping)
        {
            Debug.Assert(interactionMapping.AxisType == AxisType.SixDof);
            if (TryGetJointPose(TrackedHandJoint.Palm, out var palmPose))
            {
                interactionMapping.PoseData = palmPose;
            }
        }

        private void UpdateSpatialPointerMapping(MixedRealityInteractionMapping interactionMapping)
        {
            Debug.Assert(interactionMapping.AxisType == AxisType.SixDof);
            interactionMapping.PoseData = PointerPose;
        }

        private void UpdateSelectMapping(MixedRealityInteractionMapping interactionMapping)
        {
            Debug.Assert(interactionMapping.AxisType == AxisType.Digital);

            if (!LastIsPinching && IsPinching)
            {
                interactionMapping.BoolData = true;
            }
            else if (LastIsPinching && !IsPinching)
            {
                interactionMapping.BoolData = false;
            }
            else if (IsPinching)
            {
                interactionMapping.BoolData = LastIsPinching;
            }
        }

        private void UpdatePointingMapping(MixedRealityInteractionMapping interactionMapping)
        {
            Debug.Assert(interactionMapping.AxisType == AxisType.Digital);

            if (!LastIsPointing && IsPointing)
            {
                interactionMapping.BoolData = true;
            }
            else if (LastIsPointing && !IsPointing)
            {
                interactionMapping.BoolData = false;
            }
            else if (IsPointing)
            {
                interactionMapping.BoolData = LastIsPointing;
            }
        }

        private void UpdateIndexFingerMapping(MixedRealityInteractionMapping interactionMapping)
        {
            Debug.Assert(interactionMapping.AxisType == AxisType.SixDof);
            if (TryGetJointPose(TrackedHandJoint.IndexTip, out var indexTipPose))
            {
                interactionMapping.PoseData = indexTipPose;
            }
        }

        private void UpdateHandPoseMapping(MixedRealityInteractionMapping interactionMapping)
        {
            Debug.Assert(interactionMapping.AxisType == AxisType.Raw);

            if (string.Equals(LastPose?.Id, Pose?.Id))
            {
                interactionMapping.RawData = LastPose?.Id;
            }
            else if (!string.Equals(LastPose?.Id, Pose?.Id))
            {
                interactionMapping.RawData = Pose?.Id;
            }
        }

        #endregion

        /// <summary>
        /// Updates the controller's velocity / angular velocity.
        /// </summary>
        private void UpdateVelocity()
        {
            if (velocityUpdateFrame == 0)
            {
                deltaTimeStart = Time.unscaledTime;
                lastPalmPosition = GetJointPosition(TrackedHandJoint.Palm);
                lastPalmNormal = PalmNormal;
            }
            else if (velocityUpdateFrame == velocityUpdateFrameInterval)
            {
                // Update linear velocity.
                var deltaTime = Time.unscaledTime - deltaTimeStart;
                var newVelocity = (GetJointPosition(TrackedHandJoint.Palm) - lastPalmPosition) / deltaTime;
                Velocity = (Velocity * CURRENT_VELOCITY_WEIGHT) + (newVelocity * NEW_VELOCITY_WEIGHT);

                // Update angular velocity.
                var currentPalmNormal = PalmNormal;
                var rotation = Quaternion.FromToRotation(lastPalmNormal, currentPalmNormal);
                var rotationRate = rotation.eulerAngles * Mathf.Deg2Rad;
                AngularVelocity = rotationRate / deltaTime;
            }

            velocityUpdateFrame++;
            velocityUpdateFrame = velocityUpdateFrame > velocityUpdateFrameInterval ? 0 : velocityUpdateFrame;
        }

        /// <summary>
        /// Updates the hand controller's internal is pinching state.
        /// Instead of updating the value for each frame, is pinching state
        /// is buffered for a few frames to stabilize and avoid false positives.
        /// </summary>
        /// <param name="handData">The hand data received for the current hand update frame.</param>
        private void UpdateIsPinching(HandData handData)
        {
            if (handData.IsTracked)
            {
                var isPinchingThisFrame = handData.IsPinching;
                if (isPinchingBuffer.Count < POSE_FRAME_BUFFER_SIZE)
                {
                    isPinchingBuffer.Enqueue(isPinchingThisFrame);
                    IsPinching = false;
                }
                else
                {
                    isPinchingBuffer.Dequeue();
                    isPinchingBuffer.Enqueue(isPinchingThisFrame);

                    isPinchingThisFrame = true;
                    for (int i = 0; i < isPinchingBuffer.Count; i++)
                    {
                        var value = isPinchingBuffer.Dequeue();

                        if (!value)
                        {
                            isPinchingThisFrame = false;
                        }

                        isPinchingBuffer.Enqueue(value);
                    }

                    IsPinching = isPinchingThisFrame;
                }
            }
            else
            {
                isPinchingBuffer.Clear();
                IsPinching = false;
            }
        }

        /// <summary>
        /// Updates the hand controller's internal is pointing state.
        /// Instead of updating the value for each frame, is pointing state
        /// is buffered for a few frames to stabilize and avoid false positives.
        /// </summary>
        /// <param name="handData">The hand data received for the current hand update frame.</param>
        private void UpdateIsIsPointing(HandData handData)
        {
            if (handData.IsTracked)
            {
                var isPointingThisFrame = handData.IsPointing;
                if (isPointingBuffer.Count < POSE_FRAME_BUFFER_SIZE)
                {
                    isPointingBuffer.Enqueue(isPointingThisFrame);
                    IsPointing = false;
                }
                else
                {
                    isPointingBuffer.Dequeue();
                    isPointingBuffer.Enqueue(isPointingThisFrame);

                    isPointingThisFrame = true;
                    for (int i = 0; i < isPointingBuffer.Count; i++)
                    {
                        var value = isPointingBuffer.Dequeue();

                        if (!value)
                        {
                            isPointingThisFrame = false;
                        }

                        isPointingBuffer.Enqueue(value);
                    }

                    IsPointing = isPointingThisFrame;
                }
            }
            else
            {
                isPointingBuffer.Clear();
                IsPointing = false;
            }
        }

        /// <inheritdoc />
        public virtual bool TryGetJointPose(TrackedHandJoint joint, out MixedRealityPose pose) => jointPoses.TryGetValue(joint, out pose);

        private Vector3 GetJointPosition(TrackedHandJoint joint) => TryGetJointPose(joint, out var pose) ? pose.Position : Vector3.zero;
    }
}