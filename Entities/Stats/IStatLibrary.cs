using System.Collections.Generic;

namespace SpaxUtils
{
	public interface IStatLibrary
	{
		IReadOnlyList<IStatSetting> Settings { get; }

		IStatSetting Get(string identifier);
	}
}
