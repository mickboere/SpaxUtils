using UnityEngine;

namespace SpaxUtils
{
	[RequireComponent(typeof(WeaponTrail))]
	public class PooledWeaponTrail : PooledItemBase
	{
		public override bool Finished => WeaponTrail.ShotBuffer.IsEmpty;
		public override int DefaultPoolSize => defaultPoolSize;

		[field: SerializeField] public WeaponTrail WeaponTrail { get; private set; }
		[SerializeField] private int defaultPoolSize = 10;

		protected void Awake()
		{
			if (WeaponTrail == null)
			{
				WeaponTrail = GetComponent<WeaponTrail>();
			}
		}
	}
}
