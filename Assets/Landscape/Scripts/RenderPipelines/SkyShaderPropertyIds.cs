using UnityEngine;

namespace Landscape.RenderPipelines
{
    internal static class SkyShaderPropertyIds
    {
        internal static readonly int TransmittanceLut = Shader.PropertyToID("_TransmittanceLUT");
        internal static readonly int MultiScatteringLut = Shader.PropertyToID("_MultiScatteringLUT");
        internal static readonly int SkyViewLut = Shader.PropertyToID("_SkyViewLUT");
    }
}

