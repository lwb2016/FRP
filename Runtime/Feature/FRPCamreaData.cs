using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.Rendering.Universal;

namespace UnityEngine.Rendering.FRP
{
    public class FRPCamreaData
    {
        static Dictionary<(Camera, int), FRPCamreaData> s_Cameras = new Dictionary<(Camera, int), FRPCamreaData>();
        
        /// <summary>Camera name.</summary>
        public string name { get; private set; } // Needs to be cached because camera.name generates GCAllocs
        
        /// <summary>Volume stack used for this camera.</summary>
        public VolumeStack volumeStack { get; private set; }
        
        // Always true for cameras that just got added to the pool - needed for previous matrices to
        // avoid one-frame jumps/hiccups with temporal effects (motion blur, TAA...)
        internal bool isFirstFrame { get; private set; }
        
        internal Material uberMaterial;
        
        // BufferedRTHandleSystem m_HistoryRTSystem = new BufferedRTHandleSystem();
        // public RTHandleProperties historyRTHandleProperties { get { return m_HistoryRTSystem.rtHandleProperties; } }
        
        internal RTHandle ssaoRTHadnle;
        internal RTHandle hbaoRTHadnle;
        internal RTHandle ssgiRTHadnle;
        internal RTHandle planarReflectionRTHandle;
        internal RTHandle planarReflectDepthRTHandle;
        internal RTHandle planarReflectionMipRTHandle;
        internal RTHandle[] gbufferRTHandles;
        internal RTHandle gBufferDepthTexture;
        
        internal FRPCamreaData(Camera cam)
        {
            name = cam.name;
            volumeStack = VolumeManager.instance.CreateStack();
            Reset();
        }

        /// <summary>
        /// Reset the camera persistent informations.
        /// This needs to be used when doing camera cuts for example in order to avoid information from previous unrelated frames to be used.
        /// </summary>
        public void Reset()
        {
            isFirstFrame = true;
        }
        
        internal static void ClearAll()
        {
            foreach (var cam in s_Cameras)
            {
                cam.Value.Dispose();
            }

            s_Cameras.Clear();
        }

        void Dispose()
        {
            VolumeManager.instance.DestroyStack(volumeStack);
            
            ssaoRTHadnle?.Release();
            hbaoRTHadnle?.Release();
            ssgiRTHadnle?.Release();
            planarReflectionRTHandle?.Release();
            planarReflectionMipRTHandle?.Release();
            
            if (gbufferRTHandles != null)
            {
                foreach (var gbufferRT in gbufferRTHandles)
                {
                    gbufferRT?.Release();
                }
            }
            
            gBufferDepthTexture?.Release();
        }
        
        public static FRPCamreaData GetOrCreate(Camera camera, CameraData cameraData, int xrMultipassId = 0)
        {
            FRPCamreaData frpCamera;

            if (!s_Cameras.TryGetValue((camera, xrMultipassId), out frpCamera))
            {
                frpCamera = new FRPCamreaData(camera);
                s_Cameras.Add((camera, xrMultipassId), frpCamera);
            }
            
            return frpCamera;
        }
    }
}