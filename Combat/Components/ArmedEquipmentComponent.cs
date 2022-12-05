using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Provides combatant with all data relevant to a weapon.
	/// </summary>
	public class ArmedEquipmentComponent : EntityComponentBase
	{
		public ArmedSettings ArmedSettings => armedSettings;

		[SerializeField] private ArmedSettings armedSettings;
	}
}
