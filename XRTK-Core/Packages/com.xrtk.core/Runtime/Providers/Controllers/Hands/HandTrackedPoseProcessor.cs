﻿// Copyright (c) XRTK. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using UnityEngine;
using XRTK.Definitions.Controllers.Hands;
using XRTK.Extensions;

namespace XRTK.Providers.Controllers.Hands
{
    /// <summary>
    /// The hand pose processor uses the recorded hand pose definitions
    /// configured in <see cref="Definitions.InputSystem.MixedRealityInputSystemProfile.TrackedPoses"/>
    /// or the platform's <see cref="BaseHandControllerDataProviderProfile.TrackedPoses"/>
    /// and attempts to recognize a hand's current pose during runtime to provide for
    /// <see cref="HandData.TrackedPose"/>.
    /// </summary>
    public sealed class HandTrackedPoseProcessor
    {
        /// <summary>
        /// Creates a new recognizer instance to work on the provided list of poses.
        /// </summary>
        /// <param name="recognizablePoses">Recognizable poses by this recognizer.</param>
        public HandTrackedPoseProcessor(IReadOnlyList<HandControllerPoseDefinition> recognizablePoses)
        {
            bakedHandDatas = new HandData[recognizablePoses.Count];
            definitions = new Dictionary<int, HandControllerPoseDefinition>();

            var i = 0;
            foreach (var item in recognizablePoses)
            {
                if (item.DidBake)
                {
                    bakedHandDatas[i] = item.ToHandData();
                    definitions.Add(i, item);

                    i++;
                }
                else
                {
                    throw new ArgumentException($"Pose definition {item.Id} was not baked. Only baked poses are supported for recognition.");
                }
            }
        }

        private const int RECOGNITION_FRAME_DELIMITER = 10;
        private const float CURL_STRENGTH_DELTA_THRESHOLD = .01f;
        private const float GRIP_STRENGTH_DELTA_THRESHOLD = .02f;

        private readonly HandData[] bakedHandDatas;
        private readonly Dictionary<int, HandControllerPoseDefinition> definitions;
        private int passedFramesSinceRecognition = 0;

        /// <summary>
        /// Attempts to recognize a hand pose.
        /// </summary>
        /// <param name="handData">The hand data to compare against.</param>
        public void Process(HandData handData)
        {
            if (handData.IsTracked)
            {
                // Recognition is pretty expensive so we don't want to
                // do it every frame.
                if (passedFramesSinceRecognition < RECOGNITION_FRAME_DELIMITER)
                {
                    passedFramesSinceRecognition++;
                    return;
                }

                passedFramesSinceRecognition = 0;
                var currentHighestProbability = 0f;
                HandControllerPoseDefinition recognizedPose = null;

                for (int i = 0; i < bakedHandDatas.Length; i++)
                {
                    var bakedHandData = bakedHandDatas[i];
                    var probability = Compare(handData, bakedHandData);

                    if (probability > currentHighestProbability)
                    {
                        currentHighestProbability = probability;
                        recognizedPose = definitions[i];
                    }
                }

                handData.TrackedPose = recognizedPose;

                if (handData.TrackedPose != null)
                {
                    Debug.Log(handData.TrackedPose.Id);
                }
            }
            else
            {
                // Easy game when hand is not tracked, there is no pose.
                handData.TrackedPose = null;
                passedFramesSinceRecognition = 0;
            }
        }

        private static float Compare(HandData runtimeHandData, HandData bakedHandData)
        {
            const int totalTests = 6;
            var passedTests = 0;

            // If the gripping states are not the same it is very unlikely
            // poses are the same so we can quit right away.
            if (runtimeHandData.IsGripping == bakedHandData.IsGripping)
            {
                var runtimeGripStrength = runtimeHandData.GripStrength;
                var bakedGripStrength = bakedHandData.GripStrength;
                if (Math.Abs(runtimeGripStrength - bakedGripStrength) <= GRIP_STRENGTH_DELTA_THRESHOLD)
                {
                    passedTests++;
                }

                var runtimeThumbCurl = runtimeHandData.FingerCurlStrengths[(int)HandFinger.Thumb];
                var bakedThumbCurl = bakedHandData.FingerCurlStrengths[(int)HandFinger.Thumb];
                if (Math.Abs(runtimeThumbCurl - bakedThumbCurl) <= CURL_STRENGTH_DELTA_THRESHOLD)
                {
                    passedTests++;
                }

                var runtimeIndexCurl = runtimeHandData.FingerCurlStrengths[(int)HandFinger.Index];
                var bakedIndexCurl = bakedHandData.FingerCurlStrengths[(int)HandFinger.Index];
                if (Math.Abs(runtimeIndexCurl - bakedIndexCurl) <= CURL_STRENGTH_DELTA_THRESHOLD)
                {
                    passedTests++;
                }

                var runtimeMiddleCurl = runtimeHandData.FingerCurlStrengths[(int)HandFinger.Middle];
                var bakedMiddleCurl = bakedHandData.FingerCurlStrengths[(int)HandFinger.Middle];
                if (Math.Abs(runtimeMiddleCurl - bakedMiddleCurl) <= CURL_STRENGTH_DELTA_THRESHOLD)
                {
                    passedTests++;
                }

                var runtimeRingCurl = runtimeHandData.FingerCurlStrengths[(int)HandFinger.Ring];
                var bakedRingCurl = bakedHandData.FingerCurlStrengths[(int)HandFinger.Ring];
                if (Math.Abs(runtimeRingCurl - bakedRingCurl) <= CURL_STRENGTH_DELTA_THRESHOLD)
                {
                    passedTests++;
                }

                var runtimeLittleCurl = runtimeHandData.FingerCurlStrengths[(int)HandFinger.Little];
                var bakedLittleCurl = bakedHandData.FingerCurlStrengths[(int)HandFinger.Little];
                if (Math.Abs(runtimeLittleCurl - bakedLittleCurl) <= CURL_STRENGTH_DELTA_THRESHOLD)
                {
                    passedTests++;
                }
            }

            // The more tests have passed, the more likely it is
            // the poses are the same.
            return passedTests / totalTests;
        }
    }
}