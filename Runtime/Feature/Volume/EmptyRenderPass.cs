using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEngine.Rendering.FRP
{
    public class EmptyRenderPass : ScriptableRenderPass
    {
        public EmptyRenderPass()
        {
            renderPassEvent = RenderPassEvent.AfterRendering;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            
            return;
        }
    }
}