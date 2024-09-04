using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.FRP;
using UnityEngine.Rendering.Universal;

namespace UnityEditor.Rendering.FRP
{
    [CustomEditor(typeof(FRPCamera))]
    public class FRPCameraEditor : Editor
    {
        FRPCamera script;
        UniversalAdditionalCameraData m_additionalCameraData;
        private SerializedProperty frpPostProcessingEnable;
        private SerializedProperty m_overrideURPPostProcessing;
        private SerializedProperty m_resetProjectMatrixAfterTAA;
        private SerializedProperty antialiasing;
        private SerializedProperty m_TaaSettings;
        private SerializedProperty taaQuality;
        private SerializedProperty taaFrameInfluence;
        private SerializedProperty taaJitterScale;
        private SerializedProperty taaMipBias;
        private SerializedProperty taaVarianceClampScale;
        private SerializedProperty taaContrastAdaptiveSharpening;

        private SerializedProperty m_SMAAQuality;
        private SerializedProperty normalQuality;
        private SerializedProperty debugMode;
        private SerializedProperty renderingPath;


        // fsr 3.0
        private SerializedProperty fsr3Setting;
        private SerializedProperty fsr3Quality;
        private SerializedProperty performSharpenPass;
        private SerializedProperty sharpness;
        private SerializedProperty fsrMipmapBias;
        private SerializedProperty enableFP16;
        private SerializedProperty reactiveMask;
        private SerializedProperty transparencyAndCompositionMask;
        private SerializedProperty autoGenerateReactiveMask;
        private SerializedProperty generateReactiveParameters;
        private SerializedProperty GenerateReactiveParams;
        private SerializedProperty autoGenerateTransparencyAndComposition;
        private SerializedProperty generateTransparencyAndCompositionParameters;


        public static readonly GUIContent taaBaseBlendFactorStyle = EditorGUIUtility.TrTextContent("Base blend factor",
            "Determines how much the history buffer is blended together with current frame result. Higher values means more history contribution, which leads to better anti aliasing, but also more prone to ghosting.");

        public static GUIContent antialiasingQualityStyle = EditorGUIUtility.TrTextContent("Quality",
            "The quality level to use for the selected anti-aliasing method.");

        public static GUIContent taaContrastAdaptiveSharpeningStyle = EditorGUIUtility.TrTextContent(
            "Contrast Adaptive Sharpening",
            "Enables high quality post sharpening to reduce TAA blur. The FSR upscaling overrides this setting if enabled.");

        public static readonly GUIContent taaJitterScaleStyle = EditorGUIUtility.TrTextContent("Jitter Scale",
            "Determines the scale to the jitter applied when TAA is enabled. Lowering this value will lead to less visible flickering and jittering, but also will produce more aliased images.");

        public static readonly GUIContent taaMipBiasStyle = EditorGUIUtility.TrTextContent("Mip Bias",
            "Determines how much texture mip map selection is biased when rendering. Lowering this can slightly reduce blur on textures at the cost of performance. Requires mip maps in textures.");

        public static readonly GUIContent taaVarianceClampScaleStyle = EditorGUIUtility.TrTextContent(
            "Variance Clamp Scale",
            "Determines the strength of the history color rectification clamp. Lower values can reduce ghosting, but produce more flickering. Higher values reduce flickering, but are prone to blur and ghosting.");

        private void OnEnable()
        {
            script = (FRPCamera)target;
            m_additionalCameraData = script.GetComponent<UniversalAdditionalCameraData>();

            frpPostProcessingEnable = serializedObject.FindProperty("m_frpEnable");
            normalQuality = serializedObject.FindProperty("normalQuality");
            debugMode = serializedObject.FindProperty("debugMode");

            // m_overrideURPPostProcessing = serializedObject.FindProperty("m_overrideURPPostProcessing");
            // m_resetProjectMatrixAfterTAA = serializedObject.FindProperty("m_resetProjectMatrixAfterTAA");
            // antialiasing = serializedObject.FindProperty("antialiasing");
            // m_TaaSettings = serializedObject.FindProperty("m_TaaSettings");
            // taaQuality = m_TaaSettings.FindPropertyRelative("quality");
            // taaFrameInfluence = m_TaaSettings.FindPropertyRelative("frameInfluence");
            // taaJitterScale = m_TaaSettings.FindPropertyRelative("jitterScale");
            // taaMipBias = m_TaaSettings.FindPropertyRelative("mipBias");
            // taaVarianceClampScale = m_TaaSettings.FindPropertyRelative("varianceClampScale");
            // taaContrastAdaptiveSharpening = m_TaaSettings.FindPropertyRelative("contrastAdaptiveSharpening");
            //
            // m_SMAAQuality = serializedObject.FindProperty("m_SMAAQuality");
            // renderingPath = serializedObject.FindProperty("renderingPath");

            // fsr
            // fsr3Setting = serializedObject.FindProperty("m_fsr3Setting");
            // fsr3Quality = fsr3Setting.FindPropertyRelative("qualityMode");
            // performSharpenPass = fsr3Setting.FindPropertyRelative("performSharpenPass");
            // sharpness = fsr3Setting.FindPropertyRelative("sharpness");
            // fsrMipmapBias = fsr3Setting.FindPropertyRelative("mipmapBias");
            // enableFP16 = fsr3Setting.FindPropertyRelative("enableFP16");
            // reactiveMask = fsr3Setting.FindPropertyRelative("reactiveMask");
            // transparencyAndCompositionMask = fsr3Setting.FindPropertyRelative("transparencyAndCompositionMask");
            // autoGenerateReactiveMask = fsr3Setting.FindPropertyRelative("autoGenerateReactiveMask");
            // generateReactiveParameters = fsr3Setting.FindPropertyRelative("generateReactiveParameters");
            // GenerateReactiveParams = fsr3Setting.FindPropertyRelative("GenerateReactiveParams");
            // autoGenerateTransparencyAndComposition =
            //     fsr3Setting.FindPropertyRelative("autoGenerateTransparencyAndComposition");
            // generateTransparencyAndCompositionParameters =
            //     fsr3Setting.FindPropertyRelative("generateTransparencyAndCompositionParameters");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();


            EditorGUILayout.Space();


            EditorGUILayout.PropertyField(frpPostProcessingEnable, new GUIContent("FRP Enable"));


            // sync state
            if (script.frpEnable)
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.BeginVertical();

                    if (script.frpEnable)
                    {
                        EditorGUILayout.PropertyField(debugMode);

                        EditorGUILayout.Space(5);
                        EditorGUILayout.PropertyField(normalQuality);
                        EditorGUILayout.Space(5);
                    }

                    GUILayout.EndVertical();
                }

                EditorGUILayout.HelpBox(new GUIContent(
                    "FRP advanced features provide more post-processing and feature, \n" +
                    "please download FRP-Pipeline to replace URP."));

                if (EditorGUILayout.LinkButton(new GUIContent("Download FRP-Pipeline")))
                {
                    Application.OpenURL("https://gitlab-u3d.internal.unity.cn/dongjun.huang/frp-pipeline");
                }

                EditorGUILayout.Space();

                // }else if (script.settingView == 1)
                // {
                //     using (new EditorGUILayout.VerticalScope())
                //     {
                //         GUILayout.BeginVertical();
                //         EditorGUILayout.Space(5);
                //             EditorGUILayout.PropertyField(m_overrideURPPostProcessing,
                //                 new GUIContent("Override URP PostProcessing"));
                //
                //             EditorGUILayout.HelpBox(new GUIContent(
                //                 "Override PostProcessing will disable urp post-processing, \n" +
                //                 "Please manually disable the Post-Processing checkbox in URP Render"));
                //
                //             if (script.IsOverrideURPPostProcessing())
                //             {
                //                 //m_additionalCameraData.renderPostProcessing = false;
                //                 m_additionalCameraData.antialiasing = AntialiasingMode.None;
                //             }
                //         EditorGUILayout.Space(5);
                //         GUILayout.EndVertical();
                //     }
                // }
                // else if (script.settingView == 2)
                // {
                //     using (new EditorGUILayout.VerticalScope())
                //     {
                //         GUILayout.BeginVertical();
                //
                //         EditorGUILayout.PropertyField(antialiasing);
                //         if (script.antialiasing == AntialiasingMode.TemporalAntiAliasing)
                //         {
                //             EditorGUI.indentLevel++;
                //             EditorGUILayout.PropertyField(taaQuality, antialiasingQualityStyle);
                //             {
                //                 // FSR overrides TAA CAS settings. Disable this setting when FSR is enabled.
                //                 bool disableSharpnessControl = UniversalRenderPipeline.asset != null &&
                //                                                (UniversalRenderPipeline.asset.upscalingFilter ==
                //                                                 UpscalingFilterSelection.FSR);
                //                 using var disable = new EditorGUI.DisabledScope(disableSharpnessControl);
                //
                //                 EditorGUILayout.Slider(taaContrastAdaptiveSharpening, 0.0f, 1.0f,
                //                     taaContrastAdaptiveSharpeningStyle);
                //             }
                //
                //             taaFrameInfluence.floatValue = 1.0f - EditorGUILayout.Slider(taaBaseBlendFactorStyle,
                //                 1.0f - taaFrameInfluence.floatValue, 0.6f, 0.98f);
                //             EditorGUILayout.Slider(taaJitterScale, 0.0f, 1.0f, taaJitterScaleStyle);
                //             EditorGUILayout.Slider(taaMipBias, -0.5f, 0.0f, taaMipBiasStyle);
                //
                //             if (taaQuality.intValue >= 2) // Medium
                //                 EditorGUILayout.Slider(taaVarianceClampScale, 0.6f, 1.2f, taaVarianceClampScaleStyle);
                //             EditorGUI.indentLevel--;
                //
                //             EditorGUILayout.PropertyField(m_resetProjectMatrixAfterTAA,
                //                 new GUIContent("Reset ProjectMatrix After TAA"));
                //             EditorGUILayout.HelpBox(new GUIContent(
                //                 "If there are any objects that need to be rendered after TAA, \n" +
                //                 "turning on this option prevents shaking"));
                //         }
                //         else if (script.antialiasing == AntialiasingMode.SubpixelMorphologicalAntiAliasing)
                //         {
                //             EditorGUI.indentLevel++;
                //             EditorGUILayout.PropertyField(m_SMAAQuality);
                //             EditorGUI.indentLevel--;
                //         }
                //
                //         GUILayout.EndVertical();
                //     }
                // }
                // else if (script.settingView == 3)
                // {
                //     using (new EditorGUILayout.VerticalScope())
                //     {
                //         GUILayout.BeginVertical();
                //         if (script.m_fsr3Setting.qualityMode != Fsr3Upscaler.QualityMode.None)
                //         {
                //             if (script.antialiasing == AntialiasingMode.TemporalAntiAliasing)
                //             {
                //                 script.antialiasing = AntialiasingMode.None;
                //             }
                //
                //             UniversalRenderPipeline.asset.upscalingFilter = UpscalingFilterSelection.Auto;
                //             EditorGUILayout.PropertyField(fsr3Quality, new GUIContent("FSR 3.0"));
                //             EditorGUI.indentLevel++;
                //             EditorGUILayout.PropertyField(performSharpenPass);
                //             EditorGUILayout.PropertyField(sharpness);
                //             EditorGUILayout.PropertyField(fsrMipmapBias);
                //             EditorGUILayout.PropertyField(enableFP16);
                //             EditorGUI.indentLevel--;
                //
                //             EditorGUILayout.LabelField("TODO: ");
                //             EditorGUI.indentLevel++;
                //             EditorGUI.indentLevel++;
                //             EditorGUILayout.PropertyField(generateReactiveParameters);
                //             EditorGUI.indentLevel--;
                //             EditorGUI.indentLevel--;
                //         }
                //         else
                //         {
                //             EditorGUILayout.PropertyField(fsr3Quality, new GUIContent("FSR 3.0"));
                //         }
                //
                //         EditorGUILayout.Space(5);
                //         GUILayout.EndVertical();
                //     }
                // }

                EditorGUILayout.Space();
            }


            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }
        }
    }
}