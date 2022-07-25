using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for objects able to provide a collection of <see cref="ILabeledData"/>.
	/// </summary>
	public interface ILabeledDataProvider
	{
		IEnumerable<ILabeledData> LabeledData { get; }

		/// <summary>
		/// Gets value of labeled data with ID <paramref name="identifier"/> as type <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="identifier">The identifier of the labeled data to retrieve the value of.</param>
		/// <param name="defaultIfNull">The default value to return if the labeled data cannot be found.</param>
		/// <returns>Whether retrieving the value of the labeled data was a success.</returns>
		bool TryGet<T>(string identifier, T defaultIfNull, out T result);

		bool TryGetFloat(string identifier, float defaultIfNull, out float result);

		bool TryGetInt(string identifier, int defaultIfNull, out int result);

		bool TryGetBool(string identifier, bool defaultIfNull, out bool result);

		bool TryGetString(string identifier, string defaultIfNull, out string result);
	}
}
