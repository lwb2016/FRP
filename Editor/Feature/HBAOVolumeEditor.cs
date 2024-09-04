using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.Rendering;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine.Rendering;
using UnityEngine.Rendering.FRP;

namespace UnityEditor.Rendering.FRP
{
    [CustomEditor(typeof(HBAOVolume))]
    public class HBAOEditor : FRPVolumeBaseEditor
    {
        public override void OnEnable()
        {
            m_volument = (HBAOVolume)target;
            base.OnEnable();
        }
    }
}
