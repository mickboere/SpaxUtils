using SpaxUtils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils.UI
{
	/// <summary>
	/// UI menu helper class that ties data of type <typeparamref name="T"/> to a <see cref="MenuItem"/> visual.
	/// </summary>
	/// <typeparam name="T">The type of data this menu visualizes.</typeparam>
	public class ItemMenu<T> : IDisposable where T : class
	{
		/// <summary>
		/// Invoked whenever a menu item's button has been clicked.
		/// </summary>
		public event Action<T> SelectedItemEvent;

		/// <summary>
		/// Invoked whenever a menu item's button has been highlighted.
		/// </summary>
		public event Action<T> HighlightedItemEvent;

		/// <summary>
		/// Invoked whenever the list of items in the menu has updated.
		/// </summary>
		public event Action MenuUpdatedEvent;

		/// <summary>
		/// All items currently present within this menu.
		/// </summary>
		public IReadOnlyDictionary<string, (T data, MenuItem visual)> Items => items;

		private Dictionary<string, (T data, MenuItem visual)> items = new Dictionary<string, (T data, MenuItem visual)>();

		private MenuItem template;
		private Func<T, string> itemIdentifier;
		private Func<T, string> itemLabel;
		private Func<T, Sprite> itemSprite;
		private Func<T, IDependencyManager> itemDependencies;
		private bool bypassUpdateEvent;

		public ItemMenu(MenuItem template,
			Func<T, string> itemIdentifier,
			Func<T, string> itemLabel,
			Func<T, Sprite> itemSprite,
			Func<T, IDependencyManager> itemDependencies = null,
			IEnumerable<T> data = null)
		{
			this.template = template;
			this.itemIdentifier = itemIdentifier;
			this.itemLabel = itemLabel;
			this.itemSprite = itemSprite;
			this.itemDependencies = itemDependencies;

			template.gameObject.SetActive(false);

			Populate(data);
		}

		public void Dispose()
		{
			Clear();
		}

		/// <summary>
		/// Clears all present data and visuals and populates the menu with <paramref name="data"/> instead.
		/// </summary>
		public void Populate(IEnumerable<T> data)
		{
			bypassUpdateEvent = true;
			Clear();
			if (data == null)
			{
				return;
			}
			foreach (T item in data)
			{
				Add(item);
			}
			bypassUpdateEvent = false;
			OnMenuUpdated();
		}

		/// <summary>
		/// Adds a new <see cref="MenuItem"/> for <paramref name="data"/>.
		/// </summary>
		/// <param name="data">The data to add a new visual for.</param>
		/// <returns>Whether adding the item was successful or not.</returns>
		public bool Add(T data)
		{
			string identifier = itemIdentifier(data);
			if (items.ContainsKey(identifier))
			{
				SpaxDebug.Warning("Cannot add duplicate item to menu. ", $"Identifier: \"{identifier}\"", items[identifier].visual);
				return false;
			}

			MenuItem visual = GameObject.Instantiate(template, template.transform.parent);
			visual.SetData(data);
			visual.Visualize(identifier, itemSprite(data), itemLabel(data));
			if (itemDependencies != null)
			{
				DependencyUtils.Inject(visual.gameObject, itemDependencies(data));
			}
			visual.ButtonClickedEvent += OnSelectedItemEvent;
			visual.ButtonHighlightEvent += OnHighlightedItemEvent;
			visual.gameObject.SetActive(true);

			items.Add(identifier, (data, visual));
			OnMenuUpdated();
			return true;
		}

		/// <summary>
		/// Removes item ties to <paramref name="data"/> from the menu.
		/// </summary>
		/// <param name="data">The data to remove from the menu.</param>
		public void Remove(T data)
		{
			Remove(itemIdentifier(data));
		}

		/// <summary>
		/// Removes item tied to <paramref name="identifier"/> from the menu.
		/// </summary>
		/// <param name="identifier">Identifier that points to the item that needs to be removed.</param>
		public void Remove(string identifier)
		{
			if (!items.ContainsKey(identifier))
			{
				SpaxDebug.Error("Cannot find item in menu. ", $"Identifier: \"{identifier}\"");
				return;
			}

			// Destroy and remove visual
			GameObject.Destroy(items[identifier].visual.gameObject);
			items.Remove(identifier);
			OnMenuUpdated();
		}

		/// <summary>
		/// Returns the <see cref="MenuItem"/> belonging to <paramref name="data"/>.
		/// </summary>
		public MenuItem GetVisual(T data)
		{
			if (data == null)
			{
				return null;
			}

			string identifier = itemIdentifier(data);
			if (items.ContainsKey(identifier))
			{
				return items[identifier].visual;
			}
			else
			{
				return null;
			}
		}

		/// <summary>
		/// Refresh the visual for data with identifier <paramref name="identifier"/>.
		/// </summary>
		/// <param name="identifier">The identifier for the data of which to refresh the visual for.</param>
		public void RefreshVisual(string identifier)
		{
			RefreshVisual(items[identifier].data);
		}

		/// <summary>
		/// Refresh the visual for <paramref name="data"/>.
		/// </summary>
		/// <param name="data">The data to refresh the visual for.</param>
		public void RefreshVisual(T data)
		{
			string identifier = itemIdentifier(data);
			items[identifier].visual.Visualize(identifier, itemSprite(data), itemLabel(data));
		}

		/// <summary>
		/// Refreshes all <see cref="MenuItem"/>s to ensure they match their data.
		/// </summary>
		public void RefreshVisuals()
		{
			foreach (KeyValuePair<string, (T data, MenuItem visual)> item in items)
			{
				item.Value.visual.Visualize(itemIdentifier(item.Value.data), itemSprite(item.Value.data), itemLabel(item.Value.data));
			}
		}

		/// <summary>
		/// Clears the enitre menu.
		/// </summary>
		public void Clear()
		{
			foreach (KeyValuePair<string, (T data, MenuItem visual)> item in items)
			{
				GameObject.Destroy(item.Value.visual.gameObject);
			}
			items.Clear();
			OnMenuUpdated();
		}

		/// <summary>
		/// Sorts all the <see cref="MenuItem"/>s using <paramref name="comparable"/>.
		/// </summary>
		/// <param name="comparable">The comparable variable of the data to sort by.</param>
		/// <param name="ascending">Whether the sort should ascend or descend.</param>
		public void SortMenu(Func<T, IComparable> comparable, bool ascending = false)
		{
			List<(T, MenuItem)> sortedList = new List<(T, MenuItem)>(items.Values);
			sortedList.Sort((a, b) => ascending ? comparable(b.Item1).CompareTo(comparable(a.Item1)) : comparable(a.Item1).CompareTo(comparable(b.Item1)));
			for (int i = 0; i < sortedList.Count; i++)
			{
				sortedList[i].Item2.transform.SetSiblingIndex(i);
			}
		}

		protected virtual void OnSelectedItemEvent(string identifier)
		{
			SelectedItemEvent?.Invoke(items[identifier].data);
		}

		protected virtual void OnHighlightedItemEvent(string identifier)
		{
			HighlightedItemEvent?.Invoke(items[identifier].data);
		}

		private void OnMenuUpdated()
		{
			if (!bypassUpdateEvent)
			{
				MenuUpdatedEvent?.Invoke();
			}
		}
	}
}
