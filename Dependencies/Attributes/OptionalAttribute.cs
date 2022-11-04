using System;

namespace SpaxUtils
{
	/// <summary>
	/// Attribute used to mark dependency parameters as optional, meaning they will not be created if not already bound to the <see cref="IDependencyManager"/>.
	/// </summary>
	public class OptionalAttribute : Attribute
	{
	}
}
