namespace SpaxUtils
{
	public struct HitData
	{
		public IEntity Hitter { get; private set; }
		public ImpactData Impact { get; private set; }

		public HitData(IEntity hitter, ImpactData impactData)
		{
			Hitter = hitter;
			Impact = impactData;
		}
	}
}
