using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "PoseBlendMap", menuName = "ScriptableObjects/Animation/PoseBlendMap")]
	public class PoseBlendMap : PosingData
	{
		public const float WEIGHT_THRESHOLD = 0.01f;

		public IReadOnlyList<PoseBlendMapEntry> BlendMap => blendMap;

		[SerializeField] private PoseBlendMethod blendMethod;
		[SerializeField, FormerlySerializedAs("blendTree")] private List<PoseBlendMapEntry> blendMap;

		/// <summary>
		/// Creates a runtime copy of this blend map with the idle entry (at Vector3.zero) replaced.
		/// Returns null if <paramref name="idleOverride"/> is null.
		/// The returned instance is a runtime ScriptableObject and should be destroyed when no longer needed.
		/// </summary>
		public PoseBlendMap CreateWithIdleOverride(PoseSequence idleOverride)
		{
			if (idleOverride == null)
			{
				return null;
			}

			PoseBlendMap copy = CreateInstance<PoseBlendMap>();
			copy.blendMethod = blendMethod;
			copy.blendMap = new List<PoseBlendMapEntry>(blendMap.Count);
			for (int i = 0; i < blendMap.Count; i++)
			{
				if (blendMap[i].Position == Vector3.zero)
				{
					copy.blendMap.Add(new PoseBlendMapEntry(idleOverride, Vector3.zero));
				}
				else
				{
					copy.blendMap.Add(blendMap[i]);
				}
			}
			return copy;
		}

		/// <inheritdoc/>
		public override IPoserInstructions GetInstructions(float time)
		{
			return GetInstructions(time, Vector3.zero);
		}

		/// <inheritdoc/>
		public override IPoserInstructions GetInstructions(float time, Vector3 position)
		{
			return GetInstructions(position, (_) => time);
		}

		public IPoserInstructions GetInstructions(Vector3 position, Func<IPoseSequence, float> timeFunc)
		{
			switch (blendMethod)
			{
				default:
				case PoseBlendMethod.NearestPair:
					var instructions = BlendNearestPair(position);
					PoseTransition a = instructions.from.Evaluate(timeFunc(instructions.from));
					PoseTransition b = instructions.to.Evaluate(timeFunc(instructions.to));
					return new PoserInstructions(a, b, instructions.interpolation);
				case PoseBlendMethod.PolarGradientBlend:
					return CreateInstructions(BlendPolarGradientBand(position), timeFunc);
				case PoseBlendMethod.Obstructive:
					return CreateInstructions(BlendObstructive(position), timeFunc);
			}
		}

		/// <summary>
		/// Creates new <see cref="IPoserInstructions"/> for <paramref name="weights"/>.
		/// </summary>
		/// <param name="weights">The normalized weights for each <see cref="PoseBlendMapEntry"/> in <see cref="blendMap"/></param>
		/// <param name="time">The time at which to evaluate the pose sequences.</param>
		public IPoserInstructions CreateInstructions(float[] weights, Func<IPoseSequence, float> timeFunc)
		{
			// Collect pose instructions exceeding the weight threshold.
			var blends = new List<PoseInstruction>();
			for (int i = 0; i < weights.Length; i++)
			{
				if (weights[i] > WEIGHT_THRESHOLD)
				{
					blends.Add(new PoseInstruction(
						blendMap[i].Sequence.Evaluate(timeFunc(blendMap[i].Sequence)),
						weights[i]));
				}
			}
			return new PoserInstructions(blends);
		}

		/// <summary>
		/// Modified PolarGradientBandInterpolation by Rune Skovbo Johansen: https://runevision.com/thesis/rune_skovbo_johansen_thesis.pdf
		/// From his package: https://assetstore.unity.com/packages/tools/animation/locomotion-system-7135
		/// </summary>
		public float[] BlendPolarGradientBand(Vector3 pos, bool normalize = true)
		{
			float[] weights = new float[blendMap.Count];

			Vector3[] samp = new Vector3[blendMap.Count];
			for (int i = 0; i < blendMap.Count; i++)
			{
				samp[i] = blendMap[i].Position;
			}

			for (int i = 0; i < samp.Length; i++)
			{
				bool outsideHull = false;
				float value = 1;
				for (int j = 0; j < samp.Length; j++)
				{
					if (i == j) continue;

					Vector3 sampleI = samp[i];
					Vector3 sampleJ = samp[j];

					float iAngle, oAngle;
					Vector3 outputProj;
					float angleMultiplier = 2;
					if (sampleI == Vector3.zero)
					{
						iAngle = Vector3.Angle(pos, sampleJ) * Mathf.Deg2Rad;
						oAngle = 0;
						outputProj = pos;
						angleMultiplier = 1;
					}
					else if (sampleJ == Vector3.zero)
					{
						iAngle = Vector3.Angle(pos, sampleI) * Mathf.Deg2Rad;
						oAngle = iAngle;
						outputProj = pos;
						angleMultiplier = 1;
					}
					else
					{
						iAngle = Vector3.Angle(sampleI, sampleJ) * Mathf.Deg2Rad;
						if (iAngle > 0)
						{
							if (pos == Vector3.zero)
							{
								oAngle = iAngle;
								outputProj = pos;
							}
							else
							{
								Vector3 axis = Vector3.Cross(sampleI, sampleJ);
								outputProj = pos.ProjectOnPlane(axis);
								oAngle = Vector3.Angle(sampleI, outputProj) * Mathf.Deg2Rad;
								if (iAngle < Mathf.PI * 0.99f)
								{
									if (Vector3.Dot(Vector3.Cross(sampleI, outputProj), axis) < 0)
									{
										oAngle *= -1;
									}
								}
							}
						}
						else
						{
							outputProj = pos;
							oAngle = 0;
						}
					}

					float magI = sampleI.magnitude;
					float magJ = sampleJ.magnitude;
					float magO = outputProj.magnitude;
					float avgMag = (magI + magJ) / 2;
					magI /= avgMag;
					magJ /= avgMag;
					magO /= avgMag;
					Vector3 vecIJ = new Vector3(iAngle * angleMultiplier, magJ - magI, 0);
					Vector3 vecIO = new Vector3(oAngle * angleMultiplier, magO - magI, 0);

					float newValue = 1 - Vector3.Dot(vecIJ, vecIO) / vecIJ.sqrMagnitude;

					if (newValue < 0)
					{
						outsideHull = true;
						break;
					}
					value = Mathf.Min(value, newValue);
				}
				if (!outsideHull) weights[i] = value;
			}

			// Normalize weights
			if (normalize)
			{
				float summedWeight = 0;
				for (int i = 0; i < samp.Length; i++) summedWeight += weights[i];
				if (summedWeight > 0)
					for (int i = 0; i < samp.Length; i++) weights[i] /= summedWeight;
			}

			return weights;
		}

		/// <summary>
		/// Custom blend method I started on, couldn't bother finishing since the polar blend is what I need for now.
		/// </summary>
		public float[] BlendObstructive(Vector3 position)
		{
			if (blendMap.Count < 1)
			{
				SpaxDebug.Error($"PoserBlendTree requires at least 1 pose sequences.");
				return null;
			}
			if (blendMap.Count == 1)
			{
				return null;
			}

			// Collect data.
			var p = new (PoseBlendMapEntry e, Vector3 dir, float dis)[blendMap.Count];
			float closest = float.MaxValue;
			float furthest = 0f;
			for (int i = 0; i < p.Length; i++)
			{
				Vector3 dir = position - blendMap[i].Position;
				float dis = dir.magnitude;
				p[i] = (blendMap[i], dir.normalized, dis);
				if (dis < closest) { closest = dis; }
				if (dis > furthest) { furthest = dis; }
			}

			// Adjust weights depending on distance and direction.
			float[] weights = new float[blendMap.Count];
			float totalWeight = 0f;
			for (int i = 0; i < p.Length; i++)
			{
				var a = p[i];
				float location = a.dis.InverseLerp(furthest, closest);
				float obstruction = 0f;
				for (int j = 0; j < p.Length; j++)
				{
					var b = p[j];
					float comp = a.dis / b.dis;
					obstruction = obstruction.Max((comp * comp - 1f).Pos() * a.dir.ClampedDot(b.dir).InQuad());
				}
				weights[i] = Mathf.Clamp01(location - obstruction);
				totalWeight += weights[i];
			}

			// Normalize weight.
			for (int i = 0; i < p.Length; i++)
			{
				weights[i] /= totalWeight;
			}

			return weights;
		}

		/// <summary>
		/// Legacy pose blend method that only returns 2 closest sequences to blend between.
		/// </summary>
		public (IPoseSequence from, IPoseSequence to, float interpolation) BlendNearestPair(Vector3 position)
		{
			if (blendMap.Count < 1)
			{
				SpaxDebug.Error($"PoserBlendTree requires at least 1 pose sequences.");
				return default;
			}
			if (blendMap.Count == 1)
			{
				return (blendMap[0].Sequence, blendMap[0].Sequence, 1f);
			}

			float closestDistance = float.MaxValue;
			PoseBlendMapEntry closest = null;
			float secondDistance = float.MaxValue;
			PoseBlendMapEntry second = null;
			foreach (PoseBlendMapEntry sequence in blendMap)
			{
				float distance = Vector3.Distance(position, sequence.Position);
				if (distance < closestDistance)
				{
					secondDistance = closestDistance;
					second = closest;
					closestDistance = distance;
					closest = sequence;
				}
				else if (distance < secondDistance)
				{
					secondDistance = distance;
					second = sequence;
				}
			}

			if (closest == null || second == null)
			{
				SpaxDebug.Error("Could not complete blend", $"{name}, pos={position}, closest={closest}, second={second}");
				return (null, null, 0f);
			}

			Vector3Extensions.InverseLerp(second.Position, closest.Position, position, out float interpolation);
			return (second.Sequence, closest.Sequence, Mathf.Clamp01(interpolation));
		}
	}
}
