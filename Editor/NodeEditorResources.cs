using UnityEditor;
using UnityEngine;

namespace Knot.Editor
{
    public static class NodeEditorResources
    {
        // Textures
        public static Texture2D Dot => _dot != null ? _dot : _dot = Resources.Load<Texture2D>("xnode_dot");
        private static Texture2D _dot;

        public static Texture2D DotOuter => _dotOuter != null ? _dotOuter : _dotOuter = Resources.Load<Texture2D>("xnode_dot_outer");
        private static Texture2D _dotOuter;

        public static Texture2D NodeBody => _nodeBody != null ? _nodeBody : _nodeBody = Resources.Load<Texture2D>("xnode_node");
        private static Texture2D _nodeBody;

        public static Texture2D NodeHighlight => _nodeHighlight != null
            ? _nodeHighlight
            : _nodeHighlight = Resources.Load<Texture2D>("xnode_node_highlight");

        private static Texture2D _nodeHighlight;

        public static GuiStyles Styles => _styles != null ? _styles : _styles = new GuiStyles();
        private static GuiStyles _styles;
        public static GUIStyle OutputPort => new(EditorStyles.label) {alignment = TextAnchor.UpperRight};

        public class GuiStyles
        {
            public readonly GUIStyle InputPort, OutputPort, NodeHeader, NodeBody, Tooltip, NodeHighlight;

            public GuiStyles()
            {
                var baseStyle = new GUIStyle("Label");
                baseStyle.fixedHeight = 18;

                InputPort = new GUIStyle(baseStyle);
                InputPort.alignment = TextAnchor.UpperLeft;
                InputPort.padding.left = 0;
                InputPort.active.background = Dot;
                InputPort.normal.background = DotOuter;

                OutputPort = new GUIStyle(baseStyle);
                OutputPort.alignment = TextAnchor.UpperRight;
                OutputPort.padding.right = 0;
                OutputPort.active.background = Dot;
                OutputPort.normal.background = DotOuter;

                NodeHeader = new GUIStyle();
                NodeHeader.alignment = TextAnchor.MiddleCenter;
                NodeHeader.fontStyle = FontStyle.Bold;
                NodeHeader.normal.textColor = Color.white;

                NodeBody = new GUIStyle();
                NodeBody.normal.background = NodeEditorResources.NodeBody;
                NodeBody.border = new RectOffset(32, 32, 32, 32);
                NodeBody.padding = new RectOffset(16, 16, 4, 16);

                NodeHighlight = new GUIStyle();
                NodeHighlight.normal.background = NodeEditorResources.NodeHighlight;
                NodeHighlight.border = new RectOffset(32, 32, 32, 32);

                Tooltip = new GUIStyle("helpBox");
                Tooltip.alignment = TextAnchor.MiddleCenter;
            }

            public GuiStyles(GUIStyle inputPort)
            {
                InputPort = inputPort;
            }
        }

        public static Texture2D GenerateGridTexture(Color line, Color bg)
        {
            var tex = new Texture2D(64, 64);
            var cols = new Color[64 * 64];
            for (var y = 0; y < 64; y++)
            {
                for (var x = 0; x < 64; x++)
                {
                    var col = bg;
                    if (y % 16 == 0 || x % 16 == 0) col = Color.Lerp(line, bg, 0.65f);
                    if (y == 63 || x == 63) col = Color.Lerp(line, bg, 0.35f);
                    cols[(y * 64) + x] = col;
                }
            }

            tex.SetPixels(cols);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.name = "Grid";
            tex.Apply();
            return tex;
        }

        public static Texture2D GenerateCrossTexture(Color line)
        {
            var tex = new Texture2D(64, 64);
            var cols = new Color[64 * 64];
            for (var y = 0; y < 64; y++)
            {
                for (var x = 0; x < 64; x++)
                {
                    var col = line;
                    if (y != 31 && x != 31) col.a = 0;
                    cols[(y * 64) + x] = col;
                }
            }

            tex.SetPixels(cols);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.name = "Grid";
            tex.Apply();
            return tex;
        }
    }
}