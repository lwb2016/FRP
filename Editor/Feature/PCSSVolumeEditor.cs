using UnityEngine.Rendering.FRP;

namespace UnityEditor.Rendering.FRP
{
    [CustomEditor(typeof(PCSSVolume))]
    public class PCSSVolumeVolumeEditor : FRPVolumeBaseEditor
    {
        public override void OnEnable()
        { 
            m_volument = (PCSSVolume)target;
            base.OnEnable();
        }
    }
}