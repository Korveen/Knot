# Contributing

## Formatting

* Indent with 4 spaces. Do not use tabs.
* Put the opening brace on the next line.
* Put `using` directives at the top of the file, outside the namespace.
* Use a block namespace (`namespace Knot { }`). Do not use a file-scoped namespace.
* Leave a file's line endings alone. Do not rewrite a file to change a few lines.

## Language

The package lists Unity 2021.3, which compiles C# 9. Stay inside that. A newer editor in one game does not raise this floor.

Use these when they shorten the code and the type stays obvious:

* `var`
* target-typed `new()`
* expression-bodied members for a single expression
* `switch` expressions and property patterns
* `nameof`

Do not use C# 10 or later: file-scoped namespaces, global usings, file-local types, raw string literals, required members, primary constructors, collection expressions.

Do not add `#nullable`. The assembly does not enable nullable reference types.

For `UnityEngine.Object`, compare with `== null` and `!= null`. The `is null` pattern does not use Unity's overloaded equality and treats a destroyed object as alive.

## Names

* Runtime types live in `Knot`, assembly `Knot`. Editor types live in `Knot.Editor`, assembly `Knot.Editor`.
* Public types and members are PascalCase. Private fields are `_camelCase`.
* A nested type and a member of the enclosing type cannot share a name.
* Node port fields stay public fields marked `[Input]` or `[Output]`. Other serialized state is `[SerializeField] private`.

## Serialization

* When another assembly looks up a serialized field, the owning type exposes `public const string NameField = nameof(_name);`. Do not pass that name as a string literal.
* Do not add `FormerlySerializedAs`. Renaming a serialized field means migrating the YAML.
* Do not rename a `Resources.Load` path or the file it loads unless the asset is renamed in the same change.
