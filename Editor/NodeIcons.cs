using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Knot.Editor
{
    /// <summary> Loads node header icons named by <see cref="Knot.Node.NodeIconAttribute"/> from any NodeIcons folder. </summary>
    public static class NodeIcons
    {
        private const string FolderToken = "/NodeIcons/";

        private static Dictionary<string, Texture2D> _textures;
        private static Dictionary<Type, Texture2D> _byType;

        public static Texture2D Get(Type nodeType)
        {
            if (nodeType == null)
            {
                return null;
            }

            if (_byType == null)
            {
                Cache();
            }

            _byType.TryGetValue(nodeType, out var texture);
            return texture;
        }

        private static void Cache()
        {
            _byType = new Dictionary<Type, Texture2D>();
            EnsureTextures();

            var types = NodeEditorReflection.NodeTypes;
            for (var i = 0; i < types.Length; i++)
            {
                var type = types[i];
                if (!type.TryGetNodeIcon(out var name)) continue;
                if (_textures.TryGetValue(name, out var texture))
                {
                    _byType[type] = texture;
                }
            }
        }

        private static void EnsureTextures()
        {
            if (_textures != null)
            {
                return;
            }
            
            _textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
            var guids = AssetDatabase.FindAssets("t:Texture2D");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.IndexOf(FolderToken, StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                var name = Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrEmpty(name) || _textures.ContainsKey(name))
                {
                    continue;
                }

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture != null)
                {
                    _textures.Add(name, texture);
                }
            }
        }
    }
}