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

		/// <summary>
		/// Timing as SERIALIZED on the move, bypassing its accessors. Converting a move that already carries a
		/// timeline would otherwise read back the very asset this pass is about to overwrite.
		/// </summary>
		private readonly struct AuthoredTiming
		{
			public bool HasCharge { get; }
			public bool HasPerformance { get; }
			public float MinDuration { get; }
			public float Release { get; }
			public bool Melee { get; }
			public float HitDetectionDelay { get; }
			public float InertiaDelay { get; }

			public AuthoredTiming(PerformanceMove move)
			{
				SerializedObject serialized = new SerializedObject(move);
				HasCharge = serialized.FindProperty("hasCharge").boolValue;
				HasPerformance = serialized.FindProperty("hasPerformance").boolValue;
				MinDuration = serialized.FindProperty("minDuration").floatValue;
				Release = serialized.FindProperty("release").floatValue;

				SerializedProperty hit = serialized.FindProperty("hitDetectionDelay");
				SerializedProperty inertia = serialized.FindProperty("inertiaDelay");
				Melee = hit != null && inertia != null;
				HitDetectionDelay = Melee ? hit.floatValue : 0f;
				InertiaDelay = Melee ? inertia.floatValue : 0f;
			}
		}

		/// <summary>
		/// One migration candidate. A Blocker means it cannot convert; a Warning means it can but almost
		/// certainly should not, so it starts unchecked rather than being swept along with a batch.
		/// </summary>
		private class Candidate
		{
			public PerformanceMove Move { get; }
			public PoseSequence Sequence { get; }
			public string Blocker { get; }
			public string Warning { get; }
			public bool Chosen { get; set; }

			public Candidate(PerformanceMove move, PoseSequence sequence, string blocker, string warning)
			{
				Move = move;
				Sequence = sequence;
				Blocker = blocker;
				Warning = warning;
				Chosen = blocker == null && warning == null;
			}
		}

		private PerformanceMove move;
		private BakeMode bakeMode = BakeMode.Keyframed;
		private float sampleRate = 60f;
		private DefaultAsset outputFolder;
		private Vector2 scroll;
		private string report;
		private bool batch;
		private Vector2 batchScroll;
		private List<Candidate> candidates;

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

			// Governs BOTH modes now: it is the baked clip's frame grid, which every key is snapped onto.
			sampleRate = EditorGUILayout.FloatField(
				new GUIContent("Frame Rate", "The baked clip's frame rate. Every key and the clip's length snap to this grid; in Resample mode it is also the sampling density."),
				sampleRate);
			outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder", outputFolder, typeof(DefaultAsset), false);
			if (EditorGUI.EndChangeCheck())
			{
				SaveSettings();
			}

			EditorGUILayout.Space(6);
			DrawBatch();

			PoseSequence sequence = move == null ? null : move.PosingData as PoseSequence;
			if (move == null)
			{
				EditorGUILayout.HelpBox("Assign a PerformanceMove. Its timing values become the markers.", MessageType.Info);
				DrawReport();
				return;
			}

			if (sequence == null)
			{
				EditorGUILayout.HelpBox("This move's PosingData is not a PoseSequence.", MessageType.Warning);
				DrawReport();
				return;
			}

			DrawPreview(sequence);

			EditorGUILayout.Space(6);
			using (new EditorGUI.DisabledScope(sampleRate <= 0f))
			{
				if (GUILayout.Button("Convert  (rebakes, resets markers)", GUILayout.Height(28f)))
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

			DrawRebake(sequence);

			DrawReport();
		}

		#region Batch

		private void DrawReport()
		{
			if (string.IsNullOrEmpty(report))
			{
				return;
			}

			EditorGUILayout.Space(4);
			scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(180f));
			EditorGUILayout.HelpBox(report, MessageType.None);
			EditorGUILayout.EndScrollView();
		}

		/// <summary>
		/// Whole-moveset migration. One move at a time is fine for authoring, but the migration itself is a
		/// dozen-plus moves and every one of them wants the same settings.
		/// </summary>
		private void DrawBatch()
		{
			batch = EditorGUILayout.Foldout(batch, "Batch", true, EditorStyles.foldoutHeader);
			if (!batch)
			{
				return;
			}

			if (candidates == null)
			{
				RefreshCandidates();
			}

			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button("Rescan", GUILayout.Width(70f)))
				{
					RefreshCandidates();
				}

				GUILayout.FlexibleSpace();
				GUILayout.Label($"{Chosen()} of {candidates.Count}", EditorStyles.miniLabel);
			}

			batchScroll = EditorGUILayout.BeginScrollView(batchScroll, GUILayout.MaxHeight(220f));
			foreach (Candidate candidate in candidates)
			{
				DrawCandidate(candidate);
			}
			EditorGUILayout.EndScrollView();

			// Two buttons, never one with a flag: converting REBAKES and rewrites markers, so bundling the
			// switch into it means flipping a move to Timeline would silently discard any marker you re-timed.
			using (new EditorGUILayout.HorizontalScope())
			{
				using (new EditorGUI.DisabledScope(Chosen() == 0 || sampleRate <= 0f))
				{
					if (GUILayout.Button($"Convert {Chosen()}  (rebakes, resets markers)", GUILayout.Height(24f)))
					{
						report = ConvertBatch();
						Debug.Log($"[PoseSequenceConverter] {report}");
						GUIUtility.ExitGUI();
					}
				}

				using (new EditorGUI.DisabledScope(Switchable() == 0))
				{
					if (GUILayout.Button($"Switch {Switchable()} to Timeline", GUILayout.Height(24f), GUILayout.Width(170f)))
					{
						report = SwitchBatch();
						Debug.Log($"[PoseSequenceConverter] {report}");
						GUIUtility.ExitGUI();
					}
				}
			}

			using (new EditorGUI.DisabledScope(Rebakable() == 0 || sampleRate <= 0f))
			{
				if (GUILayout.Button($"Re-bake {Rebakable()} clip(s)  (Resample, keeps markers)", GUILayout.Height(24f)))
				{
					report = RebakeBatch();
					Debug.Log($"[PoseSequenceConverter] {report}");
					GUIUtility.ExitGUI();
				}
			}

			EditorGUILayout.Space(4);
		}

		private void DrawCandidate(Candidate candidate)
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				using (new EditorGUI.DisabledScope(candidate.Blocker != null))
				{
					candidate.Chosen = EditorGUILayout.Toggle(candidate.Chosen, GUILayout.Width(16f));
				}

				if (GUILayout.Button(candidate.Move.name, EditorStyles.linkLabel, GUILayout.Width(190f)))
				{
					EditorGUIUtility.PingObject(candidate.Move);
				}

				if (candidate.Blocker != null)
				{
					EditorGUILayout.LabelField(candidate.Blocker, EditorStyles.miniLabel);
					return;
				}

				if (candidate.Warning != null)
				{
					Color previous = GUI.color;
					GUI.color = new Color(1f, 0.8f, 0.35f);
					EditorGUILayout.LabelField(candidate.Warning, EditorStyles.miniLabel);
					GUI.color = previous;
					return;
				}

				// Already-migrated moves stay listed and re-convertible; a re-bake overwrites in place.
				string state = candidate.Move.Timeline == null ? "no timeline" : candidate.Move.AnimationType.ToString();
				EditorGUILayout.LabelField($"{candidate.Sequence.name}   [{state}]", EditorStyles.miniLabel);
			}
		}

		private int Chosen()
		{
			int count = 0;
			foreach (Candidate candidate in candidates)
			{
				if (candidate.Chosen && candidate.Blocker == null)
				{
					count++;
				}
			}
			return count;
		}

		/// <summary>Checked moves whose timeline already has a clip, so its curves can be replaced in place.</summary>
		private int Rebakable()
		{
			int count = 0;
			foreach (Candidate candidate in candidates)
			{
				if (IsRebakable(candidate))
				{
					count++;
				}
			}
			return count;
		}

		private static bool IsRebakable(Candidate candidate)
		{
			return candidate.Chosen && candidate.Blocker == null && candidate.Move.Timeline != null &&
				candidate.Move.Timeline.Clip != null;
		}

		/// <summary>
		/// Re-bakes curves only. Separate from converting for the same reason switching is: a move that has
		/// had its markers re-timed by hand must never lose them to a fidelity fix.
		/// </summary>
		private string RebakeBatch()
		{
			int rebaked = 0;
			int failed = 0;
			string detail = string.Empty;

			foreach (Candidate candidate in candidates)
			{
				if (!IsRebakable(candidate))
				{
					continue;
				}

				string result;
				try
				{
					result = RebakeClip(candidate.Move, candidate.Sequence, sampleRate);
				}
				catch (System.Exception exception)
				{
					result = $"FAILED: {exception.GetType().Name}: {exception.Message}";
				}

				bool ok = !result.StartsWith("ABORTED") && !result.StartsWith("FAILED");
				if (ok)
				{
					rebaked++;
				}
				else
				{
					failed++;
				}

				detail += $"\n\n=== {candidate.Move.name} ===\n{result}";
			}

			return $"Re-baked {rebaked} clip(s) at Resample fidelity, {failed} failed. Markers untouched.{detail}";
		}

		/// <summary>Checked moves that already carry a timeline and are not already using it.</summary>
		private int Switchable()
		{
			int count = 0;
			foreach (Candidate candidate in candidates)
			{
				if (IsSwitchable(candidate))
				{
					count++;
				}
			}
			return count;
		}

		private static bool IsSwitchable(Candidate candidate)
		{
			return candidate.Chosen && candidate.Blocker == null && candidate.Move.Timeline != null &&
				candidate.Move.AnimationType != PerformanceAnimationType.Timeline;
		}

		/// <summary>
		/// Flips AnimationType only. Separate from converting so switching a move over never costs the marker
		/// timings you re-tuned after its bake.
		/// </summary>
		private string SwitchBatch()
		{
			int switched = 0;
			string names = string.Empty;

			foreach (Candidate candidate in candidates)
			{
				if (!IsSwitchable(candidate))
				{
					continue;
				}

				SwitchToTimeline(candidate.Move);
				names += $"\n  {candidate.Move.name}";
				switched++;
			}

			AssetDatabase.SaveAssets();
			return $"SWITCHED {switched} moves to Timeline. Nothing was rebaked.{names}";
		}

		/// <summary>Every move in the project, with the ones that cannot migrate listed and labelled, not hidden.</summary>
		private void RefreshCandidates()
		{
			candidates = new List<Candidate>();

			foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(PerformanceMove)}"))
			{
				PerformanceMove found = AssetDatabase.LoadAssetAtPath<PerformanceMove>(AssetDatabase.GUIDToAssetPath(guid));
				if (found == null)
				{
					continue;
				}

				PoseSequence sequence = found.PosingData as PoseSequence;
				string blocker = null;
				if (found.PosingData == null)
				{
					blocker = "no posing data";
				}
				else if (sequence == null)
				{
					blocker = $"{found.PosingData.GetType().Name}, not a PoseSequence";
				}

				// An Animator-driven move never played its posing data, so whatever sits in that field was
				// never validated against the creature - the Snail's held a humanoid sequence. Never batch these.
				string warning = blocker == null && found.AnimationType == PerformanceAnimationType.Animator
					? "Animator-driven; its posing data is not what plays it"
					: null;

				candidates.Add(new Candidate(found, sequence, blocker, warning));
			}

			candidates.Sort((a, b) => string.CompareOrdinal(a.Move.name, b.Move.name));
		}

		private string ConvertBatch()
		{
			int converted = 0;
			int failed = 0;
			string detail = string.Empty;

			foreach (Candidate candidate in candidates)
			{
				if (!candidate.Chosen || candidate.Blocker != null)
				{
					continue;
				}

				string result;
				try
				{
					result = Convert(candidate.Move, candidate.Sequence, bakeMode, sampleRate, FolderPath(candidate.Sequence));
				}
				catch (System.Exception exception)
				{
					result = $"FAILED: {exception.GetType().Name}: {exception.Message}";
				}

				// The converter reports its own refusals rather than throwing, so both prefixes mean "no output".
				bool ok = !result.StartsWith("ABORTED") && !result.StartsWith("FAILED");
				if (ok)
				{
					converted++;
				}
				else
				{
					failed++;
				}

				detail += $"\n\n--- {candidate.Move.name} ---\n{result}";
			}

			AssetDatabase.SaveAssets();
			return $"BATCH: {converted} converted, {failed} failed. AnimationType left untouched.{detail}";
		}

		private static void SwitchToTimeline(PerformanceMove move)
		{
			SerializedObject serialized = new SerializedObject(move);
			serialized.FindProperty("animationType").enumValueIndex = (int)PerformanceAnimationType.Timeline;
			serialized.ApplyModifiedProperties();
			EditorUtility.SetDirty(move);
		}

		#endregion Batch

		/// <summary>
		/// The fidelity fix: Keyframed can only approximate a transition curve with one cubic per segment, so
		/// a strongly shaped ease flattens. Re-baking at Resample replaces the curves and nothing else.
		/// </summary>
		private void DrawRebake(PoseSequence sequence)
		{
			EditorGUILayout.Space(2);

			AnimationTimeline timeline = move.Timeline;
			if (timeline == null || timeline.Clip == null)
			{
				EditorGUILayout.HelpBox("No timeline clip yet - Convert first, then re-bakes keep your markers.",
					MessageType.None);
				return;
			}

			// The bake reads the SEQUENCE, so any length the clip gained by hand is reverted - and since
			// markers are absolute, that silently re-times all of them. Worth knowing before, not after.
			float baked = ClipLength(sequence, sampleRate);
			if (!Mathf.Approximately(baked, timeline.Clip.length))
			{
				EditorGUILayout.HelpBox(
					$"Clip is {timeline.Clip.length:0.000}s but the sequence bakes to {baked:0.000}s. " +
					"Re-baking will change its length, and markers are absolute positions that will NOT move.",
					MessageType.Warning);
			}

			using (new EditorGUI.DisabledScope(sampleRate <= 0f))
			{
				if (GUILayout.Button($"Re-bake Clip  (Resample, keeps {timeline.Markers.Count} markers)",
					GUILayout.Height(24f)))
				{
					try
					{
						report = RebakeClip(move, sequence, sampleRate);
					}
					catch (System.Exception exception)
					{
						report = $"FAILED: {exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}";
					}

					Debug.Log($"[PoseSequenceConverter] {report}");
					GUIUtility.ExitGUI();
				}
			}
		}

		private void DrawPreview(PoseSequence sequence)
		{
			EditorGUILayout.Space(6);
			EditorGUILayout.LabelField("Will produce", EditorStyles.boldLabel);

			AuthoredTiming timing = new AuthoredTiming(move);
			float clipLength = ClipLength(sequence, sampleRate);
			EditorGUILayout.LabelField("Clip length", $"{clipLength:0.000}s  ({Mathf.RoundToInt(clipLength * sampleRate)} frames)");
			EditorGUILayout.LabelField("Sequence covers", $"{sequence.TotalDuration:0.000}s across {sequence.PoseCount} poses");
			EditorGUILayout.LabelField("Timeline extent", $"{Mathf.Max(clipLength, timing.MinDuration + timing.Release):0.000}s  (release sustains past the clip)");
			EditorGUILayout.LabelField("Timeline goes",
				move.Timeline != null ? AssetDatabase.GetAssetPath(move.Timeline) : "into the move as a child asset");

			foreach (TimelineMarker marker in BuildMarkers(timing, ChargeCurve(sequence)))
			{
				float end = marker.EndMode == MarkerEnd.Duration ? marker.Time + marker.Duration : marker.Time;
				string label = end > marker.Time ? $"{marker.Time:0.000} .. {end:0.000}s" : $"{marker.Time:0.000}s";
				EditorGUILayout.LabelField($"   {marker.ID}", label);
			}

			// The sequence's own timebase is independent of MinDuration, so a mismatch silently holds the
			// final pose (or truncates). Surfacing it here is half the reason this migration exists.
			if (timing.HasPerformance && !Mathf.Approximately(sequence.TotalDuration, timing.MinDuration))
			{
				EditorGUILayout.HelpBox(
					$"Sequence duration ({sequence.TotalDuration:0.000}s) differs from MinDuration ({timing.MinDuration:0.000}s). " +
					"The bake holds the final pose through the remainder, matching current runtime behaviour.",
					MessageType.Warning);
			}
		}

		#region Conversion

		/// <summary>
		/// The clip is the animation and nothing more. Release is a region sustaining past the last frame,
		/// not padding baked into the curves.
		/// </summary>
		private static float ClipLength(PoseSequence sequence, float rate)
		{
			return Quantize(sequence.TotalDuration, rate);
		}

		/// <summary>
		/// Rounds to the clip's frame grid. Pose durations are authored in arbitrary seconds, so their running
		/// sum lands between frames - a clip half a frame long, with keys Unity DISPLAYS on the frame it rounds to.
		/// </summary>
		private static float Quantize(float time, float rate)
		{
			return rate <= 0f ? time : Mathf.Round(time * rate) / rate;
		}

		private string FolderPath(PoseSequence sequence)
		{
			if (outputFolder != null)
			{
				return AssetDatabase.GetAssetPath(outputFolder);
			}
			return Path.GetDirectoryName(AssetDatabase.GetAssetPath(sequence)).Replace("\\", "/");
		}

		/// <summary>
		/// Every curve a sequence produces, built in memory ahead of any write so that a failed bake is a
		/// no-op rather than a wipe. <see cref="Abort"/> non-null means nothing may be written.
		/// </summary>
		private class BakedCurves
		{
			public readonly List<(EditorCurveBinding binding, AnimationCurve curve)> Pending =
				new List<(EditorCurveBinding, AnimationCurve)>();

			public float ClipLength;
			public int Frames;
			public int KeysPerCurve;
			public int Bindings;
			public int PoseClips;
			public bool Mirrored;
			public string Abort;
		}

		/// <summary>
		/// Bakes the sequence into curves without touching a single asset. Shared by the full conversion and
		/// the clip-only re-bake, so both can never drift in how they sample.
		/// </summary>
		private static BakedCurves Bake(PoseSequence sequence, BakeMode mode, float rate)
		{
			BakedCurves result = new BakedCurves();
			result.ClipLength = ClipLength(sequence, rate);
			float seqDuration = sequence.TotalDuration;

			// Round, not ceil: ClipLength is already on the grid, so ceil would add a stray trailing frame.
			result.Frames = Mathf.Max(1, Mathf.RoundToInt(result.ClipLength * rate));

			// Cache every pose clip's value per binding; pose clips hold a single keyframe at t=0.
			Dictionary<AnimationClip, Dictionary<EditorCurveBinding, float>> cache =
				BuildCache(sequence, out var bindings, out bool mirrored);
			result.Mirrored = mirrored;
			result.Bindings = bindings.Count;
			result.PoseClips = cache.Count;

			// Bail loudly rather than writing an empty clip, which reads as "it worked" until you open it.
			if (sequence.PoseCount == 0 || cache.Count == 0 || bindings.Count == 0)
			{
				result.Abort = $"ABORTED - nothing to bake.\n" +
					$"poses: {sequence.PoseCount}, pose clips resolved: {cache.Count}, curve bindings found: {bindings.Count}.\n" +
					(cache.Count == 0
						? "The sequence's poses have no AnimationClip assigned."
						: "The pose clips contain no readable curves (AnimationUtility.GetCurveBindings returned none).");
				return result;
			}

			float[] times = CumulativeTimes(sequence, rate);
			bool rootRotation = false;

			foreach (EditorCurveBinding binding in bindings)
			{
				if (binding.propertyName.StartsWith(ROOT_Q))
				{
					rootRotation = true;
					continue;
				}

				AnimationCurve curve = mode == BakeMode.Resample
					? ResampleCurve(sequence, cache, binding, rate, result.Frames, result.ClipLength, seqDuration)
					: KeyframeCurve(sequence, cache, binding, times);

				// A zero-key curve does not write an empty curve, it DELETES the binding.
				if (curve.length == 0)
				{
					continue;
				}

				result.KeysPerCurve = curve.length;
				result.Pending.Add((binding, curve));
			}

			if (rootRotation)
			{
				result.Pending.AddRange(BakeRootRotation(sequence, cache, mode, times, rate,
					result.Frames, result.ClipLength, seqDuration));
			}

			// Nothing usable? Leave whatever is already on disk completely alone.
			if (result.Pending.Count == 0)
			{
				result.Abort = $"ABORTED - produced no curves from {bindings.Count} bindings across {cache.Count} pose clips.\n" +
					"The existing asset was left untouched.";
			}

			return result;
		}

		/// <summary>
		/// Clears and rewrites a clip's curves. Only ever reached once <see cref="Bake"/> has confirmed it
		/// produced some, since ClearCurves is the destructive step.
		/// </summary>
		private static void WriteCurves(AnimationClip clip, BakedCurves baked, PoseSequence sequence, float rate)
		{
			clip.ClearCurves();
			foreach ((EditorCurveBinding binding, AnimationCurve curve) in baked.Pending)
			{
				AnimationUtility.SetEditorCurve(clip, binding, curve);
			}

			// The grid the keys were snapped onto has to be the clip's own, or every tool downstream
			// (the timeline ruler included) measures those keys against a different one.
			clip.frameRate = rate;
			CopyClipSettings(sequence, clip);
			EditorUtility.SetDirty(clip);
		}

		private static string Convert(PerformanceMove move, PoseSequence sequence, BakeMode mode, float rate, string folder)
		{
			AuthoredTiming timing = new AuthoredTiming(move);
			BakedCurves baked = Bake(sequence, mode, rate);
			if (baked.Abort != null)
			{
				return baked.Abort;
			}

			AnimationClip clip = LoadOrCreateClip($"{folder}/{sequence.name}_Baked.anim");
			WriteCurves(clip, baked, sequence, rate);

			// Into the move itself unless one is already assigned, so a bulk migration does not scatter
			// standalone assets that then need re-homing.
			AnimationTimeline timeline = TimelineOwnership.GetOrCreate(move);
			if (timeline == null)
			{
				return $"ABORTED - '{move.name}' could not be given a timeline. The clip was still written.";
			}

			timeline.Setup(clip, BuildMarkers(timing, ChargeCurve(sequence)));
			EditorUtility.SetDirty(timeline);
			EditorUtility.SetDirty(move);

			// SaveAssets only, mirroring ExtractPosesToClips. A Refresh here can reimport the clip out from
			// under the curves we just wrote.
			AssetDatabase.SaveAssets();

			// Read the asset BACK rather than trusting the write. This is the check that turns a silent
			// empty export into a reported failure.
			int persisted = AnimationUtility.GetCurveBindings(clip).Length;

			string warnings = baked.Mirrored
				? "\nWARNING: a pose has Mirror enabled. Mirroring is NOT baked - the result will be wrong."
				: string.Empty;
			warnings += mode == BakeMode.Keyframed ? DescribeCurveFidelity(sequence) : string.Empty;

			if (persisted < baked.Pending.Count)
			{
				warnings += $"\nERROR: wrote {baked.Pending.Count} curves but the asset reports {persisted}. " +
					"Re-run before relying on this clip.";
				Debug.LogError($"[PoseSequenceConverter] '{sequence.name}' did not persist: " +
					$"{baked.Pending.Count} written, {persisted} present.");
			}

			string density = mode == BakeMode.Resample
				? $"{baked.Frames + 1} keys per curve at {rate:0} fps"
				: $"{baked.KeysPerCurve} keys per curve (one per pose)";

			return $"Baked '{sequence.name}' [{mode}] -> {AssetDatabase.GetAssetPath(clip)}\n" +
				$"{density}, {baked.Pending.Count} curves built from {baked.Bindings} bindings, {persisted} persisted, " +
				$"clip length {baked.ClipLength:0.000}s (asset reports {clip.length:0.000}s).\n" +
				$"Timeline -> {AssetDatabase.GetAssetPath(timeline)} " +
				$"({(TimelineOwnership.IsOwned(move, timeline) ? "child of the move" : "shared standalone")})," +
				$" {timeline.Markers.Count} markers{warnings}";
		}

		/// <summary>
		/// Replaces ONLY the curve data of the clip a move's timeline already references, always at full
		/// Resample fidelity. Markers, clip identity and the timeline reference are left exactly as authored.
		/// </summary>
		private static string RebakeClip(PerformanceMove move, PoseSequence sequence, float rate)
		{
			AnimationTimeline timeline = move.Timeline;
			if (timeline == null || timeline.Clip == null)
			{
				return $"ABORTED - '{move.name}' has no timeline clip to re-bake. Convert it first.";
			}

			BakedCurves baked = Bake(sequence, BakeMode.Resample, rate);
			if (baked.Abort != null)
			{
				return baked.Abort;
			}

			AnimationClip clip = timeline.Clip;
			float previous = clip.length;
			WriteCurves(clip, baked, sequence, rate);
			AssetDatabase.SaveAssets();

			int persisted = AnimationUtility.GetCurveBindings(clip).Length;

			string warnings = baked.Mirrored
				? "\nWARNING: a pose has Mirror enabled. Mirroring is NOT baked - the result will be wrong."
				: string.Empty;

			// Markers are ABSOLUTE clip positions, so a length change silently re-times every one of them.
			if (!Mathf.Approximately(previous, clip.length))
			{
				warnings += $"\nWARNING: clip length changed {previous:0.000}s -> {clip.length:0.000}s. " +
					"Markers are absolute positions and did NOT move - re-check them.";
			}

			if (persisted < baked.Pending.Count)
			{
				warnings += $"\nERROR: wrote {baked.Pending.Count} curves but the asset reports {persisted}. " +
					"Re-run before relying on this clip.";
				Debug.LogError($"[PoseSequenceConverter] '{sequence.name}' did not persist: " +
					$"{baked.Pending.Count} written, {persisted} present.");
			}

			return $"Re-baked '{sequence.name}' [Resample] -> {AssetDatabase.GetAssetPath(clip)}\n" +
				$"{baked.Frames + 1} keys per curve at {rate:0} fps, {baked.Pending.Count} curves from " +
				$"{baked.Bindings} bindings, {persisted} persisted, clip length {clip.length:0.000}s.\n" +
				$"Timeline untouched - {timeline.Markers.Count} markers preserved.{warnings}";
		}

		/// <summary>
		/// Time at which each pose is reached - the running sum of pose durations, snapped to the frame grid.
		/// Snapping the SUM rather than each duration keeps the drift under half a frame however many poses there are.
		/// </summary>
		private static float[] CumulativeTimes(PoseSequence sequence, float rate)
		{
			float[] times = new float[sequence.PoseCount];
			float running = 0f;
			for (int i = 0; i < sequence.PoseCount; i++)
			{
				running += sequence.Get(i).Duration;
				times[i] = Quantize(running, rate);
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
		/// Markers derived from the move's authored values, so the timeline round-trips back to identical
		/// timings through the accessors on <see cref="PerformanceMove"/>.
		/// </summary>
		private static List<TimelineMarker> BuildMarkers(AuthoredTiming timing, AnimationCurve chargeCurve)
		{
			var markers = new List<TimelineMarker>();

			// A phase region's PRESENCE is what HasCharge/HasPerformance read, so emitting one the move does
			// not have would switch that phase on - guards and parries would silently gain a swing.
			if (timing.HasCharge)
			{
				TimelineMarker charging = timing.HasPerformance
					? new TimelineMarker(TimelineMarkerIdentifiers.CHARGING, 0f, TimelineMarkerIdentifiers.PERFORMING)
					: new TimelineMarker(TimelineMarkerIdentifiers.CHARGING, 0f);

				// The charge pose's transition curve becomes the region's own: it shapes how fast the
				// performer's weight moves into the charge, which is exactly what this marker governs.
				if (chargeCurve != null)
				{
					charging.SetCurve(chargeCurve);
				}

				markers.Add(charging);
			}

			if (!timing.HasPerformance)
			{
				return markers;
			}

			// A converted sequence has no lunge window, so Charging is zero-length at 0 - widen it by
			// dragging Performing later.
			markers.Add(new TimelineMarker(TimelineMarkerIdentifiers.PERFORMING, 0f, TimelineMarkerIdentifiers.FINISHING));

			if (timing.Melee)
			{
				if (timing.MinDuration > timing.HitDetectionDelay)
				{
					markers.Add(new TimelineMarker(TimelineMarkerIdentifiers.HIT, timing.HitDetectionDelay,
						timing.MinDuration - timing.HitDetectionDelay));
				}

				if (timing.InertiaDelay > 0f)
				{
					markers.Add(new TimelineMarker(TimelineMarkerIdentifiers.INERTIA, timing.InertiaDelay));
				}
			}

			markers.Add(new TimelineMarker(TimelineMarkerIdentifiers.FINISHING, timing.MinDuration, timing.Release));
			return markers;
		}

		/// <summary>
		/// The first pose's transition curve - how strongly the charge pose asserts itself over charge
		/// progress. The one piece of the sequence a baked clip cannot hold, so it rides on the CHARGING marker.
		/// </summary>
		private static AnimationCurve ChargeCurve(PoseSequence sequence)
		{
			return sequence.PoseCount > 0 ? sequence.Get(0).TransitionCurve : null;
		}

		private static AnimationClip LoadOrCreateClip(string path)
		{
			AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
			if (clip != null)
			{
				return clip;
			}

			clip = new AnimationClip();
			AssetDatabase.CreateAsset(clip, path);
			return clip;
		}
	}
}
