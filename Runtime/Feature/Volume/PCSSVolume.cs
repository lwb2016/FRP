using UnityEngine.Rendering.Universal;

namespace UnityEngine.Rendering.FRP
{   
    [ExecuteInEditMode, VolumeComponentMenu("FRPVolume/URP/PCSS")]
    public class PCSSVolume : VolumeComponent, IPostProcessComponent
    {
        [GeneralSettings, Space(6)]
        public BoolParameter isEnable = new BoolParameter(false);
        
        public bool IsActive()
        {
            return isEnable.value;
        }
 
        public bool IsTileCompatible()
        {
            return false;
        }
    }
}