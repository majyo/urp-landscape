using UnityEngine;

namespace Landscape.Sky
{
    [ExecuteAlways]
    public class SkyRenderer : MonoBehaviour
    {
        [SerializeField] private SkySettings skySettings;
        [SerializeField] private Transform sunTransform;
        [SerializeField] private Quaternion skyOffsetRotation;
        
        public SkySettings SkySettings => skySettings;
        public Transform SunTransform => sunTransform;
        public Quaternion SkyOffsetRotation => skyOffsetRotation;

        [Header("Global Illumination")]
        [SerializeField] private bool updateAmbientProbe = true;
        [SerializeField] private float ambientUpdateInterval = 0.2f;
        [Header("Editor Debug")]
        [SerializeField] private bool updateInEditor = false;
        [SerializeField] private float editorUpdateInterval = 1.0f;

        private float _timeSinceLastGIUpdate;

        private void Update()
        {
            if (Application.isPlaying)
            {
                if (updateAmbientProbe)
                {
                    HandleAmbientLightUpdate(ambientUpdateInterval);
                }
            }
            else
            {
                if (updateInEditor && updateAmbientProbe)
                {
                    HandleAmbientLightUpdate(editorUpdateInterval);
                }
            }
        }

        private void HandleAmbientLightUpdate(float interval)
        {
            _timeSinceLastGIUpdate += Time.deltaTime;

            if (_timeSinceLastGIUpdate >= interval)
            {
                UnityEngine.DynamicGI.UpdateEnvironment();
                _timeSinceLastGIUpdate = 0f;
            }
        }

        public Matrix4x4 GetSunRotation(bool inverse = false)
        {
            if (sunTransform == null)
            {
                return Matrix4x4.identity;
            }

            var sunRotation = sunTransform.rotation;
            
            if (inverse)
            {
                sunRotation = Quaternion.Inverse(sunRotation);
            }

            return Matrix4x4.Rotate(sunRotation);
        }

        public Matrix4x4 GetSkyInverseRotation()
        {
            if (sunTransform == null)
            {
                return Matrix4x4.Rotate(Quaternion.Inverse(skyOffsetRotation));
            }
            
            var sunRotation = sunTransform.rotation;
            var skyRotation = skyOffsetRotation * sunRotation;
            
            return Matrix4x4.Rotate(Quaternion.Inverse(skyRotation));
        }
    }
}