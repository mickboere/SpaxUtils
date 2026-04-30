using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "AllySensesSettings", menuName = "AEMOI/Ally Senses Settings")]
	public class AllySensesSettings : ScriptableObject, IService
	{
		[Header("Memory")]
		[Tooltip("Seconds without sight before a tracked ally is forgotten.")]
		public float ForgetTime = 30f;

		[Header("Follow")]
		[Tooltip("Distance at which E (Follow) stim reaches 1. Below this the stim scales linearly to 0.")]
		public float FollowRange = 10f;

		[Header("Channel Thresholds")]
		[Tooltip("SW: ally health ratio below this triggers Supply stim.")]
		public float SupplyHealthThreshold = 0.5f;
		[Tooltip("W: ally health ratio below this (and in danger) triggers Shield stim.")]
		public float ShieldHealthThreshold = 0.3f;
		[Tooltip("S: own Emotion.S above this triggers Retreat-to-ally stim.")]
		public float FearToRetreatThreshold = 3f;
		[Tooltip("SE: own emotion AbsSum below this triggers Rally stim.")]
		public float RallyMaxMotivation = 2f;
	}
}
