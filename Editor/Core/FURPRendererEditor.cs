using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.FRP;

namespace UnityEditor.Rendering.FRP
{
    [CustomEditor(typeof(FRPRenderer))]
    public class FURPRendererEditor : Editor
    {
        FRPRenderer script;
        private SerializedProperty renderPipelineAsset;

        private void OnEnable()
        {
            script = (FRPRenderer)target;

            renderPipelineAsset = serializedObject.FindProperty("renderPipelineAsset");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            
            EditorGUI.BeginChangeCheck();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(renderPipelineAsset);

                if (GraphicsSettings.renderPipelineAsset != script.renderPipelineAsset)
                {
                    if (GUILayout.Button("Apply", GUILayout.MaxWidth(70f)))
                    {
                        script.ApplyRenderPipeline();
                    }
                }
                GUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }
        }
    }
}