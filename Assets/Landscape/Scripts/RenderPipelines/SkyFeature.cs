using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Landscape.RenderPipelines
{
    public class SkyFeature : ScriptableRendererFeature
    {
        [SerializeField] private ComputeShader skyComputeShader;
        [SerializeField] private Material skyboxMaterial;

        private static int _transmittanceKernel;
        private static int _multiScatteringKernel;
        private static int _skyViewKernel;

        private static uint _threadGroupSizeX;
        private static uint _threadGroupSizeY;

        private SkyPass _atmospherePass;
        private SkyLutKeepAlivePass _lutKeepAlivePass;
        private SkyLutCache _lutCache;

        /// <inheritdoc/>
        public override void Create()
        {
            _lutCache?.Dispose();
            _lutCache = new SkyLutCache();

            _transmittanceKernel = skyComputeShader.FindKernel("ComputeTransmittance");
            _multiScatteringKernel = skyComputeShader.FindKernel("ComputeMultiScattering");
            _skyViewKernel = skyComputeShader.FindKernel("ComputeSkyView");

            skyComputeShader.GetKernelThreadGroupSizes(_transmittanceKernel, out _threadGroupSizeX,
                out _threadGroupSizeY, out _);

            _atmospherePass = new SkyPass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingSkybox,
                SkyboxMaterial = skyboxMaterial,
                LutCache = _lutCache,
                SkyComputeShaderData = new SkyComputeShaderData
                {
                    skyComputeShader = skyComputeShader,
                    transmittanceKernel = _transmittanceKernel,
                    multiScatteringKernel = _multiScatteringKernel,
                    skyViewKernel = _skyViewKernel,
                    threadGroupSizeX = _threadGroupSizeX,
                    threadGroupSizeY = _threadGroupSizeY
                }
            };

            _lutKeepAlivePass = new SkyLutKeepAlivePass
            {
                renderPassEvent = RenderPassEvent.AfterRenderingSkybox
            };
        }

        // Here you can inject one or multiple render passes in the renderer.
        // This method is called when setting up the renderer once per-camera.
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(_atmospherePass);
            renderer.EnqueuePass(_lutKeepAlivePass);
        }

        protected override void Dispose(bool disposing)
        {
            _lutCache?.Dispose();
            _lutCache = null;
            base.Dispose(disposing);
        }
    }
}
