using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils.StateMachine
{
	public static class StateMachineExtensions
	{
		public static List<IStateComponent> GetAllChildComponents(this IState state)
		{
			// All of the direct child components:
			List<IStateComponent> allComponents = state.GetComponents();

			// A list of the components to retrieve the children of next:
			List<IStateComponent> nextComponents = new List<IStateComponent>(allComponents);

			// Keep looping as long as there are more components to check:
			while (nextComponents.Count > 0)
			{
				// Store the newly found children here so that we can loop over them in the next iteration.
				List<IStateComponent> newComponents = new List<IStateComponent>();

				foreach (IStateComponent next in nextComponents)
				{
					List<IStateComponent> children = next.GetComponents();
					foreach (IStateComponent child in children)
					{
						// If we haven't found this child yet and the component is not a state, add it to the components.
						if (child is not IState && !allComponents.Contains(child))
						{
							allComponents.Add(child);
							newComponents.Add(child);
						}
					}
				}

				nextComponents = newComponents;
			}

			return allComponents;
		}

		private const int MAX_PARENT_CYCLES = 1000;
		public static List<IStateComponent> GetAllComponentsInParents(this BrainState brainState)
		{
			List<IStateComponent> components = new List<IStateComponent>();
			int cycles = 0;
			BrainState current = brainState.ParentState;
			while (current != null)
			{
				cycles++;
				// Safety check.
				if (cycles > MAX_PARENT_CYCLES)
				{
					SpaxDebug.Error("Max cycles exceeded", $"Ain't no way there's more than {MAX_PARENT_CYCLES} parents, there's likely a circular parent reference.");
					return null;
				}

				// Add parent's components and 
				components.AddRange(current.GetAllChildComponents());
				current = current.ParentState;
			}

			// Reverse to have highest parent's components first.
			components.Reverse();
			return components;
		}
	}
}
