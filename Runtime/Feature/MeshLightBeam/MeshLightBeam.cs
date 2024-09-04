using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace UnityEngine.Rendering.FRP
{
    
    public class MeshLightBeam : MonoBehaviour
    {
        [FormerlySerializedAs("geomMeshType")] 
        public BeamMeshType geomBeamMeshType = BeamMeshType.SpotAuto;
        
        public int m_geomSides = MeshBeamConst.SharedMeshSidesDefault;

        private BeamMesh m_beamMesh = null;
        
        /// <summary>
        /// Returns the effective number of Sides used by this beam.
        /// </summary>
        public int geomSides
        {
            get { return MeshBeamConst.SharedMeshSidesDefault; }
            set { m_geomSides = value;}
        }

        public void GenerateGeometry()
        {
            if (m_beamMesh == null)
            {
                m_beamMesh = new GameObject().GetComponent<BeamMesh>();
                m_beamMesh.Initialize(this);
            }
            
            m_beamMesh.RegenerateMesh(enabled);
        }
    }
}