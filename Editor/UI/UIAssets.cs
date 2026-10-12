using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace XNodeEditor.UI
{
    internal static class UIAssets
    {
        public static VisualTreeAsset LoadUxml(string fileName)
        {
            return Load<VisualTreeAsset>(fileName);
        }

        public static StyleSheet LoadUss(string fileName)
        {
            return Load<StyleSheet>(fileName);
        }

        private static T Load<T>(string fileName) where T : Object
        {
            var guids = AssetDatabase.FindAssets($"{fileName} t:{typeof(T).Name}");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.IndexOf("/Editor/UI/", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (!System.IO.Path.GetFileNameWithoutExtension(path).Equals(
                        fileName, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                return AssetDatabase.LoadAssetAtPath<T>(path);
            }

            Debug.LogError($"xNode UITK asset '{fileName}' was not found under Editor/Ui.");
            return null;
        }
    }
}