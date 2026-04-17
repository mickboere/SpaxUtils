using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	public class CompositeBool
	{
		public bool Value
		{
			get
			{
				foreach (KeyValuePair<object, Func<bool>> kvp in bools)
				{
					bool value = kvp.Value();
					if (value != defaultValue)
					{
						return value;
					}
				}
				return defaultValue;
			}
		}

		private bool defaultValue;
		private Dictionary<object, Func<bool>> bools = new Dictionary<object, Func<bool>>();

		public CompositeBool(bool defaultValue)
		{
			this.defaultValue = defaultValue;
		}

		public void AddBool(object owner, Func<bool> func)
		{
			bools[owner] = func;
		}

		public void AddBool(object owner, bool value)
		{
			bools[owner] = () => value;
		}

		public void AddBool(object owner)
		{
			bools[owner] = () => !defaultValue;
		}

		public void RemoveBool(object owner)
		{
			bools.Remove(owner);
		}

		/// <summary>
		/// Implicit float cast so that you don't have to call <see cref="GetValue"/> all the time.
		/// </summary>
		public static implicit operator bool(CompositeBool composite)
		{
			if (composite == null)
			{
				throw new ArgumentNullException("", "Composite is null and could therefore not be converted to a bool.");
			}
			return composite.Value;
		}
	}
}
