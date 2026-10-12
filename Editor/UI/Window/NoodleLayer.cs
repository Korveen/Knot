using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using XNode;

namespace XNodeEditor.UI
{
    public sealed class NoodleLayer : VisualElement
    {
        public GraphUiController Controller;

        public NoodleLayer()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            if (Controller == null) return;
            Controller.PaintNoodles(ctx);
        }
    }
}