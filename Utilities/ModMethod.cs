namespace SpaxUtils
{
	/// <summary>
	/// Defines the type of modification for a <see cref="IModifier{T}"/>.
	/// </summary>
	public enum ModMethod
	{
		/// <summary>
		/// Mod application method will depend on operation type of modifier.
		/// Default example: Min/Max operations will be Absolute, everything else Additive.
		/// </summary>
		Auto = 1 << 0,

		/// <summary>
		/// Apply the value directly to the base value, does not store mod. mod(base) > dispose.
		/// </summary>
		Apply = 1 << 1,

		/// <summary>
		/// Additively adjusts the base value for the Additives. mod(base) - base.
		/// </summary>
		Base = 1 << 2,

		/// <summary>
		/// Apply the value to the base value and add the difference to the total. mod(base) - base.
		/// </summary>
		Additive = 1 << 3,

		/// <summary>
		/// Apply the value to the total (executed after additive). mod(base).
		/// </summary>
		Absolute = 1 << 4
	}
}