namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Determines how many connections a port supports.
	/// </summary>
	public enum ConnectionType
	{
		/// <summary>Allow multiple simultaneous connections.</summary>
		Multiple,
		/// <summary>A new connection replaces any existing connection.</summary>
		Override,
	}

	/// <summary>
	/// Determines which value types may be connected to a port.
	/// </summary>
	public enum TypeConstraint
	{
		/// <summary>No type restriction.</summary>
		None,
		/// <summary>Allow connections where the output type is assignable to the input type.</summary>
		Inherited,
		/// <summary>Allow only identical types.</summary>
		Strict,
	}
}
