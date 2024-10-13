using System.Collections.Generic;

namespace SpaxUtils.StateMachines
{
	public static class StateMachineExtensions
	{
		/// <summary>
		/// Given <paramref name="state"/>, will collect the entire active hierarchy when entered.
		/// Highest parent = [0], deepest child is [x].
		/// </summary>
		/// <param name="state">The state for which to collect the active hierarchy, parents and default-childs included.</param>
		/// <param name="list">The insofar recursively collected states.</param>
		/// <returns></returns>
		public static List<IState> CollectActiveHierarchyRecursively(this IState state, List<IState> list = null)
		{
			if (list == null)
			{
				list = new List<IState>();
				list.Add(state);
				list.AddRange(CollectHeadRecursively(state));
			}
			else
			{
				list.Insert(0, state);
			}

			if (state.Parent != null)
			{
				return CollectActiveHierarchyRecursively(state.Parent, list);
			}
			return list;
		}

		/// <summary>
		/// Beginning at <paramref name="state"/>, go down the path of <see cref="IState.DefaultChild"/>(ren) to collect the head-state.
		/// </summary>
		/// <param name="state">The state from which to collect de default-head (returns itself if childless).</param>
		/// <param name="list">The insofar recursively collected states.</param>
		/// <returns>A list of states beginning with <paramref name="state"/> and ending at the deepest <see cref="IState.DefaultChild"/> layer.</returns>
		public static List<IState> CollectHeadRecursively(this IState state, List<IState> list = null)
		{
			if (list == null)
			{
				list = new List<IState>();
			}

			if (state.DefaultChild != null)
			{
				list.Add(state.DefaultChild);
				return CollectHeadRecursively(state.DefaultChild, list);
			}
			return list;
		}

		public static void Invoke(this IStateListener listener, IStateListener.Callback method, ITransition transition = null)
		{
			switch (method)
			{
				case IStateListener.Callback.OnEnteringState:
					listener.OnEnteringState();
					break;
				case IStateListener.Callback.WhileEnteringState:
					listener.WhileEnteringState(transition);
					break;
				case IStateListener.Callback.OnStateEntered:
					listener.OnStateEntered();
					break;
				case IStateListener.Callback.OnExitingState:
					listener.OnExitingState();
					break;
				case IStateListener.Callback.WhileExitingState:
					listener.WhileExitingState(transition);
					break;
				case IStateListener.Callback.OnStateExit:
					listener.OnStateExit();
					break;
				default:
					SpaxDebug.Error("Callback method extension is not implemented for:", method.ToString());
					return;
			}
		}
	}
}
