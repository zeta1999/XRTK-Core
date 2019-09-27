﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using XRTK.Attributes;
using XRTK.Definitions.BoundarySystem;
using XRTK.Definitions.Diagnostics;
using XRTK.Definitions.InputSystem;
using XRTK.Definitions.NetworkingSystem;
using XRTK.Definitions.SpatialAwarenessSystem;
using XRTK.Definitions.Utilities;
using XRTK.Interfaces;
using XRTK.Interfaces.BoundarySystem;
using XRTK.Interfaces.CameraSystem;
using XRTK.Interfaces.Diagnostics;
using XRTK.Interfaces.InputSystem;
using XRTK.Interfaces.NetworkingSystem;
using XRTK.Interfaces.SpatialAwarenessSystem;
using XRTK.Interfaces.TeleportSystem;
using XRTK.Services;

namespace XRTK.Definitions
{
    /// <summary>
    /// Configuration profile settings for the Mixed Reality Toolkit.
    /// </summary>
    [CreateAssetMenu(menuName = "Mixed Reality Toolkit/Mixed Reality Toolkit Configuration Profile", fileName = "MixedRealityToolkitConfigurationProfile", order = (int)CreateProfileMenuItemIndices.Configuration)]
    public class MixedRealityToolkitConfigurationProfile : BaseMixedRealityProfile
    {
        #region Camera System

        [SerializeField]
        [Tooltip("Enable the Camera System on Startup.")]
        private bool enableCameraSystem = false;

        /// <summary>
        /// Enable and configure the Camera Profile for the Mixed Reality Toolkit
        /// </summary>
        public bool IsCameraSystemEnabled
        {
            get => CameraProfile != null && cameraSystemType != null && cameraSystemType.Type != null && enableCameraSystem;
            internal set => enableCameraSystem = value;
        }

        [SerializeField]
        [Tooltip("Camera System Class to instantiate at runtime.")]
        [Implements(typeof(IMixedRealityCameraSystem), TypeGrouping.ByNamespaceFlat)]
        private SystemType cameraSystemType;

        /// <summary>
        /// Camera System class to instantiate at runtime.
        /// </summary>
        public SystemType CameraSystemType
        {
            get => cameraSystemType;
            internal set => cameraSystemType = value;
        }


        [SerializeField]
        [Tooltip("Camera profile.")]
        private MixedRealityCameraProfile cameraProfile;

        /// <summary>
        /// Profile for customizing your camera and quality settings based on if your 
        /// head mounted display (HMD) is a transparent device or an occluded device.
        /// </summary>
        public MixedRealityCameraProfile CameraProfile
        {
            get => cameraProfile;
            internal set => cameraProfile = value;
        }

        #endregion Camera System

        #region Input System

        [SerializeField]
        [Tooltip("Enable the Input System on Startup.")]
        private bool enableInputSystem = false;

        /// <summary>
        /// Enable and configure the Input System component for the Mixed Reality Toolkit
        /// </summary>
        public bool IsInputSystemEnabled
        {
            get => inputSystemProfile != null && inputSystemType != null && inputSystemType.Type != null && enableInputSystem;
            internal set => enableInputSystem = value;
        }

        [SerializeField]
        [Tooltip("Input System profile for setting wiring up events and actions to input devices.")]
        private MixedRealityInputSystemProfile inputSystemProfile;

        /// <summary>
        /// Input System profile for setting wiring up events and actions to input devices.
        /// </summary>
        public MixedRealityInputSystemProfile InputSystemProfile
        {
            get => inputSystemProfile;
            internal set => inputSystemProfile = value;
        }

        [SerializeField]
        [Tooltip("Input System Class to instantiate at runtime.")]
        [Implements(typeof(IMixedRealityInputSystem), TypeGrouping.ByNamespaceFlat)]
        private SystemType inputSystemType;

        /// <summary>
        /// Input System Script File to instantiate at runtime.
        /// </summary>
        public SystemType InputSystemType
        {
            get => inputSystemType;
            internal set => inputSystemType = value;
        }

        #endregion Input System

        #region Boundary System

        [SerializeField]
        [Tooltip("Enable the Boundary on Startup")]
        private bool enableBoundarySystem = false;

        /// <summary>
        /// Enable and configure the boundary system.
        /// </summary>
        public bool IsBoundarySystemEnabled
        {
            get => boundarySystemType != null && boundarySystemType.Type != null && enableBoundarySystem && boundaryVisualizationProfile != null;
            internal set => enableBoundarySystem = value;
        }

        [SerializeField]
        [Tooltip("Boundary System Class to instantiate at runtime.")]
        [Implements(typeof(IMixedRealityBoundarySystem), TypeGrouping.ByNamespaceFlat)]
        private SystemType boundarySystemType;

        /// <summary>
        /// Boundary System Script File to instantiate at runtime.
        /// </summary>
        public SystemType BoundarySystemSystemType
        {
            get => boundarySystemType;
            internal set => boundarySystemType = value;
        }

        [SerializeField]
        [Tooltip("Profile for wiring up boundary visualization assets.")]
        private MixedRealityBoundaryVisualizationProfile boundaryVisualizationProfile;

        /// <summary>
        /// Active profile for controller mapping configuration
        /// </summary>
        public MixedRealityBoundaryVisualizationProfile BoundaryVisualizationProfile
        {
            get => boundaryVisualizationProfile;
            internal set => boundaryVisualizationProfile = value;
        }

        #endregion Boundary System

        #region Teleport System

        [SerializeField]
        [Tooltip("Enable the Teleport System on Startup")]
        private bool enableTeleportSystem = false;

        /// <summary>
        /// Enable and configure the boundary system.
        /// </summary>
        public bool IsTeleportSystemEnabled
        {
            get => teleportSystemType != null && teleportSystemType.Type != null && enableTeleportSystem;
            internal set => enableTeleportSystem = value;
        }

        [SerializeField]
        [Tooltip("Boundary System Class to instantiate at runtime.")]
        [Implements(typeof(IMixedRealityTeleportSystem), TypeGrouping.ByNamespaceFlat)]
        private SystemType teleportSystemType;

        /// <summary>
        /// Teleport System Script File to instantiate at runtime.
        /// </summary>
        public SystemType TeleportSystemSystemType
        {
            get => teleportSystemType;
            internal set => teleportSystemType = value;
        }

        #endregion Teleport System

        #region Spatial Awareness System

        [SerializeField]
        [Tooltip("Enable the Spatial Awareness system on Startup")]
        private bool enableSpatialAwarenessSystem = false;

        /// <summary>
        /// Enable and configure the spatial awareness system.
        /// </summary>
        public bool IsSpatialAwarenessSystemEnabled
        {
            get => spatialAwarenessSystemType != null && spatialAwarenessSystemType.Type != null && enableSpatialAwarenessSystem && spatialAwarenessProfile != null;
            internal set => enableSpatialAwarenessSystem = value;
        }

        [SerializeField]
        [Tooltip("Spatial Awareness System Class to instantiate at runtime.")]
        [Implements(typeof(IMixedRealitySpatialAwarenessSystem), TypeGrouping.ByNamespaceFlat)]
        private SystemType spatialAwarenessSystemType;

        /// <summary>
        /// Boundary System Script File to instantiate at runtime.
        /// </summary>
        public SystemType SpatialAwarenessSystemSystemType
        {
            get => spatialAwarenessSystemType;
            internal set => spatialAwarenessSystemType = value;
        }

        [SerializeField]
        [Tooltip("Profile for configuring the Spatial Awareness system.")]
        private MixedRealitySpatialAwarenessSystemProfile spatialAwarenessProfile;

        /// <summary>
        /// Active profile for spatial awareness configuration
        /// </summary>
        public MixedRealitySpatialAwarenessSystemProfile SpatialAwarenessProfile
        {
            get => spatialAwarenessProfile;
            internal set => spatialAwarenessProfile = value;
        }

        #endregion Spatial Awareness System

        #region Networking System

        [SerializeField]
        [Tooltip("Profile for wiring up networking assets.")]
        private MixedRealityNetworkSystemProfile networkingSystemProfile;

        /// <summary>
        /// Active profile for diagnostic configuration
        /// </summary>
        public MixedRealityNetworkSystemProfile NetworkingSystemProfile
        {
            get => networkingSystemProfile;
            internal set => networkingSystemProfile = value;
        }

        [SerializeField]
        [Tooltip("Enable networking system")]
        private bool enableNetworkingSystem = false;

        /// <summary>
        /// Is the networking system properly configured and enabled?
        /// </summary>
        public bool IsNetworkingSystemEnabled
        {
            get => enableNetworkingSystem && NetworkingSystemSystemType?.Type != null && networkingSystemProfile != null;
            internal set => enableNetworkingSystem = value;
        }

        [SerializeField]
        [Tooltip("Networking System Class to instantiate at runtime.")]
        [Implements(typeof(IMixedRealityNetworkingSystem), TypeGrouping.ByNamespaceFlat)]
        private SystemType networkingSystemType;

        /// <summary>
        /// Diagnostics System Script File to instantiate at runtime
        /// </summary>
        public SystemType NetworkingSystemSystemType
        {
            get => networkingSystemType;
            internal set => networkingSystemType = value;
        }

        #endregion Networking System

        #region Diagnostics System

        [SerializeField]
        [Tooltip("Profile for wiring up diagnostic assets.")]
        private MixedRealityDiagnosticsProfile diagnosticsSystemProfile;

        /// <summary>
        /// Active profile for diagnostic configuration
        /// </summary>
        public MixedRealityDiagnosticsProfile DiagnosticsSystemProfile
        {
            get => diagnosticsSystemProfile;
            internal set => diagnosticsSystemProfile = value;
        }

        [SerializeField]
        [Tooltip("Enable diagnostic system")]
        private bool enableDiagnosticsSystem = false;

        /// <summary>
        /// Is the diagnostics system properly configured and enabled?
        /// </summary>
        public bool IsDiagnosticsSystemEnabled
        {
            get => enableDiagnosticsSystem && DiagnosticsSystemSystemType?.Type != null && diagnosticsSystemProfile != null;
            internal set => enableDiagnosticsSystem = value;
        }

        [SerializeField]
        [Tooltip("Diagnostics System Class to instantiate at runtime.")]
        [Implements(typeof(IMixedRealityDiagnosticsSystem), TypeGrouping.ByNamespaceFlat)]
        private SystemType diagnosticsSystemType;

        /// <summary>
        /// Diagnostics System Script File to instantiate at runtime
        /// </summary>
        public SystemType DiagnosticsSystemSystemType
        {
            get => diagnosticsSystemType;
            internal set => diagnosticsSystemType = value;
        }

        #endregion Diagnostics System

        #region Native Library System

        [SerializeField]
        [Tooltip("Profile for setting up native libraries as data providers.")]
        private NativeLibrarySystemConfigurationProfile nativeLibrarySystemConfigurationProfile = null;

        /// <summary>
        /// Profile for setting up native libraries as data providers.
        /// </summary>
        public NativeLibrarySystemConfigurationProfile NativeLibrarySystemConfigurationProfile
        {
            get => nativeLibrarySystemConfigurationProfile;
            internal set => nativeLibrarySystemConfigurationProfile = value;
        }

        [SerializeField]
        [Tooltip("Enable the native library system.")]
        private bool enableNativeLibrarySystem = false;

        /// <summary>
        /// Is the native library system properly configured and enabled?
        /// </summary>
        public bool IsNativeLibrarySystemEnabled
        {
            get => enableNativeLibrarySystem && NativeLibrarySystemType?.Type != null && nativeLibrarySystemConfigurationProfile != null;
            internal set => enableNativeLibrarySystem = value;
        }

        [SerializeField]
        [Tooltip("Native Library class to instantiate at runtime.")]
        [Implements(typeof(IMixedRealityNativeLibrarySystem), TypeGrouping.ByNamespaceFlat)]
        private SystemType nativeLibrarySystemType;

        /// <summary>
        /// Native Library class to instantiate at runtime.
        /// </summary>
        public SystemType NativeLibrarySystemType
        {
            get => nativeLibrarySystemType;
            internal set => nativeLibrarySystemType = value;
        }

        #endregion Native Library System

        [SerializeField]
        [Tooltip("All the additional non-required services registered with the Mixed Reality Toolkit.")]
        private MixedRealityRegisteredServiceProvidersProfile registeredServiceProvidersProfile = null;

        /// <summary>
        /// All the additional non-required systems, features, and managers registered with the Mixed Reality Toolkit.
        /// </summary>
        public MixedRealityRegisteredServiceProvidersProfile RegisteredServiceProvidersProfile => registeredServiceProvidersProfile;
    }
}