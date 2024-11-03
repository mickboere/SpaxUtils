using SpaxUtils;
using System;
using System.Collections.Generic;

namespace SpaxUtils.UI
{
	/// <summary>
	/// Helper class that only allows for a single <see cref="UIGroup"/> to be active, and allows for transitioning between them.
	/// </summary>
	public class MenuNavigationHelper : IDisposable
	{
		private List<UIGroup> menus;

		private UIGroup currentMenu;

		public MenuNavigationHelper(params UIGroup[] menus)
			: this((IReadOnlyCollection<UIGroup>)menus) { }

		public MenuNavigationHelper(IReadOnlyCollection<UIGroup> menus)
		{
			this.menus = new List<UIGroup>(menus);
			ImmediatelyTransitionTo(null);
		}

		public void Dispose()
		{
			ImmediatelyTransitionTo(null);
		}

		public void AddMenu(UIGroup menu)
		{
			menus.Add(menu);
			menu.HideImmediately();
		}

		public void RemoveMenu(UIGroup menu, bool transitionOutIfActive = false)
		{
			menus.Remove(menu);

			if (menu == currentMenu)
			{
				if (transitionOutIfActive)
				{
					menu.Hide();
				}

				TransitionTo(null);
			}
		}

		/// <summary>
		/// Hides all stored <see cref="UIGroup"/>s except for <paramref name="target"/>.
		/// </summary>
		/// <param name="target">The <see cref="UIGroup"/> to transition to. Leave NULL to hide all menus.</param>
		/// <param name="crossFade">Whether the transition to <paramref name="target"/> should wait for all other menus to finish hiding.</param>
		/// <param name="immediate">Whether the transition should happen immediately or over time.</param>
		/// <param name="callback">Callback invoked once the transition is completed.</param>
		public void TransitionTo(UIGroup target, bool crossFade = false, bool immediate = false, Action callback = null)
		{
			currentMenu = target;

			if (target != null)
			{
				// Add target to our menus is it isnt added already.
				if (!menus.Contains(target))
				{
					AddMenu(target);
				}

				if (immediate)
				{
					target.ShowImmediately();
					callback?.Invoke();
				}
				else if (crossFade)
				{
					// We don't have to wait for the other menus to hide, start cross-fading.
					target.Show(callback);
				}
			}

			int hidden = 0;
			int hideMenuCount = menus.Count - (target != null ? 1 : 0);
			if (hideMenuCount == 0 && !crossFade && target != null)
			{
				// No menus to hide, show target.
				target.Show(callback);
			}
			else
			{
				// Hide all the other menus, show target once all are hidden.
				foreach (UIGroup menu in menus)
				{
					if (menu != target)
					{
						if (immediate)
						{
							menu.HideImmediately();
						}
						else
						{
							menu.Hide(() =>
							{
								hidden++;
								if (hidden == hideMenuCount && !crossFade && target != null)
								{
									// All menus finished hiding, now show the target.
									target.Show(callback);
								}
							});
						}
					}
				}
			}
		}

		public void ImmediatelyTransitionTo(UIGroup target)
		{
			TransitionTo(target, true);
		}
	}
}
