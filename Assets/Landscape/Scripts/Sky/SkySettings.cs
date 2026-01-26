using UnityEngine;

namespace Landscape.Sky
{
    [CreateAssetMenu(menuName = "Landscape/Sky/Atmosphere Settings")]
    public class SkySettings : ScriptableObject
    {
        [SerializeField] public float rayleighScalarHeight = 8000.0f;
        [SerializeField] public float rayleighScatteringStrength = 1.0f;
        [SerializeField] public float mieScalarHeight = 1200.0f;
        [SerializeField] public float mieAnisotropy = 0.8f;
        [SerializeField] public float mieScatteringStrength = 1.0f;
        [SerializeField] public float ozoneCenter = 25000.0f;
        [SerializeField] public float ozoneWidth = 15000.0f;
        [SerializeField] public float planetRadius = 6360000.0f;
        [SerializeField] public float atmosphereHeight = 60000.0f;
        [SerializeField] public float seaLevel = 0.0f;

        [SerializeField] public Color sunLightColor = Color.white;
        [SerializeField] public float sunLightIntensity = 31.4f;
        [SerializeField] public float sunDiskAngle = 2.5f;
        
        [SerializeField] public Color groundTint = Color.gray;
    }
}