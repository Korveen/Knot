using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Knot.Editor.UI
{
    internal sealed class SearchPopup : VisualElement
    {
        public readonly struct Item
        {
            public readonly string Path;
            public readonly int Order;
            public readonly Action Select;

            public Item(string path, Action select, int order = 0)
            {
                Path = path ?? "";
                Order = order;
                Select = select;
            }
        }

        private sealed class Branch
        {
            public string Name;
            public int Order;
            public Action Select;
            public readonly List<Branch> Children = new List<Branch>();
        }

        private static SearchPopup _open;

        private readonly VisualElement _panel;
        private readonly TextField _search;
        private readonly ScrollView _list;
        private readonly List<Item> _items;
        private readonly IReadOnlyList<Item> _sticky;
        private readonly List<Action> _actions = new List<Action>();
        private readonly List<VisualElement> _rows = new List<VisualElement>();
        private int _index;
        private bool _placed;

        public static void Show(
            VisualElement host,
            Vector2 worldPosition,
            IReadOnlyList<Item> items,
            IReadOnlyList<Item> sticky = null,
            string title = "Create Node")
        {
            if (host == null) return;
            if ((items == null || items.Count == 0) && (sticky == null || sticky.Count == 0))
                return;
            Close();
            var popup = new SearchPopup(items, sticky, title);
            host.Add(popup);
            _open = popup;
            popup.Place(host, worldPosition);
            popup.schedule.Execute(() =>
            {
                popup._search.Focus();
                popup._search.SelectAll();
            });
        }

        public static void Close()
        {
            if (_open == null) return;
            _open.RemoveFromHierarchy();
            _open = null;
        }

        private SearchPopup(IReadOnlyList<Item> items, IReadOnlyList<Item> sticky, string title)
        {
            _items = Sorted(items);
            _sticky = sticky;
            AddToClassList("search-popup-catcher");
            pickingMode = PickingMode.Position;
            var tokens = UIAssets.LoadUss("tokens");
            if (tokens != null) styleSheets.Add(tokens);

            RegisterCallback<PointerDownEvent>(evt =>
            {
                Close();
                evt.StopPropagation();
            });
            RegisterCallback<KeyDownEvent>(OnKey, TrickleDown.TrickleDown);

            _panel = new VisualElement();
            _panel.AddToClassList("search-popup");
            _panel.pickingMode = PickingMode.Position;
            _panel.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            Add(_panel);

            var heading = new Label(title);
            heading.AddToClassList("search-popup-title");
            heading.pickingMode = PickingMode.Ignore;
            _panel.Add(heading);

            _search = new TextField {isDelayed = false};
            _search.AddToClassList("search-popup-field");
            if (_search.textEdition != null) _search.textEdition.placeholder = "Search";
            _search.RegisterValueChangedCallback(_ => RebuildList());
            _panel.Add(_search);

            _list = new ScrollView(ScrollViewMode.Vertical);
            _list.AddToClassList("search-popup-list");
            _list.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _panel.Add(_list);

            RebuildList();
        }

        private void Place(VisualElement host, Vector2 worldPosition)
        {
            var local = host.WorldToLocal(worldPosition);
            _panel.style.left = local.x;
            _panel.style.top = local.y;
            _panel.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                if (_placed) return;
                var hostBound = host.worldBound;
                var bound = _panel.worldBound;
                var dx = 0f;
                var dy = 0f;
                if (bound.xMax > hostBound.xMax) dx = hostBound.xMax - bound.xMax;
                if (bound.yMax > hostBound.yMax) dy = hostBound.yMax - bound.yMax;
                if (bound.x + dx < hostBound.xMin) dx = hostBound.xMin - bound.x;
                if (bound.y + dy < hostBound.yMin) dy = hostBound.yMin - bound.y;
                if (dx != 0f) _panel.style.left = _panel.resolvedStyle.left + dx;
                if (dy != 0f) _panel.style.top = _panel.resolvedStyle.top + dy;
                _placed = true;
            });
        }

        private void OnKey(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape)
            {
                Close();
                evt.StopPropagation();
                evt.PreventDefault();
                return;
            }

            if (evt.keyCode == KeyCode.DownArrow)
            {
                Move(1);
                evt.StopPropagation();
                evt.PreventDefault();
                return;
            }

            if (evt.keyCode == KeyCode.UpArrow)
            {
                Move(-1);
                evt.StopPropagation();
                evt.PreventDefault();
                return;
            }

            if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
            Confirm();
            evt.StopPropagation();
            evt.PreventDefault();
        }

        private void RebuildList()
        {
            _list.Clear();
            _actions.Clear();
            _rows.Clear();
            var query = _search.value != null ? _search.value.Trim() : "";
            var expand = query.Length > 0;
            var roots = new List<Branch>();
            var any = false;
            for (var i = 0; i < _items.Count; i++)
            {
                if (expand &&
                    _items[i].Path.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                Insert(roots, _items[i]);
                any = true;
            }

            Sort(roots);
            if (!any && (_sticky == null || _sticky.Count == 0))
            {
                var empty = new Label("No results");
                empty.AddToClassList("search-popup-empty");
                _list.Add(empty);
            }
            else
            {
                AddBranches(_list, roots, expand);
            }

            AddSticky();
            _index = FirstVisible();
            ApplyHighlight();
        }

        private void AddBranches(VisualElement parent, List<Branch> branches, bool expand)
        {
            for (var i = 0; i < branches.Count; i++)
            {
                var branch = branches[i];
                if (branch.Children.Count == 0)
                {
                    AddRow(parent, branch.Name, branch.Select);
                    continue;
                }

                var foldout = new Foldout {text = branch.Name, value = expand};
                foldout.AddToClassList("search-popup-foldout");
                foldout.RegisterValueChangedCallback(_ =>
                {
                    if (_index >= 0 && _index < _rows.Count && !IsDisplayed(_rows[_index]))
                        Move(1);
                });
                parent.Add(foldout);
                AddBranches(foldout, branch.Children, expand);
            }
        }

        private void AddSticky()
        {
            if (_sticky == null || _sticky.Count == 0) return;
            for (var i = 0; i < _sticky.Count; i++)
            {
                var row = AddRow(_list, _sticky[i].Path, _sticky[i].Select);
                row.AddToClassList("search-popup-sticky");
            }
        }

        private VisualElement AddRow(VisualElement parent, string label, Action select)
        {
            var row = new VisualElement();
            row.AddToClassList("search-popup-row");
            var text = new Label(label);
            text.AddToClassList("search-popup-row-label");
            text.pickingMode = PickingMode.Ignore;
            row.Add(text);
            var index = _rows.Count;
            row.RegisterCallback<PointerEnterEvent>(_ =>
            {
                _index = index;
                ApplyHighlight();
            });
            row.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                _index = index;
                Confirm();
                evt.StopPropagation();
            });
            parent.Add(row);
            _rows.Add(row);
            _actions.Add(select);
            return row;
        }

        private void Move(int delta)
        {
            if (_rows.Count == 0) return;
            var start = _index < 0 ? 0 : _index;
            var next = start;
            for (var i = 0; i < _rows.Count; i++)
            {
                next = (next + delta + _rows.Count) % _rows.Count;
                if (IsDisplayed(_rows[next]))
                {
                    _index = next;
                    ApplyHighlight();
                    _list.ScrollTo(_rows[_index]);
                    return;
                }
            }
        }

        private int FirstVisible()
        {
            for (var i = 0; i < _rows.Count; i++)
            {
                if (IsDisplayed(_rows[i])) return i;
            }

            return _rows.Count > 0 ? 0 : -1;
        }

        private void ApplyHighlight()
        {
            for (var i = 0; i < _rows.Count; i++)
                _rows[i].EnableInClassList("active", i == _index);
        }

        private void Confirm()
        {
            if (_index < 0 || _index >= _actions.Count) return;
            if (!IsDisplayed(_rows[_index])) return;
            var action = _actions[_index];
            Close();
            action?.Invoke();
        }

        private static bool IsDisplayed(VisualElement element)
        {
            for (var current = element; current != null; current = current.parent)
            {
                if (current.resolvedStyle.display == DisplayStyle.None) return false;
            }

            return element != null;
        }

        private static void Insert(List<Branch> roots, Item item)
        {
            var parts = item.Path.Split('/');
            var siblings = roots;
            for (var i = 0; i < parts.Length; i++)
            {
                var name = parts[i];
                if (name.Length == 0) continue;
                var last = i == parts.Length - 1;
                var found = Find(siblings, name, last);
                if (found == null)
                {
                    found = new Branch
                    {
                        Name = name,
                        Order = item.Order,
                        Select = last ? item.Select : null
                    };
                    siblings.Add(found);
                }
                else if (item.Order < found.Order)
                {
                    found.Order = item.Order;
                }

                if (last) return;
                siblings = found.Children;
            }
        }

        private static Branch Find(List<Branch> siblings, string name, bool leaf)
        {
            for (var i = 0; i < siblings.Count; i++)
            {
                var branch = siblings[i];
                if (!string.Equals(branch.Name, name, StringComparison.Ordinal)) continue;
                if (leaf) return branch.Select != null ? branch : null;
                if (branch.Select == null) return branch;
            }

            return null;
        }

        private static void Sort(List<Branch> branches)
        {
            branches.Sort((a, b) =>
            {
                var order = a.Order.CompareTo(b.Order);
                return order != 0
                    ? order
                    : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            for (var i = 0; i < branches.Count; i++) Sort(branches[i].Children);
        }

        private static List<Item> Sorted(IReadOnlyList<Item> items)
        {
            var list = new List<Item>(items != null ? items.Count : 0);
            if (items == null) return list;
            for (var i = 0; i < items.Count; i++) list.Add(items[i]);
            list.Sort((a, b) =>
            {
                var order = a.Order.CompareTo(b.Order);
                return order != 0
                    ? order
                    : string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase);
            });
            return list;
        }
    }
}
