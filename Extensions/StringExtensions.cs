using System;
using System.IO;
using System.Linq;

namespace SpaxUtils
{
	public static class StringExtensions
	{
		public static readonly string[] Alphabet = new string[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z" };

		/// <summary>
		/// Converts int to capital letter of the alphabet.
		/// </summary>
		/// <param name="i">The index of the alphabetical letter.</param>
		/// <returns>Letter of the alphabet at index <paramref name="i"/>.</returns>
		public static string ToAlphabet(this int i)
		{
			if (i < 0 || i >= Alphabet.Length)
			{
				throw new IndexOutOfRangeException($"Alphabetical index '{i}' out of alphabet range.");
			}

			return Alphabet[i];
		}

		/// <summary>
		/// Splits string using <paramref name="divide"/> and returns the last element.
		/// </summary>
		public static string FirstDivision(this string s, string divide = "/")
		{
			return s.Split(divide).First();
		}

		/// <summary>
		/// Splits string using <paramref name="divide"/> and returns the last element.
		/// </summary>
		public static string LastDivision(this string s, string divide = "/")
		{
			return s.Split(divide).Last();
		}

		/// <summary>
		/// Splits string using <paramref name="divide"/> and returns the last element.
		/// </summary>
		public static string SecondToLastDivision(this string s, string divide = "/")
		{
			string[] split = s.Split(divide);
			return split[split.Length - 2];
		}

		/// <summary>
		/// Splits string using <paramref name="divide"/> and returns all but the last element.
		/// </summary>
		public static string GetPath(this string s, char divide = '/')
		{
			for (int i = s.Length - 1; i > 0; i--)
			{
				if (s[i] == divide)
				{
					return s.Substring(0, i);
				}
			}

			return "";
		}

		/// <summary>
		/// Converts a string into an integer which remains consistent across sessions.
		/// </summary>
		/// https://andrewlock.net/why-is-string-gethashcode-different-each-time-i-run-my-program-in-net-core/
		public static int GetDeterministicHashCode(this string str)
		{
			unchecked
			{
				int hash1 = (5381 << 16) + 5381;
				int hash2 = hash1;

				for (int i = 0; i < str.Length; i += 2)
				{
					hash1 = ((hash1 << 5) + hash1) ^ str[i];
					if (i == str.Length - 1)
						break;
					hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
				}

				return hash1 + (hash2 * 1566083941);
			}
		}

		#region Distance functions

		/// <summary>
		/// Returns the amount of differing characters between two strings, also called the Hamming Distance.
		/// https://stackoverflow.com/a/58086233/11012970
		/// </summary>
		public static int Distance(this string a, string b)
		{
			if (string.IsNullOrEmpty(a))
			{
				if (string.IsNullOrEmpty(b))
				{
					return 0;
				}
				return b.Length;
			}

			if (string.IsNullOrEmpty(b))
			{
				return a.Length;
			}

			return a.Zip(b, (d, c) => d != c).Count(f => f) + Math.Abs(a.Length - b.Length);
		}

		/// <summary>
		/// Returns an int signifying the the amount edits one string needs to convert it into the other.
		/// Or more simply put; this is a method of calculating how similar strings are to eachother.
		/// https://stackoverflow.com/a/6944095/11012970
		/// </summary>
		/// <param name="a">string A</param>
		/// <param name="b">string B</param>
		/// <returns>The Levenshtein Distance between <paramref name="a"/> and <paramref name="b"/>.</returns>
		public static int LevenshteinDistance(this string a, string b)
		{
			if (string.IsNullOrEmpty(a))
			{
				if (string.IsNullOrEmpty(b))
				{
					return 0;
				}
				return b.Length;
			}

			if (string.IsNullOrEmpty(b))
			{
				return a.Length;
			}

			int n = a.Length;
			int m = b.Length;
			int[,] d = new int[n + 1, m + 1];

			// initialize the top and right of the table to 0, 1, 2, ...
			for (int i = 0; i <= n; d[i, 0] = i++) ;
			for (int j = 1; j <= m; d[0, j] = j++) ;

			for (int i = 1; i <= n; i++)
			{
				for (int j = 1; j <= m; j++)
				{
					int cost = (b[j - 1] == a[i - 1]) ? 0 : 1;
					int min1 = d[i - 1, j] + 1;
					int min2 = d[i, j - 1] + 1;
					int min3 = d[i - 1, j - 1] + cost;
					d[i, j] = Math.Min(Math.Min(min1, min2), min3);
				}
			}
			return d[n, m];
		}

		#endregion
	}
}
