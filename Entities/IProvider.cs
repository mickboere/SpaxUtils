﻿namespace SpaxUtils
{
	/// <summary>
	/// Interface for objects that can provide data of type <typeparamref name="T"/>.
	/// </summary>
	public interface IProvider<T> : IGenericProvider
	{
		/// <summary>
		/// Get data of type <typeparamref name="T"/> with ID <paramref name="id"/>.
		/// </summary>
		/// <param name="id">The ID of the data to retrieve.</param>
		/// <param name="result">If returns TRUE, contains data of type <typeparamref name="T"/> with ID <paramref name="id"/>.</param>
		/// <returns>Whether getting the data of type <typeparamref name="T"/> with ID <paramref name="id"/> was a success.</returns>
		bool TryGet(string id, out T result);

		/// <summary>
		/// Set data of type <typeparamref name="T"/> with ID <paramref name="id"/>.
		/// </summary>
		/// <param name="id">The ID of the data to set.</param>
		/// <param name="value">The data to store at ID <paramref name="id"/>.</param>
		/// <returns>Whether setting the data of type <typeparamref name="T"/> with ID <paramref name="id"/> was a success.</returns>
		bool TrySet(string id, T value);
	}
}
