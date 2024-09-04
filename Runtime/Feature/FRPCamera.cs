using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace UnityEngine.Rendering.FRP
{
    
    
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [ImageEffectAllowedInSceneView]
    [HelpURL("https://gitlab-u3d.internal.unity.cn/dongjun.huang/fernrender/-/wikis/home")]
    [ExecuteAlways]
    public class FRPCamera : MonoBehaviour
    {
        [Serializable]
        public enum DebugMode
        {
            None = 0,
            BaseColor,
            Normal,
            HBAO,
            SSGI,
            PlanarReflection,
            MotionVector,
            DepthCopyTexture
        }
        
        [Serializable]
        public enum RenderingPath
        {
            Forward,
            ForwardAdd,
            Deferred,
        }
        
		#if UNITY_EDITOR
	        public int settingView = 0;// 0: General 1:PostProcessing 2: Anti-Alias 3: FSR
		#endif
       
        public bool m_overrideURPPostProcessing = false;
        public bool m_resetProjectMatrixAfterTAA = false;
        public RenderingPath renderingPath = RenderingPath.Forward;
        public AntialiasingMode antialiasing = AntialiasingMode.None;
       
        public AntialiasingQuality m_SMAAQuality = AntialiasingQuality.Medium;
        public NormalQuality normalQuality = NormalQuality.Low;
        //public bool renderGbuffer = true;
        public DebugMode debugMode = DebugMode.None;

        [FormerlySerializedAs("frpEnable")] [FormerlySerializedAs("frpPostProcessingEnable")]
        [SerializeField]
        private bool m_frpEnable = false;
        private UniversalAdditionalCameraData m_CameraData;
        public UniversalAdditionalCameraData cameraData
        {
            get
            {
                if (m_CameraData == null) m_CameraData = GetComponent<UniversalAdditionalCameraData>();
                return m_CameraData;
            }
            set => m_CameraData = value;
        }
        
        public bool frpEnable
        {
            get => m_frpEnable;
            set => m_frpEnable = value;
        }

        private bool m_stackAnyPostProcessingEnable = false;
        public bool stackAnyPostProcessingEnable => m_stackAnyPostProcessingEnable;
        
        internal FURPTaaPersistentData m_TaaPersistentData = new FURPTaaPersistentData();
        internal FURPTaaPersistentData taaPersistentData => m_TaaPersistentData;
        [SerializeField] internal TemporalAA.Settings m_TaaSettings = TemporalAA.Settings.Create();
        internal ref TemporalAA.Settings taaSettings => ref m_TaaSettings;
        internal bool needDepth = false;

        public void UpdateCameraStack()
        {
            if(cameraData == null) return;
            if (cameraData.renderType != CameraRenderType.Base) return;
            if (cameraData.cameraStack != null)
            {
                for (int i = 0; i < cameraData.cameraStack.Count; ++i)
                {
                    Camera currCamera = cameraData.cameraStack[i];
                    if (currCamera == null)
                    {
                        continue;
                    }

                    if (currCamera.isActiveAndEnabled)
                    {
                        currCamera.TryGetComponent<FRPCamera>(out var data);
                        data.frpEnable = frpEnable;
                        data.m_stackAnyPostProcessingEnable = m_overrideURPPostProcessing;
                        data.antialiasing = antialiasing;
                    }
                }
            }
        }

        public void OnEnable()
        {
            m_CameraData = GetComponent<UniversalAdditionalCameraData>();
        }

        public void OnDisable()
        {
            m_TaaPersistentData?.DeallocateTargets();
        }

        public void OnDestroy()
        {
            m_TaaPersistentData?.DeallocateTargets();
        }
    }
}
