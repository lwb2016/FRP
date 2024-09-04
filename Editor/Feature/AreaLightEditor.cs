using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.FRP;
using UnityEngine.Rendering.Universal;
using UnityEditor.Rendering.Universal;
using UnityEngine.Rendering;

namespace UnityEditor.Rendering.FRP
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(AreaLight))]
    internal class AreaLightEditor : Editor
    {
        AreaLight targetObject;
        private SerializedProperty m_Size;

        private void OnEnable()
        {
            targetObject = (AreaLight)target;

            m_Size = serializedObject.FindProperty("m_Size");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            
           // EditorGUILayout.PropertyField(m_RenderSource);
           EditorGUILayout.BeginHorizontal();
               if (GUILayout.Button("Show Mesh", GUILayout.MaxWidth(80f)))
               {
                   targetObject.m_RenderSource = true;
                   targetObject.RenderSource();
               }
               if (GUILayout.Button("Hide Mesh", GUILayout.MaxWidth(80f)))
               {
                   targetObject.m_RenderSource = false;
                   targetObject.RenderSource();
               }
           EditorGUILayout.EndHorizontal();
           EditorGUILayout.BeginVertical();
           m_Size.vector3Value = new Vector3(
               EditorGUILayout.FloatField("Size X", Mathf.Max(1, m_Size.vector3Value.x)), 
               EditorGUILayout.FloatField("Size Y", Mathf.Max(1, m_Size.vector3Value.y)),
               EditorGUILayout.FloatField("Range", Mathf.Max(1, m_Size.vector3Value.z))); 
           
           EditorGUILayout.EndVertical();
           
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }
        }
    }
}
