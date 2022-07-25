using System;

namespace SpaxUtils
{
	/// <summary>
	/// Single entry of <see cref="ILabeledData"/> within a <see cref="RuntimeDataCollection"/>.
	/// </summary>
	public class RuntimeDataEntry : ILabeledData
	{
		/// <summary>
		/// Invoked when the value of this 
		/// </summary>
		public event Action<object> ValueChangedEvent;

		/// <summary>
		/// The identidier/label of this data.
		/// </summary>
		public virtual string UID { get; private set; }

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
		/// The parent <see cref="RuntimeDataCollection"/> of this data.
		/// </summary>
		public RuntimeDataCollection Parent { get; set; }

		public RuntimeDataEntry(string id, object value, RuntimeDataCollection parent = null)
		{
			UID = id;
			_value = value;
			Parent = parent;
		}

		public RuntimeDataEntry(ILabeledData labeledData, RuntimeDataCollection parent = null)
		{
			UID = labeledData.UID;
			Value = labeledData.Value;
			Parent = parent;
		}

		public override string ToString()
		{
			return $"{{ ID={UID}, Value={Value} }}";
		}
	}
}
