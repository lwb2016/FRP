namespace UnityEngine.Rendering.FRP
{
    public class BeamMesh : MonoBehaviour
    {
        public MeshRenderer meshRenderer { get; protected set; }
        public MeshFilter meshFilter { get; protected set; }
        
        public Mesh coneMesh { get; protected set; }
        
        MeshLightBeam m_parent = null;
        
        public void Initialize(MeshLightBeam parent)
        {
            Debug.Assert(parent != null);
            m_parent = parent;
            
            transform.SetParent(m_parent.transform, false);
            meshRenderer = gameObject.GetOrAddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off; // different reflection probes could break batching with GPU Instancing
            meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

            meshFilter = gameObject.GetOrAddComponent<MeshFilter>();
        }

        /// <summary>
        /// Generate the cone mesh and calls UpdateMaterialAndBounds.
        /// Since this process involves recreating a new mesh, make sure to not call it at every frame during playtime.
        /// </summary>
        public void RegenerateMesh(bool masterEnabled)
        {
            Debug.Assert(m_parent);

            switch (m_parent.geomBeamMeshType)
            {
                case BeamMeshType.SpotAuto:
                    break;
            }
         
        }
    }
}