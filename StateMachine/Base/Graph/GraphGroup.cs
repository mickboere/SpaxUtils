using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Serializable data for a visual group in a <see cref="GraphAsset"/>.
	/// Groups are editor-only cosmetic containers; they do not affect runtime behaviour.
	/// </summary>
	[Serializable]
	public class GraphGroup
	{
		[SerializeField] private string guid;
		[SerializeField] private string title;
		[SerializeField] private Rect rect;
		[SerializeField] private List<string> nodeGuids = new List<string>();

		public string Guid => guid;
		public string Title { get => title; set => title = value; }
		public Rect Rect { get => rect; set => rect = value; }
		public IReadOnlyList<string> NodeGuids => nodeGuids;

		public GraphGroup(string title, Rect rect)
		{
			guid = System.Guid.NewGuid().ToString();
			this.title = title;
			this.rect = rect;
		}

		public void SetNodeGuids(IEnumerable<string> guids)
		{
			nodeGuids = new List<string>(guids);
		}
	}
}
