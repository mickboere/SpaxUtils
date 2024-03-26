using System;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for <see cref="IIdentifiable"/> data of any type.
	/// </summary>
	public interface ILabeledData : IIdentifiable
	{
		object Value { get; set; }
		Type ValueType { get; }
	}
}
