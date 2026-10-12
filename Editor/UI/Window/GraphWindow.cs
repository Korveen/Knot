using UnityEngine.UIElements;

namespace Knot.Editor.UI
{
    /// <summary>UITK shell for <see cref="NodeEditorWindow"/>. Loads UXML/USS and hosts <see cref="GraphUiController"/>.</summary>
    public sealed class GraphWindow : VisualElement
    {
        public GraphUiController Controller { get; }

        public GraphWindow(NodeEditorWindow window)
        {
            name = "graph-window";
            style.flexGrow = 1;
            pickingMode = PickingMode.Position;

            var uxml = UIAssets.LoadUxml("GraphWindow");
            var tokens = UIAssets.LoadUss("tokens");
            var windowSheet = UIAssets.LoadUss("GraphWindow");
            if (uxml != null) uxml.CloneTree(this);
            if (tokens != null) styleSheets.Add(tokens);
            if (windowSheet != null) styleSheets.Add(windowSheet);

            var root = this.Q("root") ?? this;
            root.style.flexGrow = 1;
            Controller = new GraphUiController(window, root);
        }
    }
}