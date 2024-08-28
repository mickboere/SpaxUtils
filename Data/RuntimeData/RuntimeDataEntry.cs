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
			set { _value = value; ValueChangedEvent?.Invoke(_value); }
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

		[JsonConstructor]
		public RuntimeDataEntry(string id, object value)
		{
			ID = id;
			_value = value;
		}

		public RuntimeDataEntry(string id, object value, RuntimeDataCollection parent = null)
		{
			ID = id;
			_value = value;
			_parent = parent;
		}

		public RuntimeDataEntry(ILabeledData labeledData, RuntimeDataCollection parent = null)
		{
			ID = labeledData.ID;
			Value = labeledData.Value;
			_parent = parent;
		}

		public virtual void Dispose() { }

		public override string ToString()
		{
			return $"{{ ID={ID}, Value={Value}, Type={ValueType.FullName} }}";
		}
	}
}
