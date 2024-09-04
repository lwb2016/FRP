using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEngine.Rendering.FRP
{
    [ExecuteAlways]
    [AddComponentMenu("FRP/Lighting/PlanarReflectionMaterialOverride")]
    [RequireComponent(typeof(Renderer))]
    public class PlanarReflectionMaterialOverride : MonoBehaviour
    {
        public string smoothnessProperty = "_Smoothness";
        [Range(0,1)] public float smoothness = 0.5f;
        [Range(-1f,1f)] public float anisotropy = 0.0f;
        [Range(4,10)] public int maxMipLavel = 10;
        public float heightOffset = 0;

        private Renderer planeRenderer;
        private PlanarReflectionVolume m_PRComponent;
        private Volume volume;
        private Material mat;
        
        // Start is called before the first frame update
        void OnEnable()
        {
           // var stack = VolumeManager.instance.stack;
           // m_PRComponent = stack?.GetComponent<PlanarReflectionVolume>();
           if (volume == null)
           {
               volume = GameObject.FindObjectOfType<Volume>();
           }
           if (volume.sharedProfile.TryGet<PlanarReflectionVolume>(out m_PRComponent))
           {
               m_PRComponent.SetBlur(1 - smoothness, anisotropy);
           }
           planeRenderer = GetComponent<Renderer>();
           mat = GetComponent<Renderer>().sharedMaterial;
        }

        // Update is called once per frame
        void Update()
        {
            if(m_PRComponent == null) return;
            if(mat == null) return;
            SetParamWithProperty();
            mat.SetFloat(smoothnessProperty, smoothness);
        }

        void SetParamWithProperty()
        {
           m_PRComponent.SetBlur(1 - smoothness, anisotropy);
           int mipLevel = PerceptualRoughnessToMipmapLevel(maxMipLavel);
           var transform1 = transform;
           var position = transform1.position;
           m_PRComponent.planarHeightOffset.value = position.y + heightOffset;
           m_PRComponent.planeUp.value = Vector3.Normalize(transform1.up);
           m_PRComponent.position.value = position;
           m_PRComponent.resolution.value = 1.0f / UnityEngine.Mathf.Max(1, mipLevel-1);
        }

        int PerceptualRoughnessToMipmapLevel(int maxMipLevel)
        {
            float perceptualRoughness = 1 - smoothness;
            return (int)(perceptualRoughness * (1.7 - 0.7 * perceptualRoughness) * maxMipLevel);
        }
    }
}