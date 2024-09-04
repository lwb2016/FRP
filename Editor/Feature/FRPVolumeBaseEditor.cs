using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.FRP;

namespace UnityEditor.Rendering.FRP
{
    public class FRPVolumeBaseEditor : VolumeComponentEditor
    {
        internal VolumeComponent m_volument;
        
        internal GUIStyle m_SettingsGroupStyle;
        internal GUIStyle m_TitleLabelStyle;
        internal PropertyFetcher<object> m_PropertyFetcher;
        
        // settings group <setting, property reference>
        internal Dictionary<SettingsGroup, List<MemberInfo>> m_GroupFields;
        
        public override bool hasAdditionalProperties => false;

        public virtual void OnEnable()
        {
            base.OnEnable();
            
            m_PropertyFetcher = new PropertyFetcher<object>(serializedObject);

            m_GroupFields = new Dictionary<SettingsGroup, List<MemberInfo>>();
            var settings = m_volument.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(t => t.FieldType.IsSubclassOf(typeof(VolumeParameter)))
                .Where(t => (t.IsPublic && t.GetCustomAttributes(typeof(NonSerializedAttribute), false).Length == 0) ||
                            (t.GetCustomAttributes(typeof(SerializeField), false).Length > 0))
                .Where(t => t.GetCustomAttributes(typeof(HideInInspector), false).Length == 0)
                .Where(t => t.GetCustomAttributes(typeof(SettingsGroup), false).Any());
            
            foreach (var setting in settings)
            {
                foreach (var attr in setting.GetCustomAttributes(typeof(SettingsGroup)) as IEnumerable<SettingsGroup>)
                {
                    if (!m_GroupFields.ContainsKey(attr))
                        m_GroupFields[attr] = new List<MemberInfo>();

                    m_GroupFields[attr].Add(setting);
                }
            }
        }
        
        private void SetStyles()
        {
            // set banner label style
            m_TitleLabelStyle = new GUIStyle(GUI.skin.label);
            m_TitleLabelStyle.alignment = TextAnchor.MiddleCenter;
            m_TitleLabelStyle.contentOffset = new Vector2(0f, 0f);

            // get shuriken module title style
            GUIStyle skurikenModuleTitleStyle = "ShurikenModuleTitle";

            // clone it as to not interfere with the original, and adjust it
            m_SettingsGroupStyle = new GUIStyle(skurikenModuleTitleStyle);
            m_SettingsGroupStyle.font = (new GUIStyle("Label")).font;
            m_SettingsGroupStyle.fontStyle = FontStyle.Bold;
            m_SettingsGroupStyle.border = new RectOffset(15, 7, 4, 4);
            m_SettingsGroupStyle.fixedHeight = 22;
            m_SettingsGroupStyle.contentOffset = new Vector2(10f, -2f);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SetStyles();
            
            EditorGUILayout.BeginVertical();
            {
                // header
                GUILayout.Space(10.0f);

                Event e = Event.current;

                // settings groups
                foreach (var group in m_GroupFields)
                {
                    GUILayout.Space(6.0f);
                    Rect rect = GUILayoutUtility.GetRect(16f, 22f, m_SettingsGroupStyle);
                    GUI.Box(rect, ObjectNames.NicifyVariableName(group.Key.GetType().Name), m_SettingsGroupStyle);
                    if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
                    {
                        group.Key.isExpanded = !group.Key.isExpanded;
                        e.Use();
                    }

                    if (!group.Key.isExpanded)
                        continue;

                    foreach (var field in group.Value)
                    {

                        var parameter = Unpack(m_PropertyFetcher.Find(field.Name));
                        var displayName = parameter.displayName;
                        var hasDisplayName = field.GetCustomAttributes(typeof(ParameterDisplayName)).Any();
                        if (hasDisplayName)
                        {
                            var displayNameAttribute = field.GetCustomAttributes(typeof(ParameterDisplayName)).First() as ParameterDisplayName;
                            displayName = displayNameAttribute.name;
                        }

                        PropertyField(parameter, new GUIContent(displayName));
                    }

                    GUILayout.Space(6.0f);
                }
            }
            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }

    }
}