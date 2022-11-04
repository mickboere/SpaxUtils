using System;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="Attribute"/> used to add a string binding identifier to an object.
	/// </summary>
	public class BindingIdentifierAttribute : Attribute
	{
		public string Identifier { get; }

		public BindingIdentifierAttribute(string identifier)
		{
			Identifier = identifier;
		}
	}
}