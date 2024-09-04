using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.FRP;

namespace UnityEditor.Rendering.FRP
{
    public class FRPRendererMenu : MonoBehaviour
    {
        private const string WeatherRainKey = "_WEATHER_RAIN";
        private const string WeatherSnowKey = "_WEATHER_SNOW";
        
        [MenuItem("GameObject/Rendering/FRP Renderer", false, 100)]
        public static void CreateFURPRenderer()
        {
            var furpRenderer = GameObject.FindObjectOfType<FRPRenderer>();
            if (furpRenderer == null)
            {
                var furpRenderGameObject = new GameObject();
                furpRenderGameObject.name = "FRP Renderer";
                var renderPipelineAsset = GraphicsSettings.renderPipelineAsset;
                furpRenderer = furpRenderGameObject.AddComponent<FRPRenderer>();
                furpRenderer.renderPipelineAsset = renderPipelineAsset;
                GraphicsSettings.renderPipelineAsset = renderPipelineAsset;
            }
            
            Selection.activeObject = furpRenderer.gameObject;
        }
        
        public static class AssetDatabaseHelper
        {
            /// <summary>
            /// Finds all assets of type T in the project.
            /// </summary>
            /// <param name="extension">Asset type extension i.e ".mat" for materials</param>
            /// <typeparam name="T">The type of material you are looking for</typeparam>
            /// <returns>A IEnumerable object</returns>
            public static IEnumerable<T> FindAssets<T>(string extension = null)
            {
                string typeName = typeof(T).ToString();
                int i = typeName.LastIndexOf('.');
                if (i != -1)
                {
                    typeName = typeName.Substring(i+1, typeName.Length - i-1);
                }

                string query = !string.IsNullOrEmpty(extension) ? $"t:{typeName} glob:\"**/*{extension}\"" : $"t:{typeName}";

                foreach (var guid in AssetDatabase.FindAssets(query))
                {
                    var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                    if (asset is T castAsset)
                        yield return castAsset;
                }
            }
        }
        [MenuItem("GameObject/Light/FRP Area Light", false, 100)]
        public static void CreateFURPAreaLight()
        {
            var furpAreaLihgtGO = new GameObject();
            furpAreaLihgtGO.name = "FRP Area Light";
            var furpAreaLihgt = furpAreaLihgtGO.AddComponent<Light>();
            furpAreaLihgt.type = LightType.Spot;
            furpAreaLihgtGO.AddComponent<AreaLight>();
        }

        [MenuItem("FRP/Re Import All ShaderGraph", false, 0)]
        public static void ReImportAllShaderGraph()
        {
            var shaders = AssetDatabaseHelper.FindAssets<Shader>(".shadergraph");
            foreach (var shader in shaders)
            {
                var path = AssetDatabase.GetAssetPath(shader);
                AssetDatabase.ImportAsset(path);
            }
        }
        
        [MenuItem("FRP/Debug/Weather/None", false, 2)]
        public static void WeatherNone()
        {
            Shader.DisableKeyword(WeatherRainKey);
            Shader.DisableKeyword(WeatherSnowKey);
        }
        
        [MenuItem("FRP/Debug/Weather/Rain", false, 3)]
        public static void WeatherRain()
        {
            Shader.EnableKeyword(WeatherRainKey);
            Shader.DisableKeyword(WeatherSnowKey);
        }
        [MenuItem("FRP/Debug/Weather/Snow", false, 4)]
        public static void WeatherSnow()
        {
            Shader.EnableKeyword(WeatherSnowKey);
            Shader.DisableKeyword(WeatherRainKey);
        }
    }
}