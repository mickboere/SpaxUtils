using System.Collections.Generic;
using System.Linq;

namespace SpaxUtils
{
	/// <summary>
	/// Basic <see cref="IPoser"/> implementation providing only data, no functionality.
	/// </summary>
	public struct PoserStruct : IPoser
	{
		public PoseInstructions[] Instructions => instructions;

		private readonly PoseInstructions[] instructions;

		public PoserStruct(PoseInstructions[] instructions, bool normalizeWeights = false)
		{
			this.instructions = instructions;

			if (normalizeWeights)
			{
				NormalizeWeights();
			}
		}

		public PoserStruct(bool normalizeWeights, params PoseInstructions[] instructions)
		{
			this.instructions = instructions;

			if (normalizeWeights)
			{
				NormalizeWeights();
			}
		}

		public PoserStruct(params PoseInstructions[] instructions)
		{
			this.instructions = instructions;
		}

		public PoserStruct(IEnumerable<PoseInstructions> instructions, bool normalizeWeights = false)
		{
			this.instructions = instructions.ToArray();

			if (normalizeWeights)
			{
				NormalizeWeights();
			}
		}

		public PoserStruct(PoseTransition from, PoseTransition to, float interpolation)
		{
			instructions = new PoseInstructions[2];
			instructions[0] = new PoseInstructions(from, 1f - interpolation);
			instructions[1] = new PoseInstructions(to, interpolation);
		}

		public PoserStruct(PoseTransition from, IPose to, float interpolation, ILabeledDataProvider additionalToPoseData = null)
		{
			instructions = new PoseInstructions[2];
			instructions[0] = new PoseInstructions(from, 1f - interpolation);
			instructions[1] = new PoseInstructions(new PoseTransition(to, to, 1f, additionalToPoseData), interpolation);
		}

		public PoserStruct(PoseSequence fromSequence, float fromTime, PoseSequence toSequence, float toTime, float interpolation)
		{
			instructions = new PoseInstructions[2];
			instructions[0] = new PoseInstructions(fromSequence.Evaluate(fromTime), 1f - interpolation);
			instructions[1] = new PoseInstructions(toSequence.Evaluate(toTime), interpolation);
		}

		public void NormalizeWeights()
		{
			float totalWeight = instructions.Sum((i) => i.Weight);
			for (int i = 0; i < instructions.Length; i++)
			{
				instructions[i].Weight = instructions[i].Weight > 0f ? instructions[i].Weight / totalWeight : 0f;
			}
		}
	}
}
