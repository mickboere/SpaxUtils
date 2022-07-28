using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class DevService : IService
	{
		private HashSet<IDevCommand> commands = new HashSet<IDevCommand>();

		public void RegisterCommand(IDevCommand command)
		{

		}
	}

	public class DevConsole
	{
		public DevConsole(IDependencyManager dependencies)
		{

		}
	}

	public interface IDevCommand<T>
	{
		string Command { get; }
		bool Process(T context, string[] args);
	}


}
