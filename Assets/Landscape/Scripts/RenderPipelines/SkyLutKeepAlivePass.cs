using Landscape.Sky;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Landscape.RenderPipelines
{
    public class SkyLutKeepAlivePass : ScriptableRenderPass
    {
        private const string PassName = "Sky LUT Keep Alive";
        private static readonly ProfilingSampler PassSampler = new(PassName);

        private class PassData
        {
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            if (cameraData.renderType == CameraRenderType.Overlay || cameraData.camera == null)
            {
                return;
            }

            if (cameraData.camera.cameraType == CameraType.Preview)
            {
                return;
            }

            if (!Object.FindAnyObjectByType<SkyRenderer>())
            {
                return;
            }

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(PassName, out _, PassSampler))
            {
                builder.UseAllGlobalTextures(true);

                builder.AllowPassCulling(false);
                builder.SetRenderFunc((PassData data, RasterGraphContext context) => { });
            }
        }
    }
}
