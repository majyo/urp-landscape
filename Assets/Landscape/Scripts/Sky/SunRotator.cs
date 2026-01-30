using UnityEngine;

namespace Landscape.Sky
{
    public class SunRotator : MonoBehaviour
    {
        [SerializeField] private Light sun;
        [SerializeField] private Vector3 rotationAxis = Vector3.right;
        [SerializeField] private float degreesPerSecond = 1.0f;
        [SerializeField] private bool useLocalAxis = false;

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            var target = sun != null ? sun.transform : RenderSettings.sun != null ? RenderSettings.sun.transform : null;
            if (target == null)
            {
                return;
            }

            if (rotationAxis.sqrMagnitude < 0.000001f || Mathf.Approximately(degreesPerSecond, 0f))
            {
                return;
            }

            var axis = useLocalAxis ? target.TransformDirection(rotationAxis.normalized) : rotationAxis.normalized;
            target.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.World);
        }
    }
}
