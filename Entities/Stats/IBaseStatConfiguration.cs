namespace SpaxUtils
{
	/// <summary>
	/// Basic interface for stat configurations.
	/// <seealso cref="StatConfiguration"/>.
	/// </summary>
	public interface IBaseStatConfiguration
	{
		/// <summary>
		/// Constant string identifying this stat.
		/// </summary>
		string Identifier { get; }

		/// <summary>
		/// Default value for this stat if it is not set externally.
		/// </summary>
		float DefaultValue { get; }

		/// <summary>
		/// Defines whether this stat's value should be clamped to <see cref="MinValue"/>.
		/// </summary>
		bool HasMinValue { get; }

		/// <summary>
		/// The minimum value to clamp this stat to.
		/// </summary>
		float MinValue { get; }

		/// <summary>
		/// Defines whether this stat's value should be clamped to <see cref="MaxValue"/>.
		/// </summary>
		bool HasMaxValue { get; }

		/// <summary>
		/// The maximum value to clamp this stat to.
		/// </summary>
		float MaxValue { get; }

		/// <summary>
		/// How to handle decimals for this stat.
		/// </summary>
		DecimalMethod Decimals { get; }

		/// <summary>
		/// User-facing name for this stat.
		/// </summary>
		string Name { get; }

		/// <summary>
		/// User-facing description for this stat.
		/// </summary>
		string Description { get; }
	}
}
