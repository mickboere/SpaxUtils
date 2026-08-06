using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Bakes a <see cref="PoseSequence"/> driven move into a single <see cref="AnimationClip"/> plus an
	/// <see cref="AnimationTimeline"/> whose markers reproduce the move's current timing values exactly.
	/// </summary>
	public class PoseSequenceConverter : EditorWindow
	{
		private const string ROOT_Q = "RootQ.";

		private enum BakeMode
		{
			/// <summary>A key every frame. Exact for any transition curve, at the cost of key count.</summary>
			Resample = 0,
			/// <summary>Keys only where poses sit, tangents taken from the transition curves.</summary>
			Keyframed = 1
		}

		private PerformanceMove move;
		private BakeMode bakeMode = BakeMode.Keyframed;
		private float sampleRate = 60f;
		private DefaultAsset outputFolder;
		private Vector2 scroll;
		private string report;

		private const string PREF_MODE = "SpaxUtils.SequenceConverter.Mode";
		private const string PREF_RATE = "SpaxUtils.SequenceConverter.Rate";
		private const string PREF_FOLDER = "SpaxUtils.SequenceConverter.Folder";

		[MenuItem("Tools/Convert Pose Sequence to Timeline")]
		private static void Open()
		{
			GetWindow<PoseSequenceConverter>("Sequence Converter").Show();
		}

		private void OnEnable()
		{
			bakeMode = (BakeMode)EditorPrefs.GetInt(PREF_MODE, (int)BakeMode.Keyframed);
			sampleRate = EditorPrefs.GetFloat(PREF_RATE, 60f);

			string folder = EditorPrefs.GetString(PREF_FOLDER, string.Empty);
			if (!string.IsNullOrEmpty(folder))
			{
				outputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder);
			}
		}

		/// <summary>
		/// Written on change rather than on close, so settings survive a recompile or a crashed session.
		/// </summary>
		private void SaveSettings()
		{
			EditorPrefs.SetInt(PREF_MODE, (int)bakeMode);
			EditorPrefs.SetFloat(PREF_RATE, sampleRate);
			EditorPrefs.SetString(PREF_FOLDER,
				outputFolder == null ? string.Empty : AssetDatabase.GetAssetPath(outputFolder));
		}

		private void OnGUI()
		{
			EditorGUILayout.Space(4);
			move = (PerformanceMove)EditorGUILayout.ObjectField("Move", move, typeof(PerformanceMove), false);

			// Settings persist; the move deliberately does not, since it changes every conversion.
			EditorGUI.BeginChangeCheck();
			bakeMode = (BakeMode)EditorGUILayout.EnumPopup("Bake Mode", bakeMode);
			using (new EditorGUI.DisabledScope(bakeMode != BakeMode.Resample))
			{
				sampleRate = EditorGUILayout.FloatField("Sample Rate", sampleRate);
			}
			outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder", outputFolder, typeof(DefaultAsset), false);
			if (EditorGUI.EndChangeCheck())
			{
				SaveSettings();
			}

			PoseSequence sequence = move == null ? null : move.PosingData as PoseSequence;
			if (move == null)
			{
				EditorGUILayout.HelpBox("Assign a PerformanceMove. Its timing values become the markers.", MessageType.Info);
				return;
			}

			if (sequence == null)
			{
				EditorGUILayout.HelpBox("This move's PosingData is not a PoseSequence.", MessageType.Warning);
				return;
			}

			DrawPreview(sequence);

			EditorGUILayout.Space(6);
			using (new EditorGUI.DisabledScope(sampleRate <= 0f))
			{
				if (GUILayout.Button("Convert", GUILayout.Height(28f)))
				{
					try
					{
						report = Convert(move, sequence, bakeMode, sampleRate, FolderPath(sequence));
					}
					catch (System.Exception exception)
					{
						// An exception mid-bake leaves a cleared clip behind, which looks like a silent
						// success. Surface it in the window as well as the console.
						report = $"FAILED: {exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}";
					}

					Debug.Log($"[PoseSequenceConverter] {report}");
					GUIUtility.ExitGUI();
				}
			}

			if (!string.IsNullOrEmpty(report))
			{
				EditorGUILayout.Space(4);
				scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(180f));
				EditorGUILayout.HelpBox(report, MessageType.None);
				EditorGUILayout.EndScrollView();
			}
		}

		private void DrawPreview(PoseSequence sequence)
		{
			EditorGUILayout.Space(6);
			EditorGUILayout.LabelField("Will produce", EditorStyles.boldLabel);

			float clipLength = ClipLength(sequence);
			EditorGUILayout.LabelField("Clip length", $"{clipLength:0.000}s  ({Mathf.CeilToInt(clipLength * sampleRate)} frames)");
			EditorGUILayout.LabelField("Sequence covers", $"{sequence.TotalDuration:0.000}s across {sequence.PoseCount} poses");
			EditorGUILayout.LabelField("Timeline extent", $"{Mathf.Max(clipLength, move.MinDuration + move.Release):0.000}s  (release sustains past the clip)");

			foreach (TimelineMarker marker in BuildMarkers(move))
			{
				float end = marker.EndMode == MarkerEnd.Duration ? marker.Time + marker.Duration : marker.Time;
				string label = end > marker.Time ? $"{marker.Time:0.000} .. {end:0.000}s" : $"{marker.Time:0.000}s";
				EditorGUILayout.LabelField($"   {marker.ID}", label);
			}

			// The sequence's own timebase is independent of MinDuration, so a mismatch silently holds the
			// final pose (or truncates). Surfacing it here is half the reason this migration exists.
			if (!Mathf.Approximately(sequence.TotalDuration, move.MinDuration))
			{
				EditorGUILayout.HelpBox(
					$"Sequence duration ({sequence.TotalDuration:0.000}s) differs from MinDuration ({move.MinDuration:0.000}s). " +
					"The bake holds the final pose through the remainder, matching current runtime behaviour.",
					MessageType.Warning);
			}
		}

		#region Conversion

		/// <summary>
		/// The clip is the animation and nothing more. Release is a region sustaining past the last frame,
		/// not padding baked into the curves.
		/// </summary>
		private static float ClipLength(PoseSequence sequence)
		{
			return sequence.TotalDuration;
		}

		private string FolderPath(PoseSequence sequence)
		{
			if (outputFolder != null)
			{
				return AssetDatabase.GetAssetPath(outputFolder);
			}
			return Path.GetDirectoryName(AssetDatabase.GetAssetPath(sequence)).Replace("\\", "/");
		}

		private static string Convert(PerformanceMove move, PoseSequence sequence, BakeMode mode, float rate, string folder)
		{
			float clipLength = ClipLength(sequence);
			float seqDuration = sequence.TotalDuration;
			int frames = Mathf.Max(1, Mathf.CeilToInt(clipLength * rate));

			// Cache every pose clip's value per binding; pose clips hold a single keyframe at t=0.
			Dictionary<AnimationClip, Dictionary<EditorCurveBinding, float>> cache = BuildCache(sequence, out var bindings, out bool mirrored);

			// Bail loudly rather than writing an empty clip, which reads as "it worked" until you open it.
			if (sequence.PoseCount == 0 || cache.Count == 0 || bindings.Count == 0)
			{
				return $"ABORTED - nothing to bake.\n" +
					$"poses: {sequence.PoseCount}, pose clips resolved: {cache.Count}, curve bindings found: {bindings.Count}.\n" +
					(cache.Count == 0
						? "The sequence's poses have no AnimationClip assigned."
						: "The pose clips contain no readable curves (AnimationUtility.GetCurveBindings returned none).");
			}

			float[] times = CumulativeTimes(sequence);
			int keysPerCurve = 0;
			bool rootRotation = false;

			// Bake into memory FIRST. Clearing the target before knowing whether this pass can produce
			// curves is what turns an off run into destroyed output - the whole point of the migration is
			// that a failure must be a no-op, not a wipe.
			var pending = new List<(EditorCurveBinding binding, AnimationCurve curve)>();

			foreach (EditorCurveBinding binding in bindings)
			{
				if (binding.propertyName.StartsWith(ROOT_Q))
				{
					rootRotation = true;
					continue;
				}

				AnimationCurve curve = mode == BakeMode.Resample
					? ResampleCurve(sequence, cache, binding, rate, frames, clipLength, seqDuration)
					: KeyframeCurve(sequence, cache, binding, times);

				// A zero-key curve does not write an empty curve, it DELETES the binding.
				if (curve.length == 0)
				{
					continue;
				}

				keysPerCurve = curve.length;
				pending.Add((binding, curve));
			}

			if (rootRotation)
			{
				pending.AddRange(BakeRootRotation(sequence, cache, mode, times, rate, frames, clipLength, seqDuration));
			}

			// Nothing usable? Leave whatever is already on disk completely alone.
			if (pending.Count == 0)
			{
				return $"ABORTED - produced no curves from {bindings.Count} bindings across {cache.Count} pose clips.\n" +
					"The existing asset was left untouched.";
			}

			AnimationClip baked = LoadOrCreate<AnimationClip>($"{folder}/{sequence.name}_Baked.anim");
			baked.ClearCurves();
			foreach ((EditorCurveBinding binding, AnimationCurve curve) in pending)
			{
				AnimationUtility.SetEditorCurve(baked, binding, curve);
			}

			CopyClipSettings(sequence, baked);
			EditorUtility.SetDirty(baked);

			AnimationTimeline timeline = LoadOrCreate<AnimationTimeline>($"{folder}/Timeline_{move.name}.asset");
			timeline.Setup(baked, BuildMarkers(move), BuildGlobalData(sequence));
			EditorUtility.SetDirty(timeline);

			// SaveAssets only, mirroring ExtractPosesToClips. A Refresh here can reimport the clip out from
			// under the curves we just wrote.
			AssetDatabase.SaveAssets();

			// Read the asset BACK rather than trusting the write. This is the check that turns a silent
			// empty export into a reported failure.
			int persisted = AnimationUtility.GetCurveBindings(baked).Length;

			string warnings = mirrored
				? "\nWARNING: a pose has Mirror enabled. Mirroring is NOT baked - the result will be wrong."
				: string.Empty;
			warnings += mode == BakeMode.Keyframed ? DescribeCurveFidelity(sequence) : string.Empty;

			if (persisted < pending.Count)
			{
				warnings += $"\nERROR: wrote {pending.Count} curves but the asset reports {persisted}. " +
					"Re-run before relying on this clip.";
				Debug.LogError($"[PoseSequenceConverter] '{sequence.name}' did not persist: " +
					$"{pending.Count} written, {persisted} present.");
			}

			string density = mode == BakeMode.Resample
				? $"{frames + 1} keys per curve at {rate:0} fps"
				: $"{keysPerCurve} keys per curve (one per pose)";

			return $"Baked '{sequence.name}' [{mode}] -> {AssetDatabase.GetAssetPath(baked)}\n" +
				$"{density}, {pending.Count} curves built from {bindings.Count} bindings, {persisted} persisted, " +
				$"clip length {clipLength:0.000}s (asset reports {baked.length:0.000}s).\n" +
				$"Timeline -> {AssetDatabase.GetAssetPath(timeline)}{warnings}";
		}

		/// <summary>Time at which each pose is reached, i.e. the running sum of pose durations.</summary>
		private static float[] CumulativeTimes(PoseSequence sequence)
		{
			float[] times = new float[sequence.PoseCount];
			float running = 0f;
			for (int i = 0; i < sequence.PoseCount; i++)
			{
				running += sequence.Get(i).Duration;
				times[i] = running;
			}
			return times;
		}

		private static AnimationCurve ResampleCurve(PoseSequence sequence,
			Dictionary<AnimationClip, Dictionary<EditorCurveBinding, float>> cache,
			EditorCurveBinding binding, float rate, int frames, float clipLength, float seqDuration)
		{
			AnimationCurve curve = new AnimationCurve();
			for (int f = 0; f <= frames; f++)
			{
				float time = Mathf.Min(f / rate, clipLength);
				curve.AddKey(new Keyframe(time, SampleScalar(sequence, cache, binding, Mathf.Min(time, seqDuration))));
			}
			return curve;
		}

		/// <summary>
		/// One key per pose, with tangents taken from the segment's transition curve so the shape between
		/// poses is carried by the curve rather than by a wall of keys.
		/// </summary>
		private static AnimationCurve KeyframeCurve(PoseSequence sequence,
			Dictionary<AnimationClip, Dictionary<EditorCurveBinding, float>> cache,
			EditorCurveBinding binding, float[] times)
		{
			int count = sequence.PoseCount;
			float[] values = new float[count];
			for (int i = 0; i < count; i++)
			{
				TryValue(cache, sequence.Get(i), binding, out values[i]);
			}

			return BuildTangentedCurve(sequence, times, values);
		}

		/// <summary>
		/// Shared key/tangent construction. Slope along a segment is (b - a) * f'(p) / duration, where f is
		/// the segment's transition curve - so the authored easing survives without resampling.
		/// </summary>
		private static AnimationCurve BuildTangentedCurve(PoseSequence sequence, float[] times, float[] values)
		{
			int count = times.Length;
			var keys = new List<Keyframe>(count);
			var sourceIndices = new List<int>(count);

			for (int i = 0; i < count; i++)
			{
				// A zero-length gap would put two keys at one time, which a curve cannot hold.
				if (keys.Count > 0 && Mathf.Approximately(times[i], keys[keys.Count - 1].time))
				{
					keys[keys.Count - 1] = new Keyframe(times[i], values[i]);
					sourceIndices[sourceIndices.Count - 1] = i;
					continue;
				}

				keys.Add(new Keyframe(times[i], values[i]));
				sourceIndices.Add(i);
			}

			for (int k = 1; k < keys.Count; k++)
			{
				int pose = sourceIndices[k];
				float duration = keys[k].time - keys[k - 1].time;
				if (duration <= 0f)
				{
					continue;
				}

				float delta = keys[k].value - keys[k - 1].value;
				IPose transition = sequence.Get(pose);

				Keyframe previous = keys[k - 1];
				previous.outTangent = delta * SlopeAtStart(transition) / duration;
				keys[k - 1] = previous;

				Keyframe current = keys[k];
				current.inTangent = delta * SlopeAtEnd(transition) / duration;
				keys[k] = current;
			}

			return new AnimationCurve(keys.ToArray());
		}

		private const float SLOPE_STEP = 0.001f;

		private static float SlopeAtStart(IPose pose)
		{
			return (pose.EvaluateTransition(SLOPE_STEP) - pose.EvaluateTransition(0f)) / SLOPE_STEP;
		}

		private static float SlopeAtEnd(IPose pose)
		{
			return (pose.EvaluateTransition(1f) - pose.EvaluateTransition(1f - SLOPE_STEP)) / SLOPE_STEP;
		}

		/// <summary>
		/// A cubic between two keys cannot reproduce a transition curve with interior shaping, so measure the
		/// error rather than quietly approximating it.
		/// </summary>
		private static string DescribeCurveFidelity(PoseSequence sequence)
		{
			string report = string.Empty;
			for (int i = 1; i < sequence.PoseCount; i++)
			{
				IPose pose = sequence.Get(i);
				float y0 = pose.EvaluateTransition(0f);
				float y1 = pose.EvaluateTransition(1f);
				float m0 = SlopeAtStart(pose);
				float m1 = SlopeAtEnd(pose);

				float worst = 0f;
				for (int s = 1; s < 16; s++)
				{
					float p = s / 16f;
					float p2 = p * p;
					float p3 = p2 * p;
					float hermite = (2f * p3 - 3f * p2 + 1f) * y0 + (p3 - 2f * p2 + p) * m0 +
						(-2f * p3 + 3f * p2) * y1 + (p3 - p2) * m1;
					worst = Mathf.Max(worst, Mathf.Abs(hermite - pose.EvaluateTransition(p)));
				}

				if (worst > 0.02f)
				{
					report += $"\nNOTE: pose {i + 1}'s transition curve is too shaped for a single cubic " +
						$"(max error {worst:0.000}). Use Resample for an exact bake.";
				}
			}
			return report;
		}

		private static Dictionary<AnimationClip, Dictionary<EditorCurveBinding, float>> BuildCache(
			PoseSequence sequence, out List<EditorCurveBinding> bindings, out bool mirrored)
		{
			var cache = new Dictionary<AnimationClip, Dictionary<EditorCurveBinding, float>>();
			var seen = new HashSet<EditorCurveBinding>();
			bindings = new List<EditorCurveBinding>();
			mirrored = false;

			for (int i = 0; i < sequence.PoseCount; i++)
			{
				IPose pose = sequence.Get(i);
				mirrored |= pose.Mirror;

				if (pose.Clip == null || cache.ContainsKey(pose.Clip))
				{
					continue;
				}

				var values = new Dictionary<EditorCurveBinding, float>();
				foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(pose.Clip))
				{
					AnimationCurve curve = AnimationUtility.GetEditorCurve(pose.Clip, binding);
					if (curve == null)
					{
						continue;
					}

					values[binding] = curve.Evaluate(0f);
					if (seen.Add(binding))
					{
						bindings.Add(binding);
					}
				}
				cache[pose.Clip] = values;
			}

			return cache;
		}

		/// <summary>
		/// Samples one scalar curve through the sequence's own evaluation, so the bake reproduces the
		/// authored transition curves rather than approximating them with keyframe tangents.
		/// </summary>
		private static float SampleScalar(PoseSequence sequence,
			Dictionary<AnimationClip, Dictionary<EditorCurveBinding, float>> cache,
			EditorCurveBinding binding, float time)
		{
			PoseTransition transition = sequence.Evaluate(time);
			bool hasFrom = TryValue(cache, transition.FromPose, binding, out float from);
			bool hasTo = TryValue(cache, transition.ToPose, binding, out float to);

			// A pose missing the curve holds the other's value rather than snapping the bone to zero.
			if (!hasFrom) return hasTo ? to : 0f;
			if (!hasTo) return from;

			return Mathf.Lerp(from, to, transition.Transition);
		}

		/// <summary>
		/// Root rotation is baked as a quaternion, not four independent scalars. Component-wise lerping
		/// across a hemisphere flip rotates the long way round - the exact cause of the Slash_B pirouette.
		/// </summary>
		private static List<(EditorCurveBinding binding, AnimationCurve curve)> BakeRootRotation(PoseSequence sequence,
			Dictionary<AnimationClip, Dictionary<EditorCurveBinding, float>> cache,
			BakeMode mode, float[] times, float rate, int frames, float clipLength, float seqDuration)
		{
			EditorCurveBinding[] rootBindings =
			{
				EditorCurveBinding.FloatCurve("", typeof(Animator), "RootQ.x"),
				EditorCurveBinding.FloatCurve("", typeof(Animator), "RootQ.y"),
				EditorCurveBinding.FloatCurve("", typeof(Animator), "RootQ.z"),
				EditorCurveBinding.FloatCurve("", typeof(Animator), "RootQ.w"),
			};

			if (mode == BakeMode.Keyframed)
			{
				return BakeRootRotationKeyframed(sequence, cache, rootBindings, times);
			}

			AnimationCurve[] curves = { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
			Vector4 previous = Vector4.zero;

			for (int f = 0; f <= frames; f++)
			{
				float time = Mathf.Min(f / rate, clipLength);
				PoseTransition transition = sequence.Evaluate(Mathf.Min(time, seqDuration));

				Vector4 from = ReadQuaternion(cache, transition.FromPose, rootBindings);
				Vector4 to = ReadQuaternion(cache, transition.ToPose, rootBindings);

				// Shortest arc between the two poses...
				if (Vector4.Dot(from, to) < 0f)
				{
					to = -to;
				}

				Vector4 value = Vector4.Lerp(from, to, transition.Transition);
				if (value.sqrMagnitude > 0f)
				{
					value.Normalize();
				}

				// ...and continuity with the previous sample, so the baked curve never flips mid-clip.
				if (previous.sqrMagnitude > 0f && Vector4.Dot(previous, value) < 0f)
				{
					value = -value;
				}
				previous = value;

				for (int c = 0; c < 4; c++)
				{
					curves[c].AddKey(new Keyframe(time, value[c]));
				}
			}

			var result = new List<(EditorCurveBinding, AnimationCurve)>(4);
			for (int c = 0; c < 4; c++)
			{
				result.Add((rootBindings[c], curves[c]));
			}
			return result;
		}

		/// <summary>
		/// Keyframed root rotation. Poses are walked into one hemisphere FIRST, then each component gets the
		/// same tangent treatment as any other curve - so the shortest arc is chosen once, at the poses.
		/// </summary>
		private static List<(EditorCurveBinding binding, AnimationCurve curve)> BakeRootRotationKeyframed(
			PoseSequence sequence, Dictionary<AnimationClip, Dictionary<EditorCurveBinding, float>> cache,
			EditorCurveBinding[] rootBindings, float[] times)
		{
			int count = sequence.PoseCount;
			Vector4[] quaternions = new Vector4[count];

			for (int i = 0; i < count; i++)
			{
				Vector4 value = ReadQuaternion(cache, sequence.Get(i), rootBindings);
				if (value.sqrMagnitude > 0f)
				{
					value.Normalize();
				}

				if (i > 0 && Vector4.Dot(quaternions[i - 1], value) < 0f)
				{
					value = -value;
				}
				quaternions[i] = value;
			}

			var result = new List<(EditorCurveBinding, AnimationCurve)>(4);
			for (int c = 0; c < 4; c++)
			{
				float[] values = new float[count];
				for (int i = 0; i < count; i++)
				{
					values[i] = quaternions[i][c];
				}
				result.Add((rootBindings[c], BuildTangentedCurve(sequence, times, values)));
			}
			return result;
		}

		private static Vector4 ReadQuaternion(Dictionary<AnimationClip, Dictionary<EditorCurveBinding, float>> cache,
			IPose pose, EditorCurveBinding[] rootBindings)
		{
			Vector4 value = Vector4.zero;
			for (int c = 0; c < 4; c++)
			{
				TryValue(cache, pose, rootBindings[c], out float component);
				value[c] = component;
			}
			return value;
		}

		private static bool TryValue(Dictionary<AnimationClip, Dictionary<EditorCurveBinding, float>> cache,
			IPose pose, EditorCurveBinding binding, out float value)
		{
			value = 0f;
			return pose != null && pose.Clip != null &&
				cache.TryGetValue(pose.Clip, out var values) && values.TryGetValue(binding, out value);
		}

		/// <summary>Inherits root handling from the source poses, which the author configured deliberately.</summary>
		private static void CopyClipSettings(PoseSequence sequence, AnimationClip baked)
		{
			AnimationClip source = sequence.PoseCount > 0 ? sequence.Get(0).Clip : null;
			if (source == null)
			{
				return;
			}

			AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(source);
			settings.loopTime = false;
			settings.stopTime = baked.length;
			AnimationUtility.SetAnimationClipSettings(baked, settings);
		}

		#endregion Conversion

		/// <summary>
		/// Markers derived from the move's current values, so the timeline round-trips back to identical
		/// timings through the accessors on <see cref="PerformanceMove"/>.
		/// </summary>
		private static List<TimelineMarker> BuildMarkers(PerformanceMove move)
		{
			var markers = new List<TimelineMarker>();

			// The three phase regions chain end-to-end, mirroring PerformanceState. A converted sequence has
			// no lunge window, so Charging is zero-length at 0 - widen it by dragging Performing later.
			markers.Add(new TimelineMarker(TimelineMarkerIdentifiers.CHARGING, 0f, TimelineMarkerIdentifiers.PERFORMING));
			markers.Add(new TimelineMarker(TimelineMarkerIdentifiers.PERFORMING, 0f, TimelineMarkerIdentifiers.FINISHING));

			if (move is IMeleeCombatMove melee)
			{
				if (move.MinDuration > melee.HitDetectionDelay)
				{
					markers.Add(new TimelineMarker(TimelineMarkerIdentifiers.HIT, melee.HitDetectionDelay,
						move.MinDuration - melee.HitDetectionDelay));
				}

				if (melee.InertiaDelay > 0f)
				{
					markers.Add(new TimelineMarker(TimelineMarkerIdentifiers.INERTIA, melee.InertiaDelay));
				}
			}

			markers.Add(new TimelineMarker(TimelineMarkerIdentifiers.FINISHING, move.MinDuration, move.Release));
			return markers;
		}

		/// <summary>
		/// Carries the first pose's transition curve across as global data. It shapes how strongly the charge
		/// pose asserts itself over charge progress - the one piece of the sequence a baked clip cannot hold.
		/// </summary>
		private static LabeledPoseData BuildGlobalData(PoseSequence sequence)
		{
			LabeledPoseData data = new LabeledPoseData();
			if (sequence.PoseCount > 0 && sequence.Get(0).TransitionCurve != null)
			{
				data.SetCurve(AnimationFloatConstants.CHARGE_WEIGHT, sequence.Get(0).TransitionCurve);
			}
			return data;
		}

		private static T LoadOrCreate<T>(string path) where T : Object
		{
			T asset = AssetDatabase.LoadAssetAtPath<T>(path);
			if (asset != null)
			{
				return asset;
			}

			asset = typeof(T) == typeof(AnimationClip)
				? new AnimationClip() as T
				: ScriptableObject.CreateInstance(typeof(T)) as T;
			AssetDatabase.CreateAsset(asset, path);
			return asset;
		}
	}
}
