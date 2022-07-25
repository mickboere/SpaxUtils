using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace SpaxUtils
{
	[Serializable]
	public class PoserSettings
	{
		public string Layer => layer;

		public int MaxInstructions => maxInstructions;

		[SerializeField, ConstDropdown(typeof(IAnimatorLayerConstants)), FormerlySerializedAs("handle")] private string layer;
		[SerializeField] private int maxInstructions = 3;
		[SerializeField] private string poseClipName = "Pose_";
		[SerializeField] private string mirrorParam = "Mirror_";
		[SerializeField] private string interpolationParam = "Interpolation_";
		[SerializeField] private string weightParam = "Weight_";

		public string GetPoseClipName(int poseIndex)
		{
			return poseClipName + poseIndex.ToAlphabet();
		}

		public string GetMirrorParam(int poseIndex)
		{
			return mirrorParam + poseIndex.ToAlphabet();
		}

		public string GetInterpolationParam(params int[] poseIndices)
		{
			return interpolationParam + GetSuffix(poseIndices);
		}

		public string GetWeightParam(params int[] poseIndices)
		{
			return weightParam + GetSuffix(poseIndices);
		}

		private string GetSuffix(params int[] indices)
		{
			string suffix = "";
			for (int i = 0; i < indices.Length; i++)
			{
				suffix += indices[i].ToAlphabet();
			}
			return suffix;
		}
	}
}
