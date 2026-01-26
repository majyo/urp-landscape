using System;
using System.Collections.Generic;
using Landscape.Sky;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Landscape.RenderPipelines
{
    public sealed class SkyLutCache : IDisposable
    {
        private const int CameraCacheKeepFrames = 300;
        private const int CameraCacheCleanupIntervalFrames = 60;

        private const float CameraHeightQuantize = 0.01f;
        private const float DirectionQuantize = 0.0001f;

        private struct SharedEntry
        {
            public RTHandle transmittance;
            public RTHandle multiScattering;
            public int key;
            public int descKey;
            public bool hasValidData;
        }

        private struct CameraEntry
        {
            public RTHandle skyView;
            public int key;
            public int descKey;
            public bool hasValidData;
            public int lastUsedFrame;
        }

        private SharedEntry _shared;
        private readonly Dictionary<int, CameraEntry> _perCamera = new();
        private int _lastCleanupFrame;

        public void Dispose()
        {
            ReleaseAll();
        }

        internal void ReleaseAll()
        {
            _shared.transmittance?.Release();
            _shared.multiScattering?.Release();
            _shared = default;

            foreach (var entry in _perCamera.Values)
            {
                entry.skyView?.Release();
            }

            _perCamera.Clear();
            _lastCleanupFrame = 0;
        }

        internal static int ComputeSharedKey(SkySettings settings)
        {
            if (settings == null)
            {
                return 0;
            }

            unchecked
            {
                var hash = 17;
                hash = (hash * 23) + settings.rayleighScalarHeight.GetHashCode();
                hash = (hash * 23) + settings.rayleighScatteringStrength.GetHashCode();
                hash = (hash * 23) + settings.mieScalarHeight.GetHashCode();
                hash = (hash * 23) + settings.mieAnisotropy.GetHashCode();
                hash = (hash * 23) + settings.mieScatteringStrength.GetHashCode();
                hash = (hash * 23) + settings.ozoneCenter.GetHashCode();
                hash = (hash * 23) + settings.ozoneWidth.GetHashCode();
                hash = (hash * 23) + settings.planetRadius.GetHashCode();
                hash = (hash * 23) + settings.atmosphereHeight.GetHashCode();
                hash = (hash * 23) + settings.seaLevel.GetHashCode();
                hash = (hash * 23) + settings.sunLightColor.GetHashCode();
                hash = (hash * 23) + settings.sunLightIntensity.GetHashCode();
                hash = (hash * 23) + settings.sunDiskAngle.GetHashCode();
                hash = (hash * 23) + settings.groundTint.GetHashCode();
                return hash;
            }
        }

        internal static int ComputeSkyViewKey(int sharedKey, float cameraWorldY, Vector3 sunDirection)
        {
            unchecked
            {
                var hash = (sharedKey * 23) + Quantize(cameraWorldY, CameraHeightQuantize);

                var dir = sunDirection.sqrMagnitude > 0.0f ? sunDirection.normalized : Vector3.down;
                hash = (hash * 23) + Quantize(dir.x, DirectionQuantize);
                hash = (hash * 23) + Quantize(dir.y, DirectionQuantize);
                hash = (hash * 23) + Quantize(dir.z, DirectionQuantize);
                return hash;
            }
        }

        internal static Vector3 GetSunDirection(SkyRenderer skyRenderer)
        {
            if (skyRenderer != null && skyRenderer.SunTransform != null)
            {
                return skyRenderer.SunTransform.forward;
            }

            if (RenderSettings.sun != null)
            {
                return RenderSettings.sun.transform.forward;
            }

            return Vector3.down;
        }

        internal bool GetOrCreateShared(
            int sharedKeyBase,
            RenderTextureDescriptor transmittanceDesc,
            RenderTextureDescriptor multiScatteringDesc,
            out RTHandle transmittance,
            out RTHandle multiScattering,
            out int sharedKey,
            out bool needsUpdate)
        {
            var transDescKey = ComputeDescriptorKey(transmittanceDesc);
            var multiDescKey = ComputeDescriptorKey(multiScatteringDesc);

            sharedKey = Combine(sharedKeyBase, transDescKey, multiDescKey);
            var descKey = Combine(transDescKey, multiDescKey);

            RenderingUtils.ReAllocateHandleIfNeeded(
                ref _shared.transmittance,
                transmittanceDesc,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: "_TransmittanceLUT");

            RenderingUtils.ReAllocateHandleIfNeeded(
                ref _shared.multiScattering,
                multiScatteringDesc,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: "_MultiScatteringLUT");

            if (_shared.descKey != descKey)
            {
                _shared.hasValidData = false;
                _shared.descKey = descKey;
            }

            needsUpdate = !_shared.hasValidData || _shared.key != sharedKey;
            if (needsUpdate)
            {
                _shared.hasValidData = true;
                _shared.key = sharedKey;
            }

            transmittance = _shared.transmittance;
            multiScattering = _shared.multiScattering;
            return transmittance != null && multiScattering != null;
        }

        internal bool GetOrCreateSkyView(
            int cameraId,
            int skyViewKey,
            RenderTextureDescriptor skyViewDesc,
            int currentFrame,
            out RTHandle skyView,
            out bool needsUpdate)
        {
            if (!_perCamera.TryGetValue(cameraId, out var entry))
            {
                entry = default;
            }

            var descKey = ComputeDescriptorKey(skyViewDesc);
            RenderingUtils.ReAllocateHandleIfNeeded(
                ref entry.skyView,
                skyViewDesc,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: "_SkyViewLUT_" + cameraId);

            if (entry.descKey != descKey)
            {
                entry.hasValidData = false;
                entry.descKey = descKey;
            }

            needsUpdate = !entry.hasValidData || entry.key != skyViewKey;
            if (needsUpdate)
            {
                entry.hasValidData = true;
                entry.key = skyViewKey;
            }

            entry.lastUsedFrame = currentFrame;
            _perCamera[cameraId] = entry;

            skyView = entry.skyView;
            MaybeCleanup(currentFrame);

            return skyView != null;
        }

        private void MaybeCleanup(int currentFrame)
        {
            if (currentFrame - _lastCleanupFrame < CameraCacheCleanupIntervalFrames)
            {
                return;
            }

            _lastCleanupFrame = currentFrame;
            CleanupStaleCameras(currentFrame);
        }

        private void CleanupStaleCameras(int currentFrame)
        {
            List<int> staleKeys = null;
            foreach (var pair in _perCamera)
            {
                if (currentFrame - pair.Value.lastUsedFrame <= CameraCacheKeepFrames)
                {
                    continue;
                }

                staleKeys ??= new List<int>();
                staleKeys.Add(pair.Key);
            }

            if (staleKeys == null)
            {
                return;
            }

            foreach (var key in staleKeys)
            {
                if (_perCamera.TryGetValue(key, out var entry))
                {
                    entry.skyView?.Release();
                }

                _perCamera.Remove(key);
            }
        }

        private static int Quantize(float value, float step)
        {
            if (step <= 0.0f)
            {
                return value.GetHashCode();
            }

            return Mathf.RoundToInt(value / step);
        }

        private static int ComputeDescriptorKey(RenderTextureDescriptor desc)
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 23) + desc.width;
                hash = (hash * 23) + desc.height;
                hash = (hash * 23) + (int)desc.graphicsFormat;
                hash = (hash * 23) + (desc.enableRandomWrite ? 1 : 0);
                return hash;
            }
        }

        private static int Combine(int a, int b)
        {
            unchecked
            {
                return (a * 397) ^ b;
            }
        }

        private static int Combine(int a, int b, int c)
        {
            unchecked
            {
                var hash = (a * 397) ^ b;
                hash = (hash * 397) ^ c;
                return hash;
            }
        }
    }
}
