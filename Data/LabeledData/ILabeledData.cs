using System;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for <see cref="IUnique"/> data of any type.
	/// </summary>
	public interface ILabeledData : IUnique
	{
		object Value { get; set; }
		Type ValueType { get; }
	}
}
