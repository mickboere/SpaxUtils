﻿using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public abstract class IKComponentBase : EntityComponentBase, IIKComponent
	{
		/// <summary>
		/// Dictionary where the Key is string IK chain identifier and Value is <see cref="IKUpdateMode"/> defining the settings for the chain.
		/// </summary>
		protected abstract Dictionary<string, IKUpdateMode> Settings { get; set; }

		protected Dictionary<string, Dictionary<object, IKInfluencer>> chainInfluencers = new Dictionary<string, Dictionary<object, IKInfluencer>>();

		protected void Update()
		{
			foreach (KeyValuePair<string, IKUpdateMode> setting in Settings)
			{
				if (setting.Value == IKUpdateMode.Update)
				{
					ApplyInfluencer(setting.Key);
				}
			}
		}

		protected void LateUpdate()
		{
			foreach (KeyValuePair<string, IKUpdateMode> setting in Settings)
			{
				if (setting.Value == IKUpdateMode.LateUpdate)
				{
					ApplyInfluencer(setting.Key);
				}
			}
		}

		protected void FixedUpdate()
		{
			foreach (KeyValuePair<string, IKUpdateMode> setting in Settings)
			{
				if (setting.Value == IKUpdateMode.FixedUpdate)
				{
					ApplyInfluencer(setting.Key);
				}
			}
		}

		public void AddInfluencer(object caller, string ikChain, Transform target, float positionWeight, float rotationWeight)
		{
			AddInfluencer(caller, ikChain, target.position, positionWeight, target.rotation, rotationWeight);
		}

		public void AddInfluencer(object caller, string ikChain, Vector3 position, float positionWeight, Quaternion rotation, float rotationWeight)
		{
			if (!chainInfluencers.ContainsKey(ikChain))
			{
				chainInfluencers.Add(ikChain, new Dictionary<object, IKInfluencer>());
			}

			chainInfluencers[ikChain][caller] = new IKInfluencer(ikChain, position, positionWeight, rotation, rotationWeight);
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
