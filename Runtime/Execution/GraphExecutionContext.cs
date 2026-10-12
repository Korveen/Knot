using System;
using System.Threading;

namespace Knot
{
    /// <summary>Resolves runtime services without coupling Knot to a DI container.</summary>
    public interface IExecutionServiceResolver
    {
        object GetService(Type serviceType);
    }

    /// <summary>Runtime data shared by one execution of a graph definition.</summary>
    public class GraphExecutionContext
    {
        public NodeGraph Graph { get; }
        public BlackboardInstance Blackboard { get; }
        public IExecutionServiceResolver Services { get; }
        public CancellationToken CancellationToken { get; }

        public GraphExecutionContext(
            NodeGraph graph,
            BlackboardInstance blackboard = null,
            IExecutionServiceResolver services = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Graph = graph ? graph : throw new ArgumentNullException(nameof(graph));
            Blackboard = blackboard ?? graph.BlackboardDefinition.CreateInstance();
            Services = services;
            CancellationToken = cancellationToken;
        }

        public T GetRequiredService<T>()
        {
            if (Services == null)
            {
                throw new InvalidOperationException("This graph execution has no service resolver.");
            }

            var service = Services.GetService(typeof(T));
            if (service is T typedService) return typedService;

            throw new InvalidOperationException(
                $"Service resolver did not return a service assignable to {typeof(T).FullName}.");
        }

        public bool TryGetService<T>(out T service)
        {
            service = default(T);
            if (Services == null) return false;

            var resolved = Services.GetService(typeof(T));
            if (!(resolved is T typedService)) return false;

            service = typedService;
            return true;
        }
    }
}