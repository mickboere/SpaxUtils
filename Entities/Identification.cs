using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class Identification : IIdentification
	{
		/// <inheritdoc/>
		public event Action<IIdentification> LabelsChangedEvent;

		/// <inheritdoc/>
		public virtual string ID => id;

		/// <inheritdoc/>
		public virtual string Name { get { return name; } set { name = value; } }

		/// <inheritdoc/>
		public virtual IList<string> Labels => labels;

		public virtual IEntity Entity { get; private set; }

		[SerializeField, ConstDropdown(typeof(IIdentificationIDs), includeEmpty: true, emptyOption: "<RANDOM>")] private string id;
		[SerializeField] private string name;
		[SerializeField, ConstDropdown(typeof(IIdentificationLabels))] private List<string> labels;

		public Identification(IIdentification identification, IEntity entity)
		{
			id = identification.ID;
			if (string.IsNullOrWhiteSpace(id))
			{
				id = Guid.NewGuid().ToString();
			}
			name = identification.Name;
			labels = new List<string>(identification.Labels);
			Entity = entity;
		}

		public Identification(string id, string name, IEnumerable<string> labels, IEntity entity)
		{
			this.id = id;
			if (string.IsNullOrWhiteSpace(id))
			{
				this.id = Guid.NewGuid().ToString();
			}
			this.name = name;
			this.labels = new List<string>(labels);
			Entity = entity;
		}

		#region Label Methods

		/// <inheritdoc/>
		public void Add(params string[] labels)
		{
			bool tagsChanged = false;
			foreach (string tag in labels)
			{
				if (!Labels.Contains(tag))
				{
					Labels.Add(tag);
					tagsChanged = true;
				}
			}
			if (tagsChanged)
			{
				LabelsChangedEvent?.Invoke(this);
			}
		}

		/// <inheritdoc/>
		public void Add(IEnumerable<string> labels)
		{
			Add(labels.ToArray());
		}

		/// <inheritdoc/>
		public void Remove(params string[] labels)
		{
			foreach (string label in labels)
			{
				if (Labels.Contains(label))
				{
					Labels.Remove(label);
				}
			}
		}

		/// <inheritdoc/>
		public void Remove(IEnumerable<string> labels)
		{
			Remove(labels.ToArray());
		}

		/// <inheritdoc/>
		public bool HasAll(params string[] tags)
		{
			return tags.All((t) => Labels.Contains(t));
		}

		/// <inheritdoc/>
		public bool HasAll(IEnumerable<string> tags)
		{
			return tags.All((t) => Labels.Contains(t));
		}

		/// <inheritdoc/>
		public bool HasAny(params string[] tags)
		{
			return tags.Any((t) => Labels.Contains(t));
		}

		/// <inheritdoc/>
		public bool HasAny(IEnumerable<string> tags)
		{
			return tags.Any((t) => Labels.Contains(t));
		}

		#endregion
	}
}
