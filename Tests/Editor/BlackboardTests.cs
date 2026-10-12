using NUnit.Framework;
using UnityEngine;

namespace Knot.Tests
{
    public sealed class BlackboardTests
    {
        private sealed class TestGraph : NodeGraph
        {
        }

        private TestGraph _graph;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<TestGraph>();
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = _graph.Nodes.Count - 1; i >= 0; i--)
            {
                if (_graph.Nodes[i] != null)
                {
                    Object.DestroyImmediate(_graph.Nodes[i]);
                }
            }

            Object.DestroyImmediate(_graph);
        }

        [Test]
        public void RuntimeInstancesAreIsolated()
        {
            var variable = _graph.BlackboardDefinition.Add<FloatVariable>("Speed");
            variable.DefaultValue = 3f;

            var first = _graph.BlackboardDefinition.CreateInstance();
            var second = _graph.BlackboardDefinition.CreateInstance();
            first.Set(variable.Id, 9f);

            Assert.That(first.Get<float>(variable.Id), Is.EqualTo(9f));
            Assert.That(second.Get<float>(variable.Id), Is.EqualTo(3f));
        }

        [Test]
        public void RenameKeepsStableReference()
        {
            var variable = _graph.BlackboardDefinition.Add<IntVariable>("Count");
            var id = variable.Id;

            Assert.That(_graph.BlackboardDefinition.Rename(id, "Total"), Is.True);
            Assert.That(variable.Id, Is.EqualTo(id));
            Assert.That(variable.Name, Is.EqualTo("Total"));
        }

        [Test]
        public void DuplicateNamesAreRejectedCaseInsensitively()
        {
            var first = _graph.BlackboardDefinition.Add<IntVariable>("Count");
            var second = _graph.BlackboardDefinition.Add<IntVariable>("Other");

            Assert.That(_graph.BlackboardDefinition.Rename(second.Id, first.Name.ToLowerInvariant()), Is.False);
        }

        [Test]
        public void GetterReadsExecutionContext()
        {
            var variable = _graph.BlackboardDefinition.Add<FloatVariable>("Speed");
            variable.DefaultValue = 2f;
            var getter = _graph.AddNode<GetFloat>();
            getter.Variable = new BlackboardVariableReference<float>(variable.Id);

            var context = new GraphExecutionContext(_graph);
            context.Blackboard.Set(variable.Id, 7f);

            var port = getter.GetOutputPort(nameof(GetFloat.Value));
            Assert.That(getter.GetValue(port, context), Is.EqualTo(7f));
        }

        [Test]
        public void OverrideSetAppliesEnabledValuesOnly()
        {
            var count = _graph.BlackboardDefinition.Add<IntVariable>("Count");
            count.DefaultValue = 1;
            var speed = _graph.BlackboardDefinition.Add<FloatVariable>("Speed");
            speed.DefaultValue = 2f;

            var overrides = new BlackboardOverrideSet();
            overrides.Set(count, 9);
            overrides.Enable(speed);
            overrides.Disable(speed.Id);

            var instance = _graph.BlackboardDefinition.CreateInstance();
            overrides.Apply(instance);

            Assert.That(instance.Get<int>(count.Id), Is.EqualTo(9));
            Assert.That(instance.Get<float>(speed.Id), Is.EqualTo(2f));
        }

        [Test]
        public void TrySetByNameRejectsWrongType()
        {
            _graph.BlackboardDefinition.Add<IntVariable>("Count");
            var instance = _graph.BlackboardDefinition.CreateInstance();

            Assert.That(instance.TrySetByName("Count", 4), Is.True);
            Assert.That(instance.TrySetByName("Count", 1.5f), Is.False);
            Assert.That(instance.GetByName<int>("Count"), Is.EqualTo(4));
        }

        [Test]
        public void CopiedNodeReceivesNewStableId()
        {
            var original = _graph.AddNode<GetFloat>();
            var copy = (GetFloat) _graph.CopyNode(original);

            Assert.That(copy.NodeId, Is.Not.EqualTo(original.NodeId));
        }
    }
}