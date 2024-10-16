﻿using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Component responsible for applying posing instructions to the <see cref="Animator"/>.
	/// Posing instructions allow for pose-to-pose animation through clever use of blend trees and animation overrides.
	/// </summary>
	/// https://github.com/mickboere/SpaxUtils/blob/master/Animation/Posing/Poser/AnimatorPoser.cs
	public class AnimatorPoser : MonoBehaviour, IDependency
	{
		public IPoserInstructions Control { get; private set; }

		[SerializeField] private PoserComponentSettings settings;
		[SerializeField] private RuntimeAnimatorController animatorController;
		[SerializeField] private int frameRate;

		private AnimatorWrapper animatorWrapper;
		private CallbackService callbackService;
		private Dictionary<string, (Poser main, Dictionary<object, (IPoserInstructions instructions, int prio, float weight)> providers)> layers;
		private AnimatorOverrideController overrideController;
		private AnimationClipOverrides overrides;
		private Dictionary<string, IPoserInstructions> posers;

		public void InjectDependencies(AnimatorWrapper animatorWrapper, CallbackService callbackService)
		{
			this.animatorWrapper = animatorWrapper;
			this.callbackService = callbackService;
			Initialize();
		}

		protected void OnEnable()
		{
			Initialize();
		}

		protected void OnDisable()
		{
			foreach (var layer in layers.Values)
			{
				layer.main.Stop();
			}

			Cleanup();
		}

		protected void Update()
		{
			if (frameRate <= 0)
			{
				UpdateAnimatorPose(Time.deltaTime);
			}
		}

		/// <summary>
		/// Returns the default poser for <paramref name="layer"/>.
		/// </summary>
		/// <param name="layer">The posing layer to retrieve the default poser for.</param>
		/// <returns>The default poser for <paramref name="layer"/>.</returns>
		public Poser GetMainPoser(string layer)
		{
			if (!ValidateRequest(layer))
			{
				return null;
			}

			return layers[layer].main;
		}

		/// <summary>
		/// Return the current complete posing instructions for <paramref name="layer"/>.
		/// </summary>
		/// <param name="layer">The posing layer to retrieve the instructions from.</param>
		/// <returns>The current complete posing instructions for <paramref name="layer"/>.</returns>
		public IPoserInstructions GetPose(string layer)
		{
			if (!ValidateRequest(layer))
			{
				return null;
			}

			if (!posers.ContainsKey(layer))
			{
				return layers[layer].main;
			}

			return posers[layer];
		}

		/// <summary>
		/// Provide new posing instructions for <paramref name="layer"/>.
		/// </summary>
		/// <param name="provider">Object used to identify the instructions' provider, required for revoking instructions.</param>
		/// <param name="layer">The posing layer to provide the instructions for.</param>
		/// <param name="instructions"><see cref="IPoserInstructions"/> implementation defining a desired pose blend.</param>
		/// <param name="prio">The priority of these instructions, higher is more important.</param>
		/// <param name="weight">The weight of these instructions clamped between 0 and 1, higher weight is more important.</param>
		public void ProvideInstructions(object provider, string layer, IPoserInstructions instructions, int prio = 0, float weight = 1f)
		{
			if (!ValidateRequest(layer))
			{
				return;
			}

			layers[layer].providers[provider] = (instructions, prio, weight);
		}

		/// <summary>
		/// Provide new posing instructions for <paramref name="layer"/>.
		/// </summary>
		/// <param name="provider">Object used to identify the instructions' provider, required for revoking instructions.</param>
		/// <param name="layer">The posing layer to provide the instructions for.</param>
		/// <param name="pose">The desired pose blend instructions.</param>
		/// <param name="prio">The priority of these instructions, higher is more important.</param>
		/// <param name="weight">The weight of these instructions clamped between 0 and 1, higher weight is more important.</param>
		public void ProvideInstructions(object provider, string layer, PoseInstruction pose, int prio = 0, float weight = 1f)
		{
			if (!ValidateRequest(layer))
			{
				return;
			}

			layers[layer].providers[provider] = (new PoserInstructions(pose), prio, weight);
		}

		/// <summary>
		/// Provide new posing instructions for <paramref name="layer"/>.
		/// </summary>
		/// <param name="provider">Object used to identify the instructions' provider, required for revoking instructions.</param>
		/// <param name="layer">The posing layer to provide the instructions for.</param>
		/// <param name="pose">The desired pose blend.</param>
		/// <param name="prio">The priority of these instructions, higher is more important.</param>
		/// <param name="weight">The weight of these instructions clamped between 0 and 1, higher weight is more important.</param>
		public void ProvideInstructions(object provider, string layer, PoseTransition pose, int prio = 0, float weight = 1f)
		{
			if (!ValidateRequest(layer))
			{
				return;
			}

			layers[layer].providers[provider] = (new PoserInstructions(new PoseInstruction(pose, 1f)), prio, weight);
		}

		/// <summary>
		/// Remove the posing instructions previously provided by <paramref name="provider"/>.
		/// </summary>
		/// <param name="provider">The object the instructions were provided with.</param>
		public void RevokeInstructions(object provider)
		{
			foreach ((Poser main, Dictionary<object, (IPoserInstructions instructions, int prio, float weight)> providers) layer in layers.Values)
			{
				if (layer.providers.ContainsKey(provider))
				{
					layer.providers.Remove(provider);
				}
			}
		}

		private void UpdateAnimatorPose(float delta)
		{
			foreach ((Poser main, Dictionary<object, (IPoserInstructions instructions, int prio, float weight)> providers) layer in layers.Values)
			{
				PoserSettings settings = layer.main.Settings;
				Control = GetControl(layer.main, layer.providers.Values);
				posers[layer.main.Settings.Layer] = Control;

				// Collect and override all pose clips and apply mirroring.
				int poseCount = Control.Instructions.Length * 2;
				for (int i = 0; i < poseCount; i++)
				{
					ApplyPoseClip(Control.GetPose(i), settings.GetPoseClipName(i), settings.GetMirrorParam(i), i);
				}
				overrideController.ApplyOverrides(overrides);

				// Apply instructions to animator
				for (int i = 0; i < Control.Instructions.Length; i++)
				{
					int toPoseIndex = (i + 1) * 2 - 1;
					animatorWrapper.SetFloat(settings.GetInterpolationParam(toPoseIndex - 1, toPoseIndex), Control.Instructions[i].Transition.Transition);
					animatorWrapper.SetFloat(settings.GetWeightParam(toPoseIndex - 1, toPoseIndex), Control.Instructions[i].Weight);
					//SpaxDebug.Log("Apply Instruction", $"({settings.GetInterpolationParam(toPoseIndex - 1, toPoseIndex)} = {control.Instructions[i].Transition.Transition}, {settings.GetWeightParam(toPoseIndex - 1, toPoseIndex)} = {control.Instructions[i].Weight})");
				}
			}

			void ApplyPoseClip(IPose pose, string poseName, string mirroringParam, int index = -1)
			{
				if (pose == null)
				{
					return;
				}

				//SpaxDebug.Log($"Apply Pose [{index}]", $"{poseName} = {pose.Clip.name}. {mirroringParam} = {pose.Mirror}");
				overrides[poseName] = pose.Clip;
				animatorWrapper.SetFloat(mirroringParam, pose.Mirror ? 1f : 0f);
			}
		}

		private IPoserInstructions GetControl(Poser main, IEnumerable<(IPoserInstructions poser, int prio, float weight)> posers)
		{
			List<(IPoserInstructions poser, int prio, float weight)> collection = new List<(IPoserInstructions poser, int prio, float weight)>();
			collection.Add((main, 0, 1f));
			collection.AddRange(posers);
			collection = collection.OrderByDescending((e) => e.weight).OrderByDescending((e) => e.prio).ToList();

			int topPrio = collection[0].prio;
			float totalWeight = 0f;
			List<PoseInstruction> instructions = new List<PoseInstruction>();
			for (int i = 0; i < collection.Count; i++)
			{
				for (int j = 0; j < collection[i].poser.Instructions.Length; j++)
				{
					float weight = collection[i].weight * collection[i].poser.Instructions[j].Weight;

					if (weight < Mathf.Epsilon)
					{
						// No weight is no use, continue.
						continue;
					}

					if (collection[i].prio < topPrio)
					{
						// Fill gap weight with lower prio instructions.
						weight *= totalWeight < 1f ? 1f - totalWeight : 0f;
					}

					//SpaxDebug.Log($"Add Instruction", $"prio={collection[i].prio}, weight={weight}, oldTotal={totalWeight}, newTotal={totalWeight + weight}\n" +
					//	$"({collection[i].poser.Instructions[j].Transition.FromPose.Clip.name} <{collection[i].poser.Instructions[j].Weight}> {collection[i].poser.Instructions[j].Transition.ToPose.Clip.name})");

					totalWeight += weight;

					instructions.Add(new PoseInstruction(collection[i].poser.Instructions[j].Transition, weight));

					if (instructions.Count == main.Settings.MaxInstructions)
					{
						goto Maxed;
					}
				}
			}

			// Instructions must always contain the max amount, add emptys if necessary.
			for (int i = 0; i < main.Settings.MaxInstructions - instructions.Count; i++)
			{
				instructions.Add(new PoseInstruction(main.To, 0f));
			}

		Maxed:
			return new PoserInstructions(instructions, true);
		}

		#region Management

		private void Initialize()
		{
			if (animatorWrapper == null)
			{
				animatorWrapper = GetComponentInParent<AnimatorWrapper>();

				if (animatorWrapper == null)
				{
					animatorWrapper = GetComponentInChildren<AnimatorWrapper>();

					if (animatorWrapper == null)
					{
						SpaxDebug.Error("AnimatorPoser requires an AnimatorWrapper.");
						return;
					}
				}
			}

			// Clean up in unlikely case of double init.
			Cleanup();

			// Set up posers.
			layers = new Dictionary<string, (Poser main, Dictionary<object, (IPoserInstructions instructions, int prio, float weight)> providers)>();
			foreach (PoserSettings setting in settings.PosersSettings)
			{
				layers.Add(setting.Layer, (new Poser(callbackService, setting), new Dictionary<object, (IPoserInstructions instructions, int prio, float weight)>()));
			}
			posers = new Dictionary<string, IPoserInstructions>();

			// Set up animator controller.
			overrideController = new AnimatorOverrideController(animatorController);
			animatorWrapper.Controller = overrideController;
			overrides = new AnimationClipOverrides(overrideController.overridesCount);
			overrideController.GetOverrides(overrides);

			// Define update loop.
			if (frameRate > 0)
			{
				callbackService.AddCustom(this, 1f / frameRate, UpdateAnimatorPose);
			}
		}

		private void Cleanup()
		{
			if (layers != null)
			{
				foreach (var layer in layers.Values)
				{
					layer.main.Dispose();
				}
				layers.Clear();
			}
			layers = null;

			if (overrideController != null)
			{
				Destroy(overrideController);
			}

			if (callbackService != null)
			{
				callbackService.RemoveCustom(this);
			}
		}

		private bool ValidateRequest(string layer)
		{
			if (!isActiveAndEnabled)
			{
				return false;
			}

			if (!layers.ContainsKey(layer))
			{
				SpaxDebug.Error($"Poser '{layer}' not configured.", "", this);
				return false;
			}
			return true;
		}

		#endregion // Management
	}
}
