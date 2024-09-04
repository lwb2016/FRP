using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEngine.Rendering.FRP
{
    [ExecuteAlways]
    public class FRPRenderer : MonoBehaviour
    {
        public RenderPipelineAsset renderPipelineAsset;
        
        private float cameraAspect = 0;
        private float cameraFov = 0;
        
        private static FRPRenderer instance;
        public static FRPRenderer Get => instance != null ? instance : null;
        
        private void OnEnable()
        {
            instance = this;
            GraphicsSettings.renderPipelineAsset = renderPipelineAsset;
            //RenderPipelineManager.beginCameraRendering += OnBeginCameraRender;
        }

        private void OnDisable()
        {
            instance = null;
            //RenderPipelineManager.beginCameraRendering -= OnBeginCameraRender;
        }

        private void OnDestroy()
        {
            instance = null;
            //RenderPipelineManager.beginCameraRendering -= OnBeginCameraRender;
        }

        private void OnBeginCameraRender(ScriptableRenderContext context, Camera currentCamera)
        {
            
        }

        public void ApplyRenderPipeline()
        {
            GraphicsSettings.renderPipelineAsset = renderPipelineAsset;
        }
    }
}

