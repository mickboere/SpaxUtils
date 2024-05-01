using System.Collections.Generic;
using System.Linq;

namespace SpaxUtils
{
	/// <summary>
	/// Basic <see cref="IPoserInstructions"/> implementation providing only data, no functionality.
	/// </summary>
	public struct PoserInstructions : IPoserInstructions
	{
		public PoseInstruction[] Instructions => instructions;

		private readonly PoseInstruction[] instructions;

		public PoserInstructions(PoseInstruction[] instructions, bool normalizeWeights = false)
		{
			this.instructions = instructions;

			if (normalizeWeights)
			{
				NormalizeWeights();
			}
		}

		public PoserInstructions(bool normalizeWeights, params PoseInstruction[] instructions)
		{
			this.instructions = instructions;

			if (normalizeWeights)
			{
				NormalizeWeights();
			}
		}

		public PoserInstructions(params PoseInstruction[] instructions)
		{
			this.instructions = instructions;
		}

		public PoserInstructions(IEnumerable<PoseInstruction> instructions, bool normalizeWeights = false)
		{
			this.instructions = instructions.ToArray();

			if (normalizeWeights)
			{
				NormalizeWeights();
			}
		}

		public PoserInstructions(PoseTransition poseTransition, float weight = 1f)
		{
			instructions = new PoseInstruction[1] { new PoseInstruction(poseTransition, weight) };
		}

		public PoserInstructions(PoseTransition from, PoseTransition to, float interpolation)
		{
			instructions = new PoseInstruction[2];
			instructions[0] = new PoseInstruction(from, 1f - interpolation);
			instructions[1] = new PoseInstruction(to, interpolation);
		}

		public PoserInstructions(PoseTransition from, IPose to, float interpolation, ILabeledDataProvider additionalToPoseData = null)
		{
			instructions = new PoseInstruction[2];
			instructions[0] = new PoseInstruction(from, 1f - interpolation);
			instructions[1] = new PoseInstruction(new PoseTransition(to, to, 1f, additionalToPoseData), interpolation);
		}

		public PoserInstructions(PoseSequence fromSequence, float fromTime, PoseSequence toSequence, float toTime, float interpolation)
		{
			instructions = new PoseInstruction[2];
			instructions[0] = new PoseInstruction(fromSequence.Evaluate(fromTime), 1f - interpolation);
			instructions[1] = new PoseInstruction(toSequence.Evaluate(toTime), interpolation);
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
