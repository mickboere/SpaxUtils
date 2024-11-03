using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils.UI
{
	public class UIScreenManager : MonoBehaviour
	{
		[SerializeField] private RectTransform screenParent;

		private IDependencyManager dependencyManager;
		private IContextManager contextManager;
		private UIScreenContextLibrary screenContextLibrary;

		private Dictionary<string, UIScreen> screens = new Dictionary<string, UIScreen>();

		public void InjectDependencies(IDependencyManager dependencyManager, IContextManager contextManager,
			UIScreenContextLibrary screenContextLibrary)
		{
			this.dependencyManager = dependencyManager;
			this.contextManager = contextManager;
			this.screenContextLibrary = screenContextLibrary;
		}

		protected void OnEnable()
		{
			contextManager.ContextChangedEvent += OnContextChanged;
		}

		protected void OnDisable()
		{
			contextManager.ContextChangedEvent -= OnContextChanged;
		}

		private void OnContextChanged()
		{
			List<string> stack = contextManager.GetStack();
			List<string> active = new List<string>();
			float delay = 0f;

			// Ensure all required screens are present.
			foreach (string context in stack)
			{
				if (!screens.ContainsKey(context) && screenContextLibrary.Screens.ContainsKey(context))
				{
					CreateScreen(context, screenContextLibrary.Screens[context]);
				}
				if (contextManager.IsActive(context))
				{
					active.Add(context);
				}
				if (screens.ContainsKey(context) && !active.Contains(context))
				{
					delay = delay.Max(screens[context].TransitionSettings.OutTime * screens[context].Transition.Progress);
				}
			}

			// Only show screens that are active, hide others.
			foreach (KeyValuePair<string, UIScreen> item in screens)
			{
				if (active.Contains(item.Key))
				{
					item.Value.Show(null, delay);
				}
				else
				{
					item.Value.Hide();
				}
			}
		}

		private void CreateScreen(string context, UIScreen prefab)
		{
			screens.Add(context, DependencyUtils.InstantiateAndInject(prefab.gameObject, screenParent, dependencyManager, true, false).GetComponent<UIScreen>());
			screens[context].HideImmediately();
		}
	}
}
