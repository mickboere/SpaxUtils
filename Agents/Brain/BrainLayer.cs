using UnityEngine;

namespace SpaxUtils
{
    public class BrainLayer : AgentComponentBase
    {
        [SerializeField] private BrainGraph graph;

        private IDependencyManager dependencyManager;

        public void InjectDependencies(IDependencyManager dependencyManager)
        {
            this.dependencyManager = dependencyManager;
        }

        protected void OnEnable()
        {
            Agent.Brain.AppendGraph(graph, dependencyManager);
        }

        private void OnDisable()
        {
            Agent.Brain.StripGraph(graph);
        }
    }
}
