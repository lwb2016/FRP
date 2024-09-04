namespace UnityEngine.Rendering.FRP
{
    [ExecuteAlways]
    public class PlanarReflectionBounding : MonoBehaviour
    {
        private static PlanarReflectionBounding m_Instance;
        public static PlanarReflectionBounding Instance => m_Instance;
        
        public Bounds bounds = new Bounds(Vector3.zero, Vector3.one);
        public Transform centerFollow = null;
        public Vector3 centerOffset = Vector3.zero;

        private Vector3 screenPoint;
        private Vector4 aabbViewportPoints;
        private Vector3[] globalTempCubeCorners = new Vector3[8];

        private void OnEnable()
        {
            m_Instance = this;
        }

        public void CalculateBounds()
        {
            if(centerFollow == null) return;
            bounds.center = centerFollow.transform.position + centerOffset;
        }
        
        public void GetCornersFromBounds()
        {
            Vector3 max = bounds.max, min = bounds.min;
            
            globalTempCubeCorners[0] = max;
            globalTempCubeCorners[1] = min;
            globalTempCubeCorners[2] = new Vector3(min.x, max.y, min.z);
            globalTempCubeCorners[3] = new Vector3(min.x, max.y, max.z);
            globalTempCubeCorners[4] = new Vector3(max.x, max.y, min.z);
            globalTempCubeCorners[5] = new Vector3(max.x, min.y, max.z);
            globalTempCubeCorners[6] = new Vector3(min.x, min.y, max.z);
            globalTempCubeCorners[7] = new Vector3(max.x, min.y, min.z);
        }

        Vector3 WorldToViewportPoint(Camera camera, Matrix4x4 m_CameraToWorldMatrix, Matrix4x4 m_WorldToClipMatrix, Vector3 worldPoint)
        {
            bool tempBool;
            screenPoint = WorldToScreenPoint(camera, m_CameraToWorldMatrix, m_WorldToClipMatrix, worldPoint);
            return ScreenToViewportPoint(camera, screenPoint);
        }
        
        public Vector3 ScreenToViewportPoint(Camera camera, Vector3 screenPos)
        {
            var screenViewportRect = camera.pixelRect;
            float nx = (screenPos.x - screenViewportRect.x) / screenViewportRect.width;
            float ny = (screenPos.y - screenViewportRect.y) / screenViewportRect.height;
            return new Vector3(nx, ny, screenPos.z);
        }
        
        Vector3 WorldToScreenPoint(Camera camera, Matrix4x4 m_CameraToWorldMatrix, Matrix4x4 m_WorldToClipMatrix, Vector3 v)
        {
            Rect viewport = camera.pixelRect;
            
            Vector3 outP;
            CameraProject(v, m_CameraToWorldMatrix, m_WorldToClipMatrix, viewport, out outP);
            return outP;
        }
        
        void CameraProject(Vector3 p, Matrix4x4 cameraToWorld, Matrix4x4 worldToClip, Rect viewport, out Vector3 outP)
        {
            Vector3 clipPoint;
            clipPoint = worldToClip.MultiplyPoint(p);
            Vector3 cameraPos = cameraToWorld.GetPosition();
            Vector3 dir = p - cameraPos;
            // The camera/projection matrices follow OpenGL convention: positive Z is towards the viewer.
            // So negate it to get into Unity convention.
            Vector3 forward = new Vector3(-cameraToWorld.m02, -cameraToWorld.m12, -cameraToWorld.m22);
            float dist = Vector3.Dot(dir, forward);

            outP.x = viewport.x + (1.0f + clipPoint.x) * viewport.width * 0.5f;
            outP.y = viewport.y + (1.0f + clipPoint.y) * viewport.height * 0.5f;
            //outP.z = (1.0f + clipPoint.z) * 0.5f;
            outP.z = dist;
        }
        
        public Vector4 GetScreenPercentage(Camera camera, Matrix4x4 m_CameraToWorldMatrix, Matrix4x4 m_WorldToClipMatrix)
        {
            GetCornersFromBounds();
            float minX = 1, minY = 1, maxX = 0, maxY = 0;

            for (int i = 0; i < 8; ++i)
            {
                Vector3 p = WorldToViewportPoint(camera, m_CameraToWorldMatrix, m_WorldToClipMatrix, globalTempCubeCorners[i]);
                
                if (p.x < minX)
                {
                    minX = p.x;
                }
                else if (p.x > maxX)
                {
                    maxX = p.x;
                }

                if (p.y < minY)
                {
                    minY = p.y;
                }
                else if (p.y > maxY)
                {
                    maxY = p.y;
                }
            }

            minX = Mathf.Max(minX, 0.00f);
            minY = Mathf.Max(minY, 0.00f);
            maxX = Mathf.Min(maxX, 1f);
            maxY = Mathf.Min(maxY, 1f);
            
            aabbViewportPoints.x = minX;
            aabbViewportPoints.y = minY;
            aabbViewportPoints.z = maxX;
            aabbViewportPoints.w = maxY;
            
            return aabbViewportPoints;
        }

        #if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Color ogColor = Gizmos.color;
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            Gizmos.color = Color.white;
            
            Gizmos.color = ogColor;
        }
        #endif
    }
}
