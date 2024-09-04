using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.Rendering.FRP
{
    partial class AreaLight : MonoBehaviour
    {
        static Texture2D s_TransformInvTexture_Specular;
        static Texture2D s_TransformInvTexture_Diffuse;
        static Texture2D s_AmpDiffAmpSpecFresnel;

        static readonly float[,] offsets = new float[4,2] {{1, 1}, {1, -1}, {-1, -1}, {-1, 1}};
        
        private static readonly int LightPos = Shader.PropertyToID("_LightPos");
        private static readonly int LightColor = Shader.PropertyToID("_LightColor");
        private static readonly int LightAsQuad = Shader.PropertyToID("_LightAsQuad");

        [HideInInspector]
        public Matrix4x4 lightVerts = new Matrix4x4();

        private Vector4 lightVertRow = Vector4.zero;
        private Vector3 lightPoint = Vector3.zero;
        void SetupBuffer()
        {
            //Shader.SetGlobalVector(LightPos, transform.position);
            //Shader.SetGlobalVector(LightColor, GetColor());
            
            // Needed as we're using the vert_deferred vertex shader from UnityDeferredLibrary.cginc
            // TODO: Make the light render as quad if it intersects both near and far plane.
            // (Also missing: rendering as front faces when near doesn't intersect, stencil optimisations)
            //Shader.SetGlobalFloat(LightAsQuad, 0);
            
            // A little bit of bias to prevent the light from lighting itself - the source quad
            float z = 0.01f;
            Transform t = transform;

            for (int i = 0; i < 4; i++)
            {
                lightPoint = t.TransformPoint(new Vector3(m_Size.x * offsets[i, 0], m_Size.y * offsets[i, 1], z) * 0.5f);
                lightVertRow.x = lightPoint.x;
                lightVertRow.y = lightPoint.y;
                lightVertRow.z = lightPoint.z;
                lightVertRow.w = i switch
                {
                    0 => m_Size.x,
                    1 => m_Size.y,
                    2 => Mathf.Pow(m_Size.z, 2),
                    _ => 0 // TODO: angle?
                };
                lightVerts.SetRow(i, lightVertRow);
            }
        }
        
        public Color GetColor()
        {
            if (QualitySettings.activeColorSpace == ColorSpace.Gamma)
                return GetComponent<Light>().color * GetComponent<Light>().intensity;

            // return new Color(
            //     (light.color.r * light.intensity),
            //     (light.color.g * light.intensity),
            //     (light.color.b * light.intensity),
            //     1.0f
            // );
            
            return new Color(
                Mathf.GammaToLinearSpace(GetComponent<Light>().color.r * GetComponent<Light>().intensity),
                Mathf.GammaToLinearSpace(GetComponent<Light>().color.g * GetComponent<Light>().intensity),
                Mathf.GammaToLinearSpace(GetComponent<Light>().color.b * GetComponent<Light>().intensity),
                1.0f
            );
        }
    }

}

