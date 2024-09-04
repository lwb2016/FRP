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
    [CustomEditor(typeof(PlanarReflectionVolume))]
    public class PlanarReflectionVolumeEditor : FRPVolumeBaseEditor
    {
        public override void OnEnable()
        { 
            m_volument = (PlanarReflectionVolume)target;
            base.OnEnable();
        }
    }
}
