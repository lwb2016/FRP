using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.Rendering.FRP
{
    [ExecuteAlways]
    [RequireComponent(typeof(Light))]
    public partial class AreaLight : MonoBehaviour
    {
        internal class MinValueAttribute : PropertyAttribute
        {
            public float min;

            public MinValueAttribute (float min)
            {
                this.min = min;
            }
        }
        
        public bool m_RenderSource = false;
        public Vector3 m_Size = new Vector3(1, 1, 2);
        [Range(0, 179)] public float m_Angle = 0.0f;
       // public Color m_Color = Color.white;
        
        Mesh m_SourceMesh;
        MeshRenderer m_SourceRenderer;
        private MeshFilter mfs;
        private Light light;
        private Shader m_SourceShader;
        Vector2 m_CurrentQuadSize = Vector2.one;
        Vector3 m_CurrentSize = Vector3.zero;
        float m_CurrentAngle = -1.0f;
        bool m_Initialized = false;
        
        private Material m_SourceMaterial;

        public Material SourceMaterial
        {
            get
            {
                if (m_SourceMaterial != null) return m_SourceMaterial;
                m_SourceMaterial = new Material(Shader.Find("Hidden/FernRP/AreaLightSource"));
                return m_SourceMaterial;
            }
        }


        private Mesh m_areaQuadSourceMesh;
        private Mesh AreaQuadSourceMesh
        {
            get
            {
                if (m_areaQuadSourceMesh != null)
                {
                    return m_areaQuadSourceMesh;
                }
                
                Mesh val = new Mesh();
                m_areaQuadSourceMesh = val;
                m_areaQuadSourceMesh.name = "NormalQuad";
                
                Vector3[] vertices = new Vector3[4]
                {
                    new Vector3(-0.5f, -0.5f, 0),
                    new Vector3(0.5f, -0.5f, 0),
                    new Vector3(-0.5f, 0.5f, 0),
                    new Vector3(0.5f, 0.5f, 0)
                };
                m_areaQuadSourceMesh.SetVertices(vertices);
                m_areaQuadSourceMesh.SetUVs(0, new List<Vector2>
                {
                    new Vector2(0f, 0),
                    new Vector2(0f, 1),
                    new Vector2(1f, 0),
                    new Vector2(1f, 1)
                });
                m_areaQuadSourceMesh.SetIndices(new int[6] { 0, 2, 1, 3, 1, 2 }, (MeshTopology)0, 0, false);
                return m_areaQuadSourceMesh;
            }
        }

        public void RenderSource()
        {
            if (m_RenderSource)
            {
                m_SourceMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/FernRP/AreaLightSource"));
                if (SourceMaterial == null)
                {
                    Debug.LogError("AreaLight: " + gameObject.name + " Material not allow null");
                    return;
                }
                m_SourceMaterial.hideFlags = HideFlags.HideInInspector | HideFlags.HideAndDontSave;
        
                if (m_SourceMesh == null)
                {
                    m_SourceMesh = AreaQuadSourceMesh;
                    if (m_SourceMesh == null)
                    {
                        Debug.LogError("AreaLight: " + gameObject.name + " SourceMesh not allow null");
                    }
                    return;
                }
        
                m_SourceRenderer = GetComponent<MeshRenderer>();
                if (m_SourceRenderer == null)
                {
                    m_SourceRenderer = gameObject.AddComponent<MeshRenderer>();
                }
                m_SourceRenderer.enabled = true;
                m_SourceRenderer.sharedMaterial = m_SourceMaterial;
        
                mfs = gameObject.GetComponent<MeshFilter>();
                if (mfs == null)
                {
                    mfs = gameObject.AddComponent<MeshFilter>();
                }
                mfs.sharedMesh = m_SourceMesh;

                mfs.hideFlags = HideFlags.HideInInspector;
                m_SourceRenderer.hideFlags = HideFlags.HideInInspector;
            }
            else
            {
                m_SourceRenderer = GetComponent<MeshRenderer>();
                if (m_SourceRenderer != null)
                {
                    m_SourceRenderer.enabled = false;
                }
            }
        }
        
        bool Init()
        {
            if (m_Initialized)
                return true;

            light = GetComponent<Light>();

            RenderSource();
            
            Transform t = transform;
            if (t.localScale != Vector3.one)
            {
#if UNITY_EDITOR
                Debug.LogError("AreaLights don't like to be scaled. Setting local scale to 1.", this);
#endif
                t.localScale = Vector3.one;
            }

            m_Initialized = true;
            return true;
        }
        
        void OnEnable()
        {
            if(!Init())
                return;
            RenderSource();
            UpdateSourceMesh();
        }
        
        void OnDestroy()
        {
            if (m_SourceMesh != null)
                DestroyImmediate(m_SourceMesh);
            if(m_SourceMaterial != null)
                DestroyImmediate(m_SourceMaterial);

            m_areaQuadSourceMesh = null;
            m_SourceMesh = null;
            m_SourceMaterial = null;
            m_Initialized = false;
        }

        static Vector3[] vertices = new Vector3[4];
        static int[] triangles =  new[] { 0, 1, 2, 3, 0, 2 };
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private void UpdateSourceMesh()
        {
            if(m_SourceMesh == null) return;
            if (!(Math.Abs(m_Size.x - m_CurrentQuadSize.x) > Mathf.Epsilon) &&
                !(Math.Abs(m_Size.y - m_CurrentQuadSize.y) > Mathf.Epsilon)) return;
            m_Size.x = Mathf.Max(m_Size.x, 0);
            m_Size.y = Mathf.Max(m_Size.y, 0);
            m_Size.z = Mathf.Max(m_Size.z, 0);
            float x = m_Size.x * 0.5f;
            float y = m_Size.y * 0.5f;
            // To prevent the source quad from getting into the shadowmap, offset it back a bit.
            float z = -0.001f;
            vertices[0].Set(-x,  -y, z);
            vertices[1].Set( x, -y, z);
            vertices[2].Set( -x,  y, z);
            vertices[3].Set(x, y, z);

            m_SourceMesh.vertices = vertices;
            //m_SourceMesh.triangles = triangles;

            m_CurrentQuadSize.x = m_Size.x;
            m_CurrentQuadSize.y = m_Size.y;
                
            if (Math.Abs(m_Angle - m_CurrentAngle) > Mathf.Epsilon)
            {
                m_SourceMesh.bounds = GetFrustumBounds();
            }
        }

        private void Update()
        {
            if(!Init())
                return;
            #if UNITY_EDITOR
                // Todo: Should Hide this param
                light.spotAngle = 179;
                light.innerSpotAngle = 0;
                light.range = Mathf.Max(2, m_Size.magnitude);
                UpdateSourceMesh();
                if(m_RenderSource) m_SourceMaterial.SetVector(EmissionColor, GetColor() * light.intensity);
            #endif
            SetupBuffer();
        }

        private Bounds GetFrustumBounds()
        {
            if (m_Angle == 0.0f)
                return new Bounds(Vector3.zero, m_Size);

            float tanhalffov = Mathf.Tan(m_Angle * 0.5f * Mathf.Deg2Rad);
            float near = m_Size.y * 0.5f / tanhalffov;
            float z = m_Size.z;
            float y = (near + m_Size.z) * tanhalffov * 2.0f;
            float x = m_Size.x * y / m_Size.y;
            return new Bounds(Vector3.forward * m_Size.z * 0.5f, new Vector3(x, y, z));
        }
        
        float GetNearToCenter()
        {
            if (m_Angle == 0.0f)
                return 0;

            return m_Size.y * 0.5f / Mathf.Tan(m_Angle * 0.5f * Mathf.Deg2Rad);
        }
        
        Matrix4x4 GetOffsetMatrix(float zOffset)
        {
            Matrix4x4 m = Matrix4x4.identity;
            m.SetColumn(3, new Vector4(0, 0, zOffset, 1));
            return m;
        }
        
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.white;

            if (m_Angle == 0.0f)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(new Vector3(0, 0, 0.5f * m_Size.z), m_Size);
                return;
            }

            float near = GetNearToCenter();
            Gizmos.matrix = transform.localToWorldMatrix * GetOffsetMatrix(-near);

            Gizmos.DrawFrustum(Vector3.zero, m_Angle, near + m_Size.z, near, m_Size.x/m_Size.y);

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.yellow;
            Bounds bounds = GetFrustumBounds();
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }  
        
        public Matrix4x4 GetProjectionMatrix(bool linearZ = false)
        {
            Matrix4x4 m;

            if (m_Angle == 0.0f)
            {
                m = Matrix4x4.Ortho(-0.5f * m_Size.x, 0.5f * m_Size.x, -0.5f * m_Size.y, 0.5f * m_Size.y, 0, -m_Size.z);
            }
            else
            {
                float near = GetNearToCenter();
                if (linearZ)
                {
                    m = PerspectiveLinearZ(m_Angle, m_Size.x/m_Size.y, near, near + m_Size.z);
                }
                else
                {
                    m = Matrix4x4.Perspective(m_Angle, m_Size.x/m_Size.y, near, near + m_Size.z);
                    m = m * Matrix4x4.Scale(new Vector3(1, 1, -1));
                }
                m = m * GetOffsetMatrix(near);
            }
		
            return m * transform.worldToLocalMatrix;
        }
        
        public Vector4 MultiplyPoint(Matrix4x4 m, Vector3 v)
        {
            Vector4 res;
            res.x = m.m00 * v.x + m.m01 * v.y + m.m02 * v.z + m.m03;
            res.y = m.m10 * v.x + m.m11 * v.y + m.m12 * v.z + m.m13;
            res.z = m.m20 * v.x + m.m21 * v.y + m.m22 * v.z + m.m23;
            res.w = m.m30 * v.x + m.m31 * v.y + m.m32 * v.z + m.m33;
            return res;
        }
        
        Matrix4x4 PerspectiveLinearZ(float fov, float aspect, float near, float far)
        {
            // A vector transformed with this matrix should get perspective division on x and y only:
            // Vector4 vClip = MultiplyPoint(PerspectiveLinearZ(...), vEye);
            // Vector3 vNDC = Vector3(vClip.x / vClip.w, vClip.y / vClip.w, vClip.z);
            // vNDC is [-1, 1]^3 and z is linear, i.e. z = 0 is half way between near and far in world space.

            float rad = Mathf.Deg2Rad * fov * 0.5f;
            float cotan = Mathf.Cos(rad) / Mathf.Sin(rad);
            float deltainv = 1.0f / (far - near);
            Matrix4x4 m;

            m.m00 = cotan / aspect;	m.m01 = 0.0f;	m.m02 = 0.0f;			 m.m03 = 0.0f;
            m.m10 = 0.0f;			m.m11 = cotan; 	m.m12 = 0.0f;			 m.m13 = 0.0f;
            m.m20 = 0.0f;			m.m21 = 0.0f;	m.m22 = 2.0f * deltainv; m.m23 = - (far + near) * deltainv;
            m.m30 = 0.0f;			m.m31 = 0.0f;	m.m32 = 1.0f;			 m.m33 = 0.0f;

            return m;
        }
        
        public Vector4 GetPosition()
        {
            Transform t = transform;

            if (m_Angle == 0.0f)
            {
                Vector3 dir = -t.forward;
                return new Vector4(dir.x, dir.y, dir.z, 0);
            }

            Vector3 pos = t.position - GetNearToCenter() * t.forward;
            return new Vector4(pos.x, pos.y, pos.z, 1);
        }

    }
}

