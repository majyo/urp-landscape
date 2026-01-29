using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Landscape.RenderPipelines
{
    /// <summary>
    /// Manages GPU-based spherical harmonics computation for sky ambient lighting.
    /// Computes L2 SH coefficients from the SkyView LUT and updates RenderSettings.ambientProbe.
    /// </summary>
    public class SkyAmbientProbeUpdater : IDisposable
    {
        private const int SH_COEFFICIENT_COUNT = 9;
        private const int SH_SAMPLE_COUNT = 64;

        private ComputeBuffer _shCoefficientsBuffer;
        private AsyncGPUReadbackRequest _readbackRequest;
        private bool _readbackPending;
        private bool _disposed;

        private int _ambientSHKernel = -1;
        private ComputeShader _skyComputeShader;

        // Cached SH coefficients (9 float4, using xyz for RGB)
        private Vector4[] _shCoefficients;

        // Track last computed sky view key to avoid redundant updates
        private int _lastComputedSkyViewKey;

        public bool IsInitialized => _shCoefficientsBuffer != null && _ambientSHKernel >= 0;
        public bool IsReadbackPending => _readbackPending;

        public SkyAmbientProbeUpdater()
        {
            _shCoefficients = new Vector4[SH_COEFFICIENT_COUNT];
        }

        /// <summary>
        /// Initialize the updater with the sky compute shader.
        /// </summary>
        public void Initialize(ComputeShader skyComputeShader)
        {
            if (skyComputeShader == null)
            {
                Debug.LogError("SkyAmbientProbeUpdater: skyComputeShader is null");
                return;
            }

            _skyComputeShader = skyComputeShader;
            _ambientSHKernel = skyComputeShader.FindKernel("ComputeAmbientSH");

            if (_ambientSHKernel < 0)
            {
                Debug.LogError("SkyAmbientProbeUpdater: ComputeAmbientSH kernel not found");
                return;
            }

            // Create buffer for 9 SH coefficients (float4 each, using xyz for RGB)
            _shCoefficientsBuffer = new ComputeBuffer(SH_COEFFICIENT_COUNT, sizeof(float) * 4);
        }

        /// <summary>
        /// Request SH computation from the SkyView LUT.
        /// This should be called after the SkyView LUT has been updated.
        /// </summary>
        /// <param name="skyViewLut">The SkyView LUT RTHandle</param>
        /// <param name="skyViewKey">The current sky view key for change detection</param>
        /// <param name="forceUpdate">Force update even if sky view key hasn't changed</param>
        public void RequestCompute(RTHandle skyViewLut, int skyViewKey, bool forceUpdate = false)
        {
            if (!IsInitialized)
            {
                return;
            }

            // Skip if already pending or sky hasn't changed
            if (_readbackPending)
            {
                return;
            }

            if (!forceUpdate && skyViewKey == _lastComputedSkyViewKey)
            {
                return;
            }

            if (skyViewLut == null || skyViewLut.rt == null)
            {
                return;
            }

            // Dispatch compute shader
            _skyComputeShader.SetTexture(_ambientSHKernel, "_SkyViewReadonlyLUT", skyViewLut.rt);
            _skyComputeShader.SetBuffer(_ambientSHKernel, "_AmbientSHCoefficients", _shCoefficientsBuffer);
            _skyComputeShader.Dispatch(_ambientSHKernel, 1, 1, 1);

            // Request async readback
            _readbackRequest = AsyncGPUReadback.Request(_shCoefficientsBuffer);
            _readbackPending = true;
            _lastComputedSkyViewKey = skyViewKey;
        }

        /// <summary>
        /// Check if readback is complete and update the ambient probe if so.
        /// Call this every frame from Update or LateUpdate.
        /// </summary>
        /// <returns>True if the ambient probe was updated this frame.</returns>
        public bool TryUpdateAmbientProbe()
        {
            if (!_readbackPending)
            {
                return false;
            }

            if (!_readbackRequest.done)
            {
                return false;
            }

            _readbackPending = false;

            if (_readbackRequest.hasError)
            {
                Debug.LogWarning("SkyAmbientProbeUpdater: GPU readback failed");
                return false;
            }

            // Get the data from readback
            var data = _readbackRequest.GetData<Vector4>();
            if (data.Length != SH_COEFFICIENT_COUNT)
            {
                Debug.LogWarning($"SkyAmbientProbeUpdater: Unexpected data length {data.Length}, expected {SH_COEFFICIENT_COUNT}");
                return false;
            }

            // Copy to cached array
            data.CopyTo(_shCoefficients);

            // Update RenderSettings.ambientProbe
            ApplyToAmbientProbe();

            return true;
        }

        /// <summary>
        /// Apply the computed SH coefficients to RenderSettings.ambientProbe.
        /// </summary>
        private void ApplyToAmbientProbe()
        {
            var sh = new SphericalHarmonicsL2();

            // SphericalHarmonicsL2 layout:
            // sh[channel, coefficient] where channel: 0=R, 1=G, 2=B
            // coefficient index 0-8 corresponds to L0, L1(-1,0,1), L2(-2,-1,0,1,2)
            for (int i = 0; i < SH_COEFFICIENT_COUNT; i++)
            {
                sh[0, i] = _shCoefficients[i].x; // R
                sh[1, i] = _shCoefficients[i].y; // G
                sh[2, i] = _shCoefficients[i].z; // B
            }

            RenderSettings.ambientMode = AmbientMode.Custom;
            RenderSettings.ambientProbe = sh;
        }

        /// <summary>
        /// Get the current SH coefficients (for debugging).
        /// </summary>
        public Vector4[] GetSHCoefficients()
        {
            return _shCoefficients;
        }

        /// <summary>
        /// Reset the last computed key to force a recompute on next request.
        /// </summary>
        public void InvalidateCache()
        {
            _lastComputedSkyViewKey = 0;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _shCoefficientsBuffer?.Release();
            _shCoefficientsBuffer = null;
            _disposed = true;
        }
    }
}
