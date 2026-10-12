# Knot

Unity package for custom node graphs. Runtime stays small. The editor is UITK (`CreateGUI` to `GraphWindow`).

Use it as a base for state machines, dialogue, behaviour trees, or any graph you own. It is not a complete game framework.

Knot started as a fork of [Siccity/xNode](https://github.com/Siccity/xNode) by Thor Brigsted. Original xNode is MIT, copyright (c) 2017 Thor Brigsted. Do not install Siccity/xNode, OpenUPM `com.github.siccity.xnode`, or `com.github.siccity.xnodegroups`.

## What it contains

### Runtime

* `Node` / `NodeGraph` ScriptableObjects
* Ports from `[Input]` / `[Output]`
* `GetValue(NodePort)` and `GetValue(NodePort, GraphExecutionContext)`
* `BlackboardDefinition` on the graph, `BlackboardInstance` at run time
* Typed Blackboard variables (`BlackboardVariable<T>`) and `GetBlackboardVariableNode<T>`
* No third-party plugins required at runtime

### Editor

* UITK window: pan, zoom, grid snap, noodles, reroutes, search
* Preferences split: shared (grid, zoom) vs per graph type (noodles, port color slots)
* Port colors: named slots, then `[Node.GraphPortColor]` on a field or type
* Blackboard panel: add, reorder, rename, create a Get node. Extra `BlackboardVariable<T>` types are picked up by reflection. Adding, removing, or reordering a variable refreshes node dropdowns. A rename does the same shortly after the last keystroke.
* Custom node UI: `BuildHeader` / `BuildBody` / `BuildToolbar` / `BuildOverlay`. There is no IMGUI node-drawing path. Preferences and the create/context menus stay IMGUI.
* Groups: `Group` is at the top of the create menu. A node belongs to a group only when its full rect is inside the group. Dragging a group moves those nodes without selecting them. Delete removes the group only. Resize from any edge while the group is selected. `Select Contents` in the context menu selects the nodes inside.

## Requirements

* Unity 2021.3 or newer (`package.json`). Developed and used on Unity 6.
* Package id: `com.github.korveen.knot`

## Install

```json
"com.github.korveen.knot": "https://github.com/Korveen/Knot.git"
```

Assembly definition references: `Knot` and `Knot.Editor`.

## Node

```csharp
public class MathNode : Node {
    [Input] public float a;
    [Input] public float b;
    [Output] public float result;

    public enum MathType { Add, Subtract, Multiply, Divide }
    public MathType mathType = MathType.Add;

    public override object GetValue(NodePort port) {
        float a = GetInputValue<float>("a", this.a);
        float b = GetInputValue<float>("b", this.b);
        if (port.fieldName != "result") return 0f;
        switch (mathType) {
            case MathType.Subtract: return a - b;
            case MathType.Multiply: return a * b;
            case MathType.Divide: return a / b;
            default: return a + b;
        }
    }
}
```

With a run context (Blackboard / runner):

```csharp
public override object GetValue(NodePort port, GraphExecutionContext context) {
    return GetValue(port);
}
```

Port color:

```csharp
[Output, GraphPortColor("#4c8dff")] public float value;
```

Custom editor (UITK):

```csharp
[CustomNodeEditor(typeof(MathNode))]
public class MathNodeEditor : NodeEditor {
    public override void BuildBody(Knot.Editor.UI.NodeView view) {
        Knot.Editor.UI.NodeUiUtility.BindNodeFields(view, serializedObject);
    }
}
```

Blackboard type in your project (not in this package):

```csharp
[Serializable]
public sealed class ActorBlackboardVariable : BlackboardVariable<Actor> { }

public sealed class GetActorBlackboardVariableNode : GetBlackboardVariableNode<Actor> { }
```

`Actor` must be a Unity-serializable type.

## License

MIT. See [LICENSE.md](LICENSE.md). The copyright notice in that file stays.
