using System;
using Newtonsoft.Json;

namespace SpaxUtils
{
	/// <summary>
	/// Single entry of <see cref="ILabeledData"/> within a <see cref="RuntimeDataCollection"/>.
	/// </summary>
	[Serializable]
	public class RuntimeDataEntry : ILabeledData, IDisposable
	{
		/// <summary>
		/// Invoked when the value of this data entry has changed.
		/// </summary>
		public event Action<object> ValueChangedEvent;

		/// <summary>
		/// The identidier/label of this data.
		/// </summary>
		[JsonProperty(Order = -2)]
		public virtual string ID { get; set; }

		/// <summary>
		/// The value of this data.
		/// </summary>
		public virtual object Value
		{
			get { return _value; }
			set
			{
				_value = value;
				OnValueChange();
			}
		}
		protected object _value;

		/// <summary>
		/// The <see cref="Type"/> of the value object.
		/// </summary>
		[JsonIgnore]
		public Type ValueType => _value.GetType();

		/// <summary>
		/// The parent <see cref="RuntimeDataCollection"/> of this data.
		/// </summary>
		[JsonIgnore]
		public RuntimeDataCollection Parent
		{
			get { return _parent; }
			set
			{
				// Remove from current parent.
				if (_parent != null)
				{
					RuntimeDataCollection parent = _parent;
					_parent = null; // To prevent circular parenting loop.
					parent.TryRemove(ID);
				}

				// Add to new parent if not added already.
				if (value != null && !value.ContainsEntry(ID))
				{
					value.TryAdd(this);
				}

				_parent = value;
			}
		}
		private RuntimeDataCollection _parent;

		/// <summary>
		/// Whether this data entry is dirty and should be saved to disk.
		/// Data that is not dirty is considered default and is therefore not saved to save storage space.
		/// </summary>
		[JsonIgnore]
		public bool Dirty { get; set; }

		[JsonConstructor]
		public RuntimeDataEntry(string id, object value)
		{
			ID = id;
			_value = value;
		}

		public RuntimeDataEntry(string id, object value, RuntimeDataCollection parent = null, bool dirty = false)
		{
			ID = id;
			_value = value;
			_parent = parent;
			Dirty = dirty;
		}

		public RuntimeDataEntry(ILabeledData labeledData, RuntimeDataCollection parent = null, bool dirty = false)
		{
			ID = labeledData.ID;
			Value = labeledData.Value;
			_parent = parent;
			Dirty = dirty;
		}

		public virtual void Dispose() { }

		public override string ToString()
		{
			return SpaxJsonUtils.Serialize(this);
		}

		public virtual string ToStringExplicit()
		{
			return $"{{ ID={ID}, Value={Value}, Type={ValueType.FullName} }}";
		}

		protected void OnValueChange()
		{
			Dirty = true;
			ValueChangedEvent?.Invoke(Value);
		}
	}
}
