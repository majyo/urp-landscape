using System;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace Landscape.Debugging
{
    [ExecuteAlways]
    public class AmbientLightingDebugger : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private bool showOverlay = true;
        [SerializeField] private bool showInEditor = true;
        [SerializeField] private int overlayWidth = 420;
        [SerializeField] private int overlayPadding = 12;
        [SerializeField] private int fontSize = 12;

        [Header("Hotkeys")]
        [SerializeField] private KeyCode toggleOverlayKey = KeyCode.F8;
        [SerializeField] private KeyCode refreshKey = KeyCode.F9;
        [SerializeField] private KeyCode dumpProbeKey = KeyCode.F10;

        [Header("Probe")]
        [SerializeField] private bool showProbeCoefficients = false;

        private GUIStyle _labelStyle;
        private GUIStyle _headerStyle;
        private int _cachedFontSize;

        private void Update()
        {
            if (!Application.isPlaying && !showInEditor)
            {
                return;
            }

            if (toggleOverlayKey != KeyCode.None && Input.GetKeyDown(toggleOverlayKey))
            {
                showOverlay = !showOverlay;
            }

            if (refreshKey != KeyCode.None && Input.GetKeyDown(refreshKey))
            {
                RefreshEnvironment();
            }

            if (dumpProbeKey != KeyCode.None && Input.GetKeyDown(dumpProbeKey))
            {
                DumpAmbientProbe();
            }
        }

        private void OnGUI()
        {
            if (!showOverlay)
            {
                return;
            }

            if (!Application.isPlaying && !showInEditor)
            {
                return;
            }

            EnsureStyles();

            var rect = new Rect(overlayPadding, overlayPadding, overlayWidth, Screen.height - overlayPadding * 2);
            GUILayout.BeginArea(rect, GUI.skin.box);

            GUILayout.Label("Ambient Lighting Debug", _headerStyle);

            DrawAmbientSection();
            DrawReflectionSection();
            DrawSkyboxSection();
            DrawProbeSection();

            GUILayout.Space(6);
            GUILayout.Label(
                $"Hotkeys: Toggle({toggleOverlayKey}) Refresh({refreshKey}) Dump({dumpProbeKey})",
                _labelStyle);

            GUILayout.EndArea();
        }

        [ContextMenu("Refresh Environment Lighting")]
        public void RefreshEnvironment()
        {
            DynamicGI.UpdateEnvironment();
        }

        [ContextMenu("Dump Ambient Probe")]
        public void DumpAmbientProbe()
        {
            var probe = RenderSettings.ambientProbe;
            var sb = new StringBuilder(256);
            sb.AppendLine("Ambient Probe (SphericalHarmonicsL2)");
            for (var rgb = 0; rgb < 3; rgb++)
            {
                sb.Append(rgb == 0 ? "R: " : rgb == 1 ? "G: " : "B: ");
                for (var i = 0; i < 9; i++)
                {
                    sb.Append(probe[rgb, i].ToString("F6"));
                    if (i < 8)
                    {
                        sb.Append(", ");
                    }
                }

                sb.AppendLine();
            }

            Debug.Log(sb.ToString(), this);
        }

        private void EnsureStyles()
        {
            if (_labelStyle != null && _cachedFontSize == fontSize)
            {
                return;
            }

            _cachedFontSize = fontSize;
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                wordWrap = true
            };
            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize + 2,
                fontStyle = FontStyle.Bold
            };
        }

        private void DrawAmbientSection()
        {
            GUILayout.Label("Ambient", _headerStyle);
            GUILayout.Label($"Mode: {RenderSettings.ambientMode}", _labelStyle);
            GUILayout.Label($"Intensity: {RenderSettings.ambientIntensity:F3}", _labelStyle);

            if (RenderSettings.ambientMode == AmbientMode.Flat)
            {
                GUILayout.Label($"Flat Color: {FormatColor(RenderSettings.ambientLight)}", _labelStyle);
                return;
            }

            GUILayout.Label($"Sky Color: {FormatColor(RenderSettings.ambientSkyColor)}", _labelStyle);
            GUILayout.Label($"Equator Color: {FormatColor(RenderSettings.ambientEquatorColor)}", _labelStyle);
            GUILayout.Label($"Ground Color: {FormatColor(RenderSettings.ambientGroundColor)}", _labelStyle);
        }

        private void DrawReflectionSection()
        {
            GUILayout.Space(6);
            GUILayout.Label("Reflection", _headerStyle);
            GUILayout.Label($"Mode: {RenderSettings.defaultReflectionMode}", _labelStyle);
            GUILayout.Label($"Intensity: {RenderSettings.reflectionIntensity:F3}", _labelStyle);
            GUILayout.Label($"Bounces: {RenderSettings.reflectionBounces}", _labelStyle);
            GUILayout.Label($"Resolution: {RenderSettings.defaultReflectionResolution}", _labelStyle);
            GUILayout.Label($"Custom Cubemap: {GetCustomReflectionName()}", _labelStyle);
        }

        private void DrawSkyboxSection()
        {
            GUILayout.Space(6);
            GUILayout.Label("Skybox", _headerStyle);
            var skybox = RenderSettings.skybox;
            GUILayout.Label($"Material: {(skybox != null ? skybox.name : "None")}", _labelStyle);
            var sun = RenderSettings.sun;
            GUILayout.Label($"Sun Light: {(sun != null ? sun.name : "None")}", _labelStyle);
        }

        private void DrawProbeSection()
        {
            GUILayout.Space(6);
            GUILayout.Label("Ambient Probe", _headerStyle);
            var probe = RenderSettings.ambientProbe;
            var l0 = new Color(probe[0, 0], probe[1, 0], probe[2, 0], 1f);
            GUILayout.Label($"L0: {FormatColor(l0)}", _labelStyle);

            if (!showProbeCoefficients)
            {
                return;
            }

            for (var rgb = 0; rgb < 3; rgb++)
            {
                var line = rgb == 0 ? "R: " : rgb == 1 ? "G: " : "B: ";
                for (var i = 0; i < 9; i++)
                {
                    line += probe[rgb, i].ToString("F3");
                    if (i < 8)
                    {
                        line += ", ";
                    }
                }

                GUILayout.Label(line, _labelStyle);
            }
        }

        private static string GetCustomReflectionName()
        {
            try
            {
                var custom = RenderSettings.customReflection;
                return custom != null ? custom.name : "None";
            }
            catch (ArgumentException)
            {
                return "None";
            }
        }

        private static string FormatColor(Color color)
        {
            return $"({color.r:F3}, {color.g:F3}, {color.b:F3}, {color.a:F3})";
        }
    }
}
