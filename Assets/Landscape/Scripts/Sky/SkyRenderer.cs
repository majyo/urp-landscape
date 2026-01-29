using Landscape.RenderPipelines;
using UnityEngine;
using UnityEngine.Rendering;

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
        [SerializeField] private bool updateReflectionProbe = true;
        [SerializeField] private ReflectionProbe reflectionProbe;
        [Header("Editor Debug")]
        [SerializeField] private bool updateInEditor = false;
        [SerializeField] private float editorUpdateInterval = 1.0f;

        private float _timeSinceLastGIUpdate;

        // Reference to the ambient probe updater (set by SkyFeature)
        private static SkyAmbientProbeUpdater _ambientProbeUpdater;

        public bool UpdateAmbientProbe => updateAmbientProbe;

        /// <summary>
        /// Set the ambient probe updater reference. Called by SkyFeature during initialization.
        /// </summary>
        public static void SetAmbientProbeUpdater(SkyAmbientProbeUpdater updater)
        {
            _ambientProbeUpdater = updater;
        }

        private void OnEnable()
        {
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        }

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            // Only process for game/scene cameras
            if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView)
            {
                return;
            }

            // Try to update ambient probe from GPU readback
            _ambientProbeUpdater?.TryUpdateAmbientProbe();
        }

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
                // Note: DynamicGI.UpdateEnvironment() no longer works in Unity 6
                // Ambient probe is now updated via SkyAmbientProbeUpdater in the render pipeline
                UpdateReflectionProbe();
                _timeSinceLastGIUpdate = 0f;
            }
        }

        private void UpdateReflectionProbe()
        {
            if (!updateReflectionProbe || reflectionProbe == null)
            {
                return;
            }

            reflectionProbe.RenderProbe();
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
