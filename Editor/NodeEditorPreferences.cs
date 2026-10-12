using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace XNodeEditor
{
    public enum NoodlePath
    {
        Curvy,
        Straight,
        Angled,
        ShaderLab
    }

    public enum NoodleStroke
    {
        Full,
        Dashed
    }

    public static class NodeEditorPreferences
    {
        public const string SharedKey = "xNode.Settings";
        public const string BehaviourTreeKey = "xNode.Settings.BehaviourTree";
        public const string FlowKey = "xNode.Settings.Flow";

        public static class Slots
        {
            public const string Exec = "exec";
            public const string Bool = "bool";
            public const string Float = "float";
            public const string Int = "int";
            public const string String = "string";
            public const string Data = "data";
            public const string HeaderStart = "header.start";
            public const string HeaderComposite = "header.composite";
            public const string HeaderDecorator = "header.decorator";
            public const string HeaderAction = "header.action";
            public const string HeaderCondition = "header.condition";
            public const string HeaderEntry = "header.entry";
            public const string HeaderControl = "header.control";
            public const string HeaderDialog = "header.dialog";
            public const string HeaderCommand = "header.command";
            public const string HeaderQuery = "header.query";
            public const string HeaderTerminal = "header.terminal";
        }

        public static event Action Changed;

        static SharedSettings _shared;
        static Texture2D _gridTexture;
        static Texture2D _crossTexture;

        static readonly Dictionary<string, GraphTypeSettings> GraphSettings =
            new Dictionary<string, GraphTypeSettings>();

        static readonly Dictionary<Type, GraphTypeSettings> DefaultCache = new Dictionary<Type, GraphTypeSettings>();

        [Serializable]
        public class SharedSettings
        {
            public bool GridSnap = true;
            public bool ShowNodeIndices = false;
            public bool AutoSave = true;
            public bool OpenOnCreate = true;
            public bool AllowMultipleWindows = false;
            public bool DragToCreate = true;
            public bool CreateFilter = true;
            public bool ZoomToMouse = true;
            public bool PortTooltips = true;
            public float MaxZoom = 5f;
            public float MinZoom = 1f;
            public float CompactNodeZoom = 1.5f;
            public Color32 TintColor = new Color32(90, 97, 105, 255);
            public Color32 SelectionColor = new Color32(240, 192, 64, 255);
            public Color32 GridBgColor = new Color32(48, 48, 48, 255);
            public Color32 GridMinorColor = new Color32(255, 255, 255, 5);
            public Color32 GridMajorColor = new Color32(255, 255, 255, 13);
            public Color32 GridOriginColor = new Color32(255, 255, 255, 26);
            public float GridMinorStep = 20f;
            public float GridMajorStep = 100f;
        }

        [Serializable]
        public class GraphTypeSettings : ISerializationCallbackReceiver
        {
            public NoodlePath NoodlePath = NoodlePath.Curvy;
            public float NoodleThickness = 2f;
            public float NoodleTension = 0.45f;
            public NoodleStroke NoodleStroke = NoodleStroke.Full;
            [SerializeField] string _slotColorsData = "";
            [NonSerialized] public Dictionary<string, Color> Slots = new Dictionary<string, Color>();

            public Color GetSlot(string key, Color fallback)
            {
                if (Slots != null && Slots.TryGetValue(key, out var color)) return color;
                return fallback;
            }

            public void SetSlot(string key, Color color)
            {
                if (Slots == null) Slots = new Dictionary<string, Color>();
                Slots[key] = color;
            }

            public void MergeMissingFrom(GraphTypeSettings defaults)
            {
                if (defaults?.Slots == null) return;
                if (Slots == null) Slots = new Dictionary<string, Color>();
                foreach (var pair in defaults.Slots)
                {
                    if (!Slots.ContainsKey(pair.Key)) Slots.Add(pair.Key, pair.Value);
                }
            }

            public void OnAfterDeserialize()
            {
                Slots = new Dictionary<string, Color>();
                if (string.IsNullOrEmpty(_slotColorsData)) return;
                var data = _slotColorsData.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries);
                for (var i = 0; i + 1 < data.Length; i += 2)
                {
                    if (ColorUtility.TryParseHtmlString("#" + data[i + 1], out var col))
                        Slots[data[i]] = col;
                }
            }

            public void OnBeforeSerialize()
            {
                _slotColorsData = "";
                if (Slots == null) return;
                foreach (var pair in Slots)
                    _slotColorsData += pair.Key + "," + ColorUtility.ToHtmlStringRGBA(pair.Value) + ",";
            }
        }

        [Serializable]
        class LegacySettings
        {
            public bool GridSnap = true;
            public bool ShowNodeIndices;
            public bool AutoSave = true;
            public bool OpenOnCreate = true;
            public bool DragToCreate = true;
            public bool CreateFilter = true;
            public bool ZoomToMouse = true;
            public bool PortTooltips = true;
            public float MaxZoom = 5f;
            public float MinZoom = 1f;
            public float CompactNodeZoom = 1.5f;
            public Color32 TintColor = new Color32(90, 97, 105, 255);
        }

        public static SharedSettings GetShared()
        {
            if (_shared == null) _shared = LoadShared();
            SanitizeShared(_shared);
            return _shared;
        }

        public static GraphTypeSettings GetFor(NodeGraphEditor editor)
        {
            return GetForKey(PrefsKey(editor), editor != null ? editor.GetType() : null);
        }

        public static GraphTypeSettings GetForKey(string key, Type editorType = null)
        {
            if (string.IsNullOrEmpty(key)) key = SharedKey;
            if (editorType == null) editorType = FindEditorType(key);
            if (GraphSettings.TryGetValue(key, out var cached))
            {
                if (editorType != null)
                    cached.MergeMissingFrom(CreateDefaults(editorType));
                return cached;
            }

            var loaded = LoadGraph(key, editorType);
            GraphSettings[key] = loaded;
            return loaded;
        }

        public static SharedSettings GetSettings() => GetShared();

        public static Color GetTypeColor(Type type)
        {
            return GetSlotColor(GetFor(NodeEditorWindow.Current?.GraphEditor), type);
        }

        public static string DataSlotFor(Type type)
        {
            if (type == typeof(bool)) return Slots.Bool;
            if (type == typeof(float) || type == typeof(double)) return Slots.Float;
            if (type == typeof(int) || type == typeof(long)) return Slots.Int;
            if (type == typeof(string)) return Slots.String;
            return Slots.Data;
        }

        public static Color ExecFallback => new Color(0.92f, 0.78f, 0.28f, 1f);
        public static Color BoolFallback => new Color(0.40f, 0.82f, 0.45f, 1f);
        public static Color FloatFallback => new Color(0.30f, 0.75f, 0.85f, 1f);
        public static Color IntFallback => new Color(0.95f, 0.55f, 0.25f, 1f);
        public static Color StringFallback => new Color(0.85f, 0.45f, 0.75f, 1f);
        public static Color DataFallback => new Color(0.30f, 0.55f, 0.90f, 1f);

        public static void ApplyDefaultDataSlots(GraphTypeSettings settings)
        {
            if (settings == null) return;
            settings.SetSlot(Slots.Exec, ExecFallback);
            settings.SetSlot(Slots.Bool, BoolFallback);
            settings.SetSlot(Slots.Float, FloatFallback);
            settings.SetSlot(Slots.Int, IntFallback);
            settings.SetSlot(Slots.String, StringFallback);
            settings.SetSlot(Slots.Data, DataFallback);
        }

        public static Color GetSlotColor(GraphTypeSettings settings, Type type)
        {
            var slot = DataSlotFor(type);
            var fallback = slot == Slots.Bool ? BoolFallback
                : slot == Slots.Float ? FloatFallback
                : slot == Slots.Int ? IntFallback
                : slot == Slots.String ? StringFallback
                : DataFallback;
            return settings != null ? settings.GetSlot(slot, fallback) : fallback;
        }

        public static Color ResolvePortColor(XNode.NodePort port, GraphTypeSettings settings, bool execution)
        {
            if (NodeEditorUtilities.TryGetGraphPortColor(port, out var attributed)) return attributed;
            if (execution)
                return settings != null ? settings.GetSlot(Slots.Exec, ExecFallback) : ExecFallback;
            return GetSlotColor(settings, port != null ? port.ValueType : null);
        }

        public static Texture2D GetGridTexture()
        {
            if (_gridTexture == null)
            {
                var shared = GetShared();
                _gridTexture = NodeEditorResources.GenerateGridTexture(shared.GridMinorColor, shared.GridBgColor);
            }

            return _gridTexture;
        }

        public static Texture2D GetCrossTexture()
        {
            if (_crossTexture == null)
                _crossTexture = NodeEditorResources.GenerateCrossTexture(GetShared().GridMajorColor);
            return _crossTexture;
        }

        public static void SetGridSnap(bool enabled)
        {
            GetShared().GridSnap = enabled;
            SaveShared();
            NotifyChanged();
        }

        public static void SetShowNodeIndices(bool enabled)
        {
            GetShared().ShowNodeIndices = enabled;
            SaveShared();
            NotifyChanged();
        }

        public static void SaveShared()
        {
            EditorPrefs.SetString(SharedKey + ".shared", JsonUtility.ToJson(GetShared()));
        }

        public static void SaveGraph(string key, GraphTypeSettings settings)
        {
            if (settings == null || string.IsNullOrEmpty(key)) return;
            settings.OnBeforeSerialize();
            GraphSettings[key] = settings;
            EditorPrefs.SetString(key, JsonUtility.ToJson(settings));
        }

        public static void ResetShared()
        {
            EditorPrefs.DeleteKey(SharedKey + ".shared");
            _shared = new SharedSettings();
            NotifyChanged();
        }

        public static void ResetGraph(string key, Type editorType)
        {
            if (!string.IsNullOrEmpty(key) && EditorPrefs.HasKey(key)) EditorPrefs.DeleteKey(key);
            GraphSettings.Remove(key);
            GetForKey(key, editorType);
            NotifyChanged();
        }

        static void NotifyChanged()
        {
            DestroyTexture(ref _gridTexture);
            DestroyTexture(ref _crossTexture);
            NodeEditorWindow.RepaintAll();
            Changed?.Invoke();
        }

        static void DestroyTexture(ref Texture2D texture)
        {
            if (texture == null) return;
            UnityEngine.Object.DestroyImmediate(texture);
            texture = null;
        }

        static void SanitizeShared(SharedSettings shared)
        {
            if (shared == null) return;
            if (shared.GridMinorStep < 1f) shared.GridMinorStep = 20f;
            if (shared.GridMajorStep < 1f) shared.GridMajorStep = 100f;
            shared.GridMajorStep = Mathf.Max(shared.GridMinorStep, shared.GridMajorStep);
        }

        static SharedSettings LoadShared()
        {
            var jsonKey = SharedKey + ".shared";
            if (EditorPrefs.HasKey(jsonKey))
                return JsonUtility.FromJson<SharedSettings>(EditorPrefs.GetString(jsonKey)) ?? new SharedSettings();
            if (EditorPrefs.HasKey(SharedKey))
            {
                var legacy = JsonUtility.FromJson<LegacySettings>(EditorPrefs.GetString(SharedKey));
                if (legacy != null)
                {
                    return new SharedSettings
                    {
                        GridSnap = legacy.GridSnap,
                        ShowNodeIndices = legacy.ShowNodeIndices,
                        AutoSave = legacy.AutoSave,
                        OpenOnCreate = legacy.OpenOnCreate,
                        DragToCreate = legacy.DragToCreate,
                        CreateFilter = legacy.CreateFilter,
                        ZoomToMouse = legacy.ZoomToMouse,
                        PortTooltips = legacy.PortTooltips,
                        MaxZoom = legacy.MaxZoom,
                        MinZoom = legacy.MinZoom,
                        CompactNodeZoom = legacy.CompactNodeZoom,
                        TintColor = legacy.TintColor
                    };
                }
            }

            return new SharedSettings();
        }

        static GraphTypeSettings LoadGraph(string key, Type editorType)
        {
            var defaults = CreateDefaults(editorType);
            if (string.IsNullOrEmpty(key) || !EditorPrefs.HasKey(key)) return defaults;
            var loaded = JsonUtility.FromJson<GraphTypeSettings>(EditorPrefs.GetString(key));
            if (loaded == null) return defaults;
            loaded.OnAfterDeserialize();
            loaded.MergeMissingFrom(defaults);
            if (loaded.NoodleThickness <= 0f) loaded.NoodleThickness = defaults.NoodleThickness;
            if (loaded.NoodleTension <= 0.01f)
                loaded.NoodleTension = defaults.NoodleTension > 0.01f ? defaults.NoodleTension : 0.45f;
            return loaded;
        }

        static GraphTypeSettings CreateDefaults(Type editorType)
        {
            if (editorType == null) return new GraphTypeSettings();
            if (DefaultCache.TryGetValue(editorType, out var cached)) return cached;
            var created = new GraphTypeSettings();
            try
            {
                var editor = Activator.CreateInstance(editorType) as NodeGraphEditor;
                if (editor != null)
                    created = editor.GetDefaultPreferences() ?? new GraphTypeSettings();
            }
            catch
            {
                // ignored
            }

            DefaultCache[editorType] = created;
            return created;
        }

        public static string PrefsKey(NodeGraphEditor editor)
        {
            if (editor == null) return SharedKey;
            var attrib = editor.GetType().GetCustomAttribute<NodeGraphEditor.CustomNodeGraphEditorAttribute>(true);
            return attrib != null ? attrib.EditorPrefsKey : SharedKey;
        }

        static Type FindEditorType(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (var editorType in
                     TypeCache.GetTypesWithAttribute<NodeGraphEditor.CustomNodeGraphEditorAttribute>())
            {
                if (editorType.IsAbstract || !typeof(NodeGraphEditor).IsAssignableFrom(editorType)) continue;
                var attrib = editorType.GetCustomAttribute<NodeGraphEditor.CustomNodeGraphEditorAttribute>(true);
                if (attrib != null && attrib.EditorPrefsKey == key) return editorType;
            }

            return null;
        }

        [SettingsProvider]
        static SettingsProvider CreateXNodeProvider()
        {
            return new SettingsProvider("Preferences/XNode", SettingsScope.User)
            {
                label = "XNode",
                keywords = new HashSet<string> {"xnode", "node", "graph", "grid", "snap", "window"},
                guiHandler = _ => DrawSharedGUI()
            };
        }

        [SettingsProvider]
        static SettingsProvider CreateBehaviourTreeProvider()
        {
            return new SettingsProvider("Preferences/XNode/Behaviour Tree", SettingsScope.User)
            {
                label = "Behaviour Tree",
                keywords = new HashSet<string> {"xnode", "behaviour", "tree", "noodle", "port"},
                guiHandler = _ => DrawGraphTypeGUI(BehaviourTreeKey, "Behaviour Tree")
            };
        }

        [SettingsProvider]
        static SettingsProvider CreateFlowGraphProvider()
        {
            return new SettingsProvider("Preferences/XNode/Flow Graph", SettingsScope.User)
            {
                label = "Flow Graph",
                keywords = new HashSet<string> {"xnode", "flow", "noodle", "port"},
                guiHandler = _ => DrawGraphTypeGUI(FlowKey, "Flow Graph")
            };
        }

        static void DrawSharedGUI()
        {
            var shared = GetShared();
            EditorGUI.BeginChangeCheck();
            DrawSection("Grid", () =>
            {
                shared.GridSnap = EditorGUILayout.Toggle(new GUIContent("Snap", "Hold Ctrl while dragging to invert"),
                    shared.GridSnap);
                shared.ShowNodeIndices = EditorGUILayout.Toggle(
                    new GUIContent("Node Indices", "Show compiled plan indices on node headers"),
                    shared.ShowNodeIndices);
                shared.GridMinorStep = EditorGUILayout.FloatField(
                    new GUIContent("Snap / Minor", "Node snap and minor grid lines"),
                    shared.GridMinorStep);
                shared.GridMajorStep = EditorGUILayout.FloatField("Major lines", shared.GridMajorStep);
                shared.GridBgColor = EditorGUILayout.ColorField("Background", shared.GridBgColor);
                shared.GridMinorColor = EditorGUILayout.ColorField("Minor lines", shared.GridMinorColor);
                shared.GridMajorColor = EditorGUILayout.ColorField("Major lines", shared.GridMajorColor);
                shared.GridOriginColor = EditorGUILayout.ColorField("Origin", shared.GridOriginColor);
            });
            DrawSection("View", () =>
            {
                shared.SelectionColor = EditorGUILayout.ColorField("Selection", shared.SelectionColor);
                shared.ZoomToMouse = EditorGUILayout.Toggle(
                    new GUIContent("Zoom to Mouse", "Keep the point under the cursor fixed while scrolling"),
                    shared.ZoomToMouse);
                EditorGUILayout.LabelField("Zoom", EditorStyles.miniBoldLabel);
                EditorGUI.indentLevel++;
                shared.MinZoom = EditorGUILayout.FloatField("Min", shared.MinZoom);
                shared.MaxZoom = EditorGUILayout.FloatField("Max", shared.MaxZoom);
                shared.CompactNodeZoom = EditorGUILayout.FloatField(
                    new GUIContent("Hide Fields At",
                        "When Scale is at least this value, node fields are not drawn. 0 disables."),
                    shared.CompactNodeZoom);
                EditorGUI.indentLevel--;
            });
            DrawSection("Editor", () =>
            {
                shared.OpenOnCreate = EditorGUILayout.Toggle("Open Editor on Create", shared.OpenOnCreate);
                shared.AllowMultipleWindows = EditorGUILayout.Toggle(
                    new GUIContent(
                        "Multiple Graphs",
                        "On: each graph opens in its own window. Off: opening a graph replaces the one in the current window."),
                    shared.AllowMultipleWindows);
                shared.DragToCreate = EditorGUILayout.Toggle(
                    new GUIContent("Drag to Create", "Drop an unconnected output on empty grid to create a node"),
                    shared.DragToCreate);
                shared.CreateFilter = EditorGUILayout.Toggle(
                    new GUIContent("Create Filter", "Only show nodes compatible with the dragged port"),
                    shared.CreateFilter);
                shared.PortTooltips = EditorGUILayout.Toggle("Port Tooltips", shared.PortTooltips);
            });
            if (EditorGUI.EndChangeCheck())
            {
                shared.MinZoom = Mathf.Max(0.1f, shared.MinZoom);
                shared.MaxZoom = Mathf.Max(shared.MinZoom + 0.1f, shared.MaxZoom);
                SanitizeShared(shared);
                SaveShared();
                NotifyChanged();
            }

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Reset XNode Defaults", GUILayout.Width(180)))
                ResetShared();
        }

        static void DrawGraphTypeGUI(string key, string label)
        {
            var editorType = FindEditorType(key);
            var settings = GetForKey(key, editorType);
            EditorGUILayout.HelpBox("Stored on this machine, not in the project.", MessageType.None);
            EditorGUI.BeginChangeCheck();
            DrawSection("Connections", () =>
            {
                var path = settings.NoodlePath == NoodlePath.Straight ? 1
                    : settings.NoodlePath == NoodlePath.Angled ? 2 : 0;
                path = EditorGUILayout.Popup("Path", path, new[] {"Curvy", "Straight", "Angled"});
                settings.NoodlePath = path == 1 ? NoodlePath.Straight
                    : path == 2 ? NoodlePath.Angled : NoodlePath.Curvy;
                settings.NoodleThickness = EditorGUILayout.Slider("Thickness", settings.NoodleThickness, 1f, 8f);
                settings.NoodleTension = EditorGUILayout.Slider(
                    new GUIContent("Bezier", "How far Curvy wires pull away from a straight line"),
                    settings.NoodleTension, 0.05f, 1f);
                settings.NoodleStroke = (NoodleStroke) EditorGUILayout.EnumPopup("Stroke", settings.NoodleStroke);
            });
            DrawSection("Ports", () =>
            {
                DrawSlot(settings, Slots.Exec, "Execution");
                DrawSlotIfPresent(settings, Slots.Bool, "Bool");
                DrawSlotIfPresent(settings, Slots.Float, "Float");
                DrawSlotIfPresent(settings, Slots.Int, "Int");
                DrawSlotIfPresent(settings, Slots.String, "String");
                DrawSlotIfPresent(settings, Slots.Data, "Other data");
                EditorGUILayout.HelpBox(
                    "Custom types use Other data unless marked [Node.GraphPortColor]. Field attribute overrides the type.",
                    MessageType.None);
            });
            DrawSection("Headers", () =>
            {
                DrawSlotIfPresent(settings, Slots.HeaderStart, "Start");
                DrawSlotIfPresent(settings, Slots.HeaderComposite, "Composite");
                DrawSlotIfPresent(settings, Slots.HeaderDecorator, "Decorator");
                DrawSlotIfPresent(settings, Slots.HeaderAction, "Action");
                DrawSlotIfPresent(settings, Slots.HeaderCondition, "Condition");
                DrawSlotIfPresent(settings, Slots.HeaderEntry, "Entry");
                DrawSlotIfPresent(settings, Slots.HeaderControl, "Control");
                DrawSlotIfPresent(settings, Slots.HeaderDialog, "Dialog");
                DrawSlotIfPresent(settings, Slots.HeaderCommand, "Command");
                DrawSlotIfPresent(settings, Slots.HeaderQuery, "Query");
                DrawSlotIfPresent(settings, Slots.HeaderTerminal, "Terminal");
            });
            if (EditorGUI.EndChangeCheck())
            {
                SaveGraph(key, settings);
                NotifyChanged();
            }

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Reset " + label + " Defaults", GUILayout.Width(220)))
                ResetGraph(key, editorType);
        }

        static void DrawSlotIfPresent(GraphTypeSettings settings, string key, string label)
        {
            if (settings.Slots != null && settings.Slots.ContainsKey(key))
                DrawSlot(settings, key, label);
        }

        static void DrawSlot(GraphTypeSettings settings, string key, string label)
        {
            var current = settings.GetSlot(key, Color.gray);
            var next = EditorGUILayout.ColorField(label, current);
            if (next != current) settings.SetSlot(key, next);
        }

        static void DrawSection(string title, Action body)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            body();
            EditorGUILayout.EndVertical();
        }
    }
}