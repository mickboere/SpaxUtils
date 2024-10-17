using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public abstract class IKComponentBase : EntityComponentMono, IIKComponent
	{
		/// <summary>
		/// Dictionary where the Key is string IK chain identifier and Value is <see cref="UpdateMode"/> defining the settings for the chain.
		/// </summary>
		protected abstract Dictionary<string, UpdateMode> Settings { get; set; }

		protected Dictionary<string, Dictionary<object, IKInfluencer>> chainInfluencers = new Dictionary<string, Dictionary<object, IKInfluencer>>();

		protected void Update()
		{
			foreach (KeyValuePair<string, UpdateMode> setting in Settings)
			{
				if (setting.Value == UpdateMode.Update)
				{
					ApplyInfluencer(setting.Key);
				}
			}
		}

		protected void LateUpdate()
		{
			foreach (KeyValuePair<string, UpdateMode> setting in Settings)
			{
				if (setting.Value == UpdateMode.LateUpdate)
				{
					ApplyInfluencer(setting.Key);
				}
			}
		}

		protected void FixedUpdate()
		{
			foreach (KeyValuePair<string, UpdateMode> setting in Settings)
			{
				if (setting.Value == UpdateMode.FixedUpdate)
				{
					ApplyInfluencer(setting.Key);
				}
			}
		}

		public void AddInfluencer(object caller, string ikChain, int priority, Transform target, float positionWeight, float rotationWeight)
		{
			AddInfluencer(caller, ikChain, priority, target.position, positionWeight, target.rotation, rotationWeight);
		}

		public void AddInfluencer(object caller, string ikChain, int priority, Vector3 position, float positionWeight, Quaternion rotation, float rotationWeight)
		{
			if (!chainInfluencers.ContainsKey(ikChain))
			{
				chainInfluencers.Add(ikChain, new Dictionary<object, IKInfluencer>());
			}

			chainInfluencers[ikChain][caller] = new IKInfluencer(ikChain, priority, position, positionWeight, rotation, rotationWeight);
		}

		public void RemoveInfluencer(object caller, string ikChain)
		{
			if (chainInfluencers.ContainsKey(ikChain))
			{
				chainInfluencers[ikChain].Remove(caller);
				if (chainInfluencers[ikChain].Count == 0)
				{
					chainInfluencers.Remove(ikChain);
				}
			}
		}

		public abstract void ApplyInfluencer(string ikChain);
	}
}
