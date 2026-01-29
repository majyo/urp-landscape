using Landscape.Sky;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Landscape.RenderPipelines
{
    public class SkyComputeShaderData
    {
        public int transmittanceKernel;
        public int multiScatteringKernel;
        public int skyViewKernel;
        public int ambientSHKernel;

        public uint threadGroupSizeX;
        public uint threadGroupSizeY;

        public ComputeShader skyComputeShader;
    }
    
    public class SkyPass : ScriptableRenderPass
    {
        // This class stores the data needed by the RenderGraph pass.
        // It is passed as a parameter to the delegate function that executes the RenderGraph pass.
        private class SkyPassData
        {
            internal RenderTextureDescriptor transmittanceLutDescriptor;
            internal RenderTextureDescriptor multiScatteringLutDescriptor;
            internal RenderTextureDescriptor skyViewLutDescriptor;

            internal TextureHandle transmittanceLut;
            internal TextureHandle multiScatteringLut;
            internal TextureHandle skyViewLut;

            internal SkySettings skySettings;
            internal SkyComputeShaderData skyComputeShaderData;

            internal bool updateTransmittance;
            internal bool updateMultiScattering;
            internal bool updateSkyView;
        }
        
        private const string SkyPassName = "Sky Pass";
        private static readonly ProfilingSampler SkyPassSampler = new(SkyPassName);

        private RenderTargetIdentifier _transmittanceLUTIdentifier;
        private RenderTargetIdentifier _multiScatteringLUTIdentifier;
        private RenderTargetIdentifier _skyViewLUTIdentifier;

        private Vector2Int _transmittanceLUTSize = new(256, 128);
        private Vector2Int _multiScatteringLUTSize = new(32, 32);
        private Vector2Int _skyViewLUTSize = new(256, 128);
        
        private SkyRenderer _skyRenderer;
        
        public SkyComputeShaderData SkyComputeShaderData { get; set; }
        public Material SkyboxMaterial { get; set; }
        public SkyLutCache LutCache { get; set; }
        public SkyAmbientProbeUpdater AmbientProbeUpdater { get; set; }

        public SkyRenderer SkyRenderer
        {
            get
            {
                if (_skyRenderer == null)
                {
                    _skyRenderer = Object.FindAnyObjectByType<SkyRenderer>();
                    
                    if (_skyRenderer == null)
                    {
                        Debug.LogError("SkyRenderer not found in the scene. You should have one in the scene before adding the SkyPass.");
                        return null;
                    }
                }

                return _skyRenderer;
            }
        }

        // This static method is passed as the RenderFunc delegate to the RenderGraph render pass.
        // It is used to execute draw commands.
        private static void ExecutePass(SkyPassData data, ComputeGraphContext context)
        {
            if (!data.updateTransmittance && !data.updateMultiScattering && !data.updateSkyView)
            {
                return;
            }

            var cmd = context.cmd;
            var skySettings = data.skySettings;
            var skyComputeShaderData = data.skyComputeShaderData;
            
            var skyComputeShader = skyComputeShaderData.skyComputeShader;
            var transmittanceKernel = skyComputeShaderData.transmittanceKernel;
            var multiScatteringKernel = skyComputeShaderData.multiScatteringKernel;
            var skyViewKernel = skyComputeShaderData.skyViewKernel;
            var threadGroupSizeX = skyComputeShaderData.threadGroupSizeX;
            var threadGroupSizeY = skyComputeShaderData.threadGroupSizeY;

            cmd.SetComputeFloatParam(skyComputeShader, "_RayleighScalarHeight",
                skySettings.rayleighScalarHeight);
            cmd.SetComputeFloatParam(skyComputeShader, "_RayleighScatteringStrength",
                skySettings.rayleighScatteringStrength);
            cmd.SetComputeFloatParam(skyComputeShader, "_MieScalarHeight", skySettings.mieScalarHeight);
            cmd.SetComputeFloatParam(skyComputeShader, "_MieAnisotropy", skySettings.mieAnisotropy);
            cmd.SetComputeFloatParam(skyComputeShader, "_MieScatteringStrength",
                skySettings.mieScatteringStrength);
            cmd.SetComputeFloatParam(skyComputeShader, "_OzoneCenter", skySettings.ozoneCenter);
            cmd.SetComputeFloatParam(skyComputeShader, "_OzoneWidth", skySettings.ozoneWidth);
            cmd.SetComputeFloatParam(skyComputeShader, "_PlanetRadius", skySettings.planetRadius);
            cmd.SetComputeFloatParam(skyComputeShader, "_AtmosphereHeight", skySettings.atmosphereHeight);
            cmd.SetComputeVectorParam(skyComputeShader, "_SunLightColor", skySettings.sunLightColor);
            cmd.SetComputeFloatParam(skyComputeShader, "_SunLightIntensity", skySettings.sunLightIntensity);
            cmd.SetComputeFloatParam(skyComputeShader, "_SunDiskAngle", skySettings.sunDiskAngle);
            cmd.SetComputeFloatParam(skyComputeShader, "_SeaLevel", skySettings.seaLevel);
            cmd.SetComputeVectorParam(skyComputeShader, "_GroundTint", skySettings.groundTint);

            if (data.updateTransmittance)
            {
                cmd.SetComputeTextureParam(skyComputeShader, transmittanceKernel, "_TransmittanceLUT",
                    data.transmittanceLut);

                DispatchComputeShader(cmd, skyComputeShader, transmittanceKernel,
                    data.transmittanceLutDescriptor.width, data.transmittanceLutDescriptor.height,
                    threadGroupSizeX, threadGroupSizeY);
            }

            if (data.updateMultiScattering)
            {
                cmd.SetComputeTextureParam(skyComputeShader, multiScatteringKernel, "_TransmittanceReadonlyLUT",
                    data.transmittanceLut);
                cmd.SetComputeTextureParam(skyComputeShader, multiScatteringKernel, "_MultiScatteringLUT",
                    data.multiScatteringLut);

                DispatchComputeShader(cmd, skyComputeShader, multiScatteringKernel,
                    data.multiScatteringLutDescriptor.width, data.multiScatteringLutDescriptor.height,
                    threadGroupSizeX, threadGroupSizeY);
            }

            if (data.updateSkyView)
            {
                cmd.SetComputeTextureParam(skyComputeShader, skyViewKernel, "_TransmittanceReadonlyLUT",
                    data.transmittanceLut);
                cmd.SetComputeTextureParam(skyComputeShader, skyViewKernel, "_MultiScatteringReadonlyLUT",
                    data.multiScatteringLut);
                cmd.SetComputeTextureParam(skyComputeShader, skyViewKernel, "_SkyViewLUT",
                    data.skyViewLut);

                DispatchComputeShader(cmd, skyComputeShader, skyViewKernel,
                    data.skyViewLutDescriptor.width, data.skyViewLutDescriptor.height,
                    threadGroupSizeX, threadGroupSizeY);
            }
        }

        // RecordRenderGraph is where the RenderGraph handle can be accessed, through which render passes can be added to the graph.
        // FrameData is a context container through which URP resources can be accessed and managed.
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (!SkyRenderer)
            {
                return;
            }

            if (LutCache == null)
            {
                return;
            }

            var cameraData = frameData.Get<UniversalCameraData>();
            if (cameraData.renderType == CameraRenderType.Overlay || cameraData.camera == null)
            {
                return;
            }

            if (cameraData.camera.cameraType == CameraType.Preview)
            {
                return;
            }

            var transmittanceDesc = new RenderTextureDescriptor(_transmittanceLUTSize.x,
                _transmittanceLUTSize.y, RenderTextureFormat.ARGBFloat, 0)
            {
                msaaSamples = 1,
                enableRandomWrite = true
            };

            var multiScatteringDesc = new RenderTextureDescriptor(_multiScatteringLUTSize.x,
                _multiScatteringLUTSize.y, RenderTextureFormat.ARGBFloat, 0)
            {
                msaaSamples = 1,
                enableRandomWrite = true
            };

            var skyViewDesc = new RenderTextureDescriptor(_skyViewLUTSize.x,
                _skyViewLUTSize.y, RenderTextureFormat.ARGBFloat, 0)
            {
                msaaSamples = 1,
                enableRandomWrite = true
            };

            var skySettings = SkyRenderer.SkySettings;
            if (skySettings == null || SkyComputeShaderData == null || SkyComputeShaderData.skyComputeShader == null)
            {
                return;
            }

            var sharedKeyBase = SkyLutCache.ComputeSharedKey(skySettings);
            if (!LutCache.GetOrCreateShared(
                    sharedKeyBase,
                    transmittanceDesc,
                    multiScatteringDesc,
                    out var transmittanceRt,
                    out var multiScatteringRt,
                    out var sharedKey,
                    out var updateShared))
            {
                return;
            }

            var cameraId = cameraData.camera.GetInstanceID();
            var sunDirection = SkyLutCache.GetSunDirection(SkyRenderer);
            var cameraHeight = cameraData.camera.transform.position.y;
            var skyViewKey = SkyLutCache.ComputeSkyViewKey(sharedKey, cameraHeight, sunDirection);

            if (!LutCache.GetOrCreateSkyView(
                    cameraId,
                    skyViewKey,
                    skyViewDesc,
                    Time.frameCount,
                    out var skyViewRt,
                    out var updateSkyView))
            {
                return;
            }

            var transmittanceTexture = renderGraph.ImportTexture(transmittanceRt);
            var multiScatteringTexture = renderGraph.ImportTexture(multiScatteringRt);
            var skyViewTexture = renderGraph.ImportTexture(skyViewRt);

            var updateTransmittance = updateShared;
            var updateMultiScattering = updateShared;

            // Add a raster render pass to the render graph. The PassData type parameter determines
            // the type of the passData output variable.
            using (var builder = renderGraph.AddComputePass<SkyPassData>(SkyPassName,
                       out var passData, SkyPassSampler))
            {
                passData.transmittanceLutDescriptor = transmittanceDesc;
                passData.multiScatteringLutDescriptor = multiScatteringDesc;
                passData.skyViewLutDescriptor = skyViewDesc;

                passData.transmittanceLut = transmittanceTexture;
                passData.multiScatteringLut = multiScatteringTexture;
                passData.skyViewLut = skyViewTexture;

                passData.skyComputeShaderData = SkyComputeShaderData;
                passData.skySettings = skySettings;

                passData.updateTransmittance = updateTransmittance;
                passData.updateMultiScattering = updateMultiScattering;
                passData.updateSkyView = updateSkyView;

                var transAccess = updateTransmittance ? AccessFlags.ReadWrite : AccessFlags.Read;
                var multiAccess = updateMultiScattering
                    ? (updateSkyView ? AccessFlags.ReadWrite : AccessFlags.Write)
                    : AccessFlags.Read;
                var skyAccess = updateSkyView ? AccessFlags.Write : AccessFlags.Read;

                builder.UseTexture(passData.transmittanceLut, transAccess);
                builder.UseTexture(passData.multiScatteringLut, multiAccess);
                builder.UseTexture(passData.skyViewLut, skyAccess);

                builder.AllowPassCulling(false);

                builder.SetGlobalTextureAfterPass(passData.transmittanceLut, SkyShaderPropertyIds.TransmittanceLut);
                builder.SetGlobalTextureAfterPass(passData.multiScatteringLut, SkyShaderPropertyIds.MultiScatteringLut);
                builder.SetGlobalTextureAfterPass(passData.skyViewLut, SkyShaderPropertyIds.SkyViewLut);

                builder.SetRenderFunc((SkyPassData data, ComputeGraphContext context) => ExecutePass(data, context));
            }

            UpdateSkyboxMaterial();

            // Request ambient SH computation after sky view is updated
            if (AmbientProbeUpdater != null && SkyRenderer.UpdateAmbientProbe)
            {
                if (LutCache.TryGetMainCameraSkyView(out var mainSkyView, out var mainSkyViewKey))
                {
                    AmbientProbeUpdater.RequestCompute(mainSkyView, mainSkyViewKey);
                }
            }
        }

        private void UpdateSkyboxMaterial()
        {
            if (!SkyboxMaterial)
            {
                return;
            }

            var skySettings = SkyRenderer.SkySettings;

            SkyboxMaterial.SetFloat("_RayleighScalarHeight", skySettings.rayleighScalarHeight);
            SkyboxMaterial.SetFloat("_RayleighScatteringStrength", skySettings.rayleighScatteringStrength);
            SkyboxMaterial.SetFloat("_MieScalarHeight", skySettings.mieScalarHeight);
            SkyboxMaterial.SetFloat("_MieAnisotropy", skySettings.mieAnisotropy);
            SkyboxMaterial.SetFloat("_MieScatteringStrength", skySettings.mieScatteringStrength);
            SkyboxMaterial.SetFloat("_OzoneCenter", skySettings.ozoneCenter);
            SkyboxMaterial.SetFloat("_OzoneWidth", skySettings.ozoneWidth);
            SkyboxMaterial.SetFloat("_PlanetRadius", skySettings.planetRadius);
            SkyboxMaterial.SetFloat("_AtmosphereHeight", skySettings.atmosphereHeight);
            SkyboxMaterial.SetVector("_SunLightColor", skySettings.sunLightColor);
            SkyboxMaterial.SetFloat("_SunLightIntensity", skySettings.sunLightIntensity);
            SkyboxMaterial.SetFloat("_SunDiskAngle", skySettings.sunDiskAngle);
            SkyboxMaterial.SetFloat("_SeaLevel", skySettings.seaLevel);
            
            SkyboxMaterial.SetMatrix("_InverseSunRotationMatrix", SkyRenderer.GetSkyInverseRotation());

            // SkyboxMaterial.SetTexture("_TransmittanceLUT", _globalData.transmittanceLUTGlobal);
            // SkyboxMaterial.SetTexture("_MultiScatteringLUT", _globalData.multiScatteringLUTGlobal);
            // SkyboxMaterial.SetTexture("_SkyViewLUT", _globalData.skyViewLUTGlobal);
        }

        // Cleanup any allocated resources that were created during the execution of this render pass.
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
        }

        private static void DispatchComputeShader(ComputeCommandBuffer cmd, ComputeShader computeShader, int kernel, int width, int height,
            uint threadGroupSizeX, uint threadGroupSizeY)
        {
            var numberX = Mathf.CeilToInt(width / (float)threadGroupSizeX);
            var numberY = Mathf.CeilToInt(height / (float)threadGroupSizeY);

            cmd.DispatchCompute(computeShader, kernel, numberX, numberY, 1);
        }
    }
}
