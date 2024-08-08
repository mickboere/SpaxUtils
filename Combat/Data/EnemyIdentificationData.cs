using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "EnemyIdentificationData", menuName = "ScriptableObjects/Combat/EnemyIdentificationData")]
	public class EnemyIdentificationData : ScriptableObject, IBindingKeyProvider
	{
		public object BindingKey => GetInstanceID();

		public List<string> EnemyLabels => enemyLabels;

		[SerializeField, ConstDropdown(typeof(IIdentificationLabels))] private List<string> enemyLabels;
	}
}
