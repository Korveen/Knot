using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Knot.Editor.Internal
{
    /// <summary> Handles caching of custom editor classes and their target types. Accessible with GetEditor(Type type) </summary>
    /// <typeparam name="T">Editor Type. Should be the type of the deriving script itself (eg. NodeEditor) </typeparam>
    /// <typeparam name="A">Attribute Type. The attribute used to connect with the runtime type (eg. CustomNodeEditorAttribute) </typeparam>
    /// <typeparam name="K">Runtime Type. The ScriptableObject this can be an editor for (eg. Node) </typeparam>
    public abstract class NodeEditorBase<T, A, K> where A : Attribute, NodeEditorBase<T, A, K>.INodeEditorAttrib
        where T : NodeEditorBase<T, A, K>
        where K : ScriptableObject
    {
        /// <summary> Custom editors defined with [CustomNodeEditor] </summary>
        private static Dictionary<Type, Type> _editorTypes;

        private static Dictionary<K, T> _editors = new Dictionary<K, T>();
        protected NodeEditorWindow Window;
        protected K Target;
        protected SerializedObject SerializedObject;

        public static T GetEditor(K target, NodeEditorWindow window)
        {
            if (target == null) return null;
            T editor;
            if (!_editors.TryGetValue(target, out editor))
            {
                var type = target.GetType();
                var editorType = GetEditorType(type);
                editor = Activator.CreateInstance(editorType) as T;
                editor.Target = target;
                editor.SerializedObject = new SerializedObject(target);
                editor.Window = window;
                editor.OnCreate();
                _editors.Add(target, editor);
            }

            if (editor.Target == null) editor.Target = target;
            if (editor.Window != window) editor.Window = window;
            if (editor.SerializedObject == null) editor.SerializedObject = new SerializedObject(target);
            return editor;
        }

        public static void DestroyEditor(K target)
        {
            if (target == null) return;
            T editor;
            if (_editors.TryGetValue(target, out editor))
            {
                _editors.Remove(target);
            }
        }

        private static Type GetEditorType(Type type)
        {
            if (type == null) return null;
            if (_editorTypes == null) CacheCustomEditors();
            Type result;
            if (_editorTypes.TryGetValue(type, out result)) return result;
            return GetEditorType(type.BaseType);
        }

        private static void CacheCustomEditors()
        {
            _editorTypes = new Dictionary<Type, Type>();

            var nodeEditors = typeof(T).GetDerivedTypes();
            for (var i = 0; i < nodeEditors.Length; i++)
            {
                if (nodeEditors[i].IsAbstract) continue;
                var attribs = nodeEditors[i].GetCustomAttributes(typeof(A), false);
                if (attribs == null || attribs.Length == 0) continue;
                var attrib = attribs[0] as A;
                _editorTypes.Add(attrib.GetInspectedType(), nodeEditors[i]);
            }
        }

        /// <summary> Called on creation, after references have been set </summary>
        public virtual void OnCreate()
        {
        }

        public interface INodeEditorAttrib
        {
            Type GetInspectedType();
        }
    }
}