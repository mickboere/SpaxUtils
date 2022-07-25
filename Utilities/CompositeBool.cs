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

		public void RemoveBool(object owner)
		{
			bools.Remove(owner);
		}
	}
}
