// HumanoidPoseEditor.cs -- place in any Editor/ folder
// Provides visual bone handles in the Scene View that write humanoid muscle values
// directly to the AnimationClip open in Unity's Animation Window.

using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Object = UnityEngine.Object;

public class HumanoidPoseEditor : EditorWindow
{
	// ---------------------------------------------
	//  STATE
	// ---------------------------------------------

	bool _active;
	bool _poseMode;
	Animator _animator;
	HumanPoseHandler _poseHandler;
	HumanBodyBones? _selectedBone = null;
	bool _rootSelected;
	Vector3 _rootDragHipPos;
	Quaternion _rootDragHipRot;
	Vector3 _rootDragHandlePos;
	bool _rootRotDragging;
	bool _rootPosDragging;
	Vector2 _scrollPos;

	// Settings
	bool _settingsFoldout;
	bool _showFingers;
	float _handleSize = 0.1f;
	float _selectedHandleSize = 0.15f;

	// Cached reflection for Animation Window
	static Type s_AnimWindowType;
	static PropertyInfo s_StateProp;
	static PropertyInfo s_ClipProp;
	static PropertyInfo s_TimeProp;
	static bool s_ReflectionReady;

	// ---------------------------------------------
	//  BONE TOPOLOGY
	// ---------------------------------------------

	static readonly (HumanBodyBones from, HumanBodyBones to)[] BoneConnections =
	{
        // Spine
        (HumanBodyBones.Hips,       HumanBodyBones.Spine),
		(HumanBodyBones.Spine,      HumanBodyBones.Chest),
		(HumanBodyBones.Chest,      HumanBodyBones.UpperChest),
		(HumanBodyBones.UpperChest, HumanBodyBones.Neck),
		(HumanBodyBones.Chest,      HumanBodyBones.Neck),            // fallback if no UpperChest
        (HumanBodyBones.Neck,       HumanBodyBones.Head),
        // Left arm
        (HumanBodyBones.UpperChest, HumanBodyBones.LeftShoulder),
		(HumanBodyBones.Chest,      HumanBodyBones.LeftShoulder),    // fallback
        (HumanBodyBones.LeftShoulder,  HumanBodyBones.LeftUpperArm),
		(HumanBodyBones.LeftUpperArm,  HumanBodyBones.LeftLowerArm),
		(HumanBodyBones.LeftLowerArm,  HumanBodyBones.LeftHand),
        // Right arm
        (HumanBodyBones.UpperChest, HumanBodyBones.RightShoulder),
		(HumanBodyBones.Chest,      HumanBodyBones.RightShoulder),   // fallback
        (HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm),
		(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm),
		(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand),
        // Left leg
        (HumanBodyBones.Hips,          HumanBodyBones.LeftUpperLeg),
		(HumanBodyBones.LeftUpperLeg,  HumanBodyBones.LeftLowerLeg),
		(HumanBodyBones.LeftLowerLeg,  HumanBodyBones.LeftFoot),
		(HumanBodyBones.LeftFoot,      HumanBodyBones.LeftToes),
        // Right leg
        (HumanBodyBones.Hips,          HumanBodyBones.RightUpperLeg),
		(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg),
		(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot),
		(HumanBodyBones.RightFoot,     HumanBodyBones.RightToes),
	};

	// Major bones (no fingers)
	static readonly HumanBodyBones[] MajorBones =
	{
		HumanBodyBones.Hips,
		HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest,
		HumanBodyBones.Neck, HumanBodyBones.Head,
		HumanBodyBones.LeftShoulder,  HumanBodyBones.LeftUpperArm,
		HumanBodyBones.LeftLowerArm,  HumanBodyBones.LeftHand,
		HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm,
		HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
		HumanBodyBones.LeftUpperLeg,  HumanBodyBones.LeftLowerLeg,
		HumanBodyBones.LeftFoot,      HumanBodyBones.LeftToes,
		HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg,
		HumanBodyBones.RightFoot,     HumanBodyBones.RightToes,
	};

	// Finger bones (togglable)
	static readonly HumanBodyBones[] FingerBones =
	{
		HumanBodyBones.LeftThumbProximal,  HumanBodyBones.LeftThumbIntermediate,  HumanBodyBones.LeftThumbDistal,
		HumanBodyBones.LeftIndexProximal,  HumanBodyBones.LeftIndexIntermediate,  HumanBodyBones.LeftIndexDistal,
		HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal,
		HumanBodyBones.LeftRingProximal,   HumanBodyBones.LeftRingIntermediate,   HumanBodyBones.LeftRingDistal,
		HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal,
		HumanBodyBones.RightThumbProximal,  HumanBodyBones.RightThumbIntermediate,  HumanBodyBones.RightThumbDistal,
		HumanBodyBones.RightIndexProximal,  HumanBodyBones.RightIndexIntermediate,  HumanBodyBones.RightIndexDistal,
		HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal,
		HumanBodyBones.RightRingProximal,   HumanBodyBones.RightRingIntermediate,   HumanBodyBones.RightRingDistal,
		HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal,
	};

	// ---------------------------------------------
	//  WINDOW SETUP
	// ---------------------------------------------

	[MenuItem("Tools/Humanoid Pose Editor")]
	static void Open() => GetWindow<HumanoidPoseEditor>("Pose Editor");

	void OnEnable()
	{
		SceneView.duringSceneGui += OnSceneGUI;
		EditorApplication.update += OnEditorUpdate;
		InitReflection();
	}

	void OnDisable()
	{
		SceneView.duringSceneGui -= OnSceneGUI;
		EditorApplication.update -= OnEditorUpdate;
		DisposePoseHandler();
	}

	void OnEditorUpdate()
	{
		if (_active)
			Repaint();
	}

	void DisposePoseHandler()
	{
		_poseHandler?.Dispose();
		_poseHandler = null;
	}

	void RebuildPoseHandler()
	{
		DisposePoseHandler();
		if (_animator != null && _animator.avatar != null && _animator.avatar.isHuman)
			_poseHandler = new HumanPoseHandler(_animator.avatar, _animator.transform);
	}

	// ---------------------------------------------
	//  EDITOR WINDOW GUI
	// ---------------------------------------------

	void OnGUI()
	{
		EditorGUILayout.Space(4);

		// --- Animator field ---
		EditorGUI.BeginChangeCheck();
		_animator = (Animator)EditorGUILayout.ObjectField("Animator", _animator, typeof(Animator), true);
		if (EditorGUI.EndChangeCheck())
			RebuildPoseHandler();

		// Auto-detect from selection
		if (_animator == null)
		{
			if (GUILayout.Button("Auto-detect from Selection"))
			{
				if (Selection.activeGameObject != null)
					_animator = Selection.activeGameObject.GetComponentInChildren<Animator>();
				if (_animator != null) RebuildPoseHandler();
			}
		}

		if (_animator == null)
		{
			EditorGUILayout.HelpBox("Assign an Animator with a Humanoid avatar.", MessageType.Info);
			return;
		}
		if (_animator.avatar == null || !_animator.avatar.isHuman)
		{
			EditorGUILayout.HelpBox("Animator must have a Humanoid avatar.", MessageType.Warning);
			return;
		}

		EditorGUILayout.Space(4);

		// --- Active toggle ---
		bool newActive = EditorGUILayout.Toggle("Enable Scene Handles", _active);
		if (newActive != _active)
		{
			_active = newActive;
			if (_active && _poseHandler == null) RebuildPoseHandler();
			SceneView.RepaintAll();
		}

		if (!_active) return;

		// --- Pose Mode toggle ---
		bool newPoseMode = EditorGUILayout.Toggle("Pose Mode (no clip)", _poseMode);
		if (newPoseMode != _poseMode)
		{
			_poseMode = newPoseMode;
			SceneView.RepaintAll();
		}

		EditorGUILayout.Space(4);

		// --- Animation Window status ---
		GetAnimationWindowState(out AnimationClip clip, out float time);
		if (!_poseMode)
		{
			if (clip != null)
			{
				EditorGUILayout.LabelField("Clip", clip.name);
				EditorGUILayout.LabelField("Time", $"{time:F3}s");
			}
			else
			{
				EditorGUILayout.HelpBox(
					"No AnimationClip detected. Open the Animation Window and select a clip, or enable Pose Mode.",
					MessageType.Warning);
			}
		}
		else
		{
			EditorGUILayout.HelpBox(
				"Pose Mode: scene edits only, no clip writes.",
				MessageType.Info);
		}

		EditorGUILayout.Space(4);

		// --- Settings ---
		_settingsFoldout = EditorGUILayout.Foldout(_settingsFoldout, "Settings", true);
		if (_settingsFoldout)
		{
			EditorGUI.indentLevel++;
			_handleSize = EditorGUILayout.Slider("Handle Size", _handleSize, 0.01f, 0.3f);
			_selectedHandleSize = EditorGUILayout.Slider("Selected Handle Size", _selectedHandleSize, 0.02f, 0.4f);
			_showFingers = EditorGUILayout.Toggle("Show Finger Handles", _showFingers);
			EditorGUI.indentLevel--;
		}

		EditorGUILayout.Space(4);

		// --- Selected bone info ---
		string boneName = _rootSelected ? "Root" :
			_selectedBone.HasValue ? _selectedBone.Value.ToString() : "None";
		EditorGUILayout.LabelField("Selected", boneName, EditorStyles.boldLabel);

		// Show muscle sliders for selected bone
		if (_selectedBone.HasValue)
			DrawMuscleSliders(clip, time);

		EditorGUILayout.Space(8);

		// --- Utility buttons ---
		if (!_poseMode && clip != null)
		{
			if (GUILayout.Button("Reset Pose to T-Pose"))
				ResetToTPose(clip, time);

			EditorGUILayout.Space(2);

			if (GUILayout.Button("Snap Full Pose to Clip"))
				SnapFullPoseToClip(clip, time);

			EditorGUILayout.Space(2);

			int poseCount = GetUniqueKeyframeTimes(clip).Count;
			EditorGUI.BeginDisabledGroup(poseCount < 2);
			if (GUILayout.Button($"Extract Poses ({poseCount})"))
				ExtractPosesToClips(clip);
			EditorGUI.EndDisabledGroup();
		}
	}

	// ---------------------------------------------
	//  MUSCLE SLIDERS (per-bone)
	// ---------------------------------------------

	void DrawMuscleSliders(AnimationClip clip, float time)
	{
		if (_poseHandler == null) return;

		int boneIndex = (int)_selectedBone.Value;
		HumanPose pose = new HumanPose();
		_poseHandler.GetHumanPose(ref pose);

		bool changed = false;

		_scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.MaxHeight(160));

		for (int dof = 0; dof < 3; dof++)
		{
			int muscleIdx = HumanTrait.MuscleFromBone(boneIndex, dof);
			if (muscleIdx < 0) continue;

			string name = HumanTrait.MuscleName[muscleIdx];
			float lo = HumanTrait.GetMuscleDefaultMin(muscleIdx);
			float hi = HumanTrait.GetMuscleDefaultMax(muscleIdx);
			float val = pose.muscles[muscleIdx];

			EditorGUI.BeginChangeCheck();
			val = EditorGUILayout.Slider(name, val, lo, hi);
			if (EditorGUI.EndChangeCheck())
			{
				pose.muscles[muscleIdx] = val;
				changed = true;
			}
		}

		EditorGUILayout.EndScrollView();

		if (changed)
		{
			if (_poseMode)
			{
				// Record all bone transforms for undo since SetHumanPose cascades
				var bones = new List<Object>();
				foreach (HumanBodyBones b in System.Enum.GetValues(typeof(HumanBodyBones)))
				{
					if (b == HumanBodyBones.LastBone) continue;
					Transform t = _animator.GetBoneTransform(b);
					if (t != null) bones.Add(t);
				}
				Undo.RecordObjects(bones.ToArray(), "Pose Muscle Change");
			}

			_poseHandler.SetHumanPose(ref pose);
			if (!_poseMode && clip != null)
				WritePoseToClip(pose, clip, time, _selectedBone.Value);
			SceneView.RepaintAll();
		}
	}

	// ---------------------------------------------
	//  SCENE VIEW DRAWING
	// ---------------------------------------------

	void OnSceneGUI(SceneView sv)
	{
		if (!_active || _animator == null || _poseHandler == null) return;

		DrawSkeleton();
		DrawRootHandle();
		DrawBoneButtons();
		DrawSelectedBoneHandle();
		DrawSelectedRootHandle();

		// Deselect when clicking empty space (no handle claimed the click)
		Event e = Event.current;
		if (e.type == EventType.MouseDown && e.button == 0
			&& GUIUtility.hotControl == 0
			&& !e.alt
			&& (_selectedBone.HasValue || _rootSelected))
		{
			_selectedBone = null;
			_rootSelected = false;
			Repaint();
			e.Use();
		}
	}

	void DrawSkeleton()
	{
		Handles.color = new Color(0f, 1f, 1f, 0.5f);
		bool hasUpperChest = _animator.GetBoneTransform(HumanBodyBones.UpperChest) != null;

		foreach (var (from, to) in BoneConnections)
		{
			// Handle fallback connections: skip UpperChest links if missing,
			// skip Chest->Neck / Chest->Shoulder fallbacks if UpperChest exists.
			if (!hasUpperChest && (from == HumanBodyBones.UpperChest || to == HumanBodyBones.UpperChest))
				continue;
			if (hasUpperChest && from == HumanBodyBones.Chest &&
				(to == HumanBodyBones.Neck || to == HumanBodyBones.LeftShoulder || to == HumanBodyBones.RightShoulder))
				continue;

			Transform a = _animator.GetBoneTransform(from);
			Transform b = _animator.GetBoneTransform(to);
			if (a != null && b != null)
				Handles.DrawLine(a.position, b.position, 2f);
		}

		// Finger connections (if toggled)
		if (_showFingers)
		{
			Handles.color = new Color(0f, 1f, 1f, 0.25f);
			for (int i = 0; i < FingerBones.Length; i++)
			{
				Transform t = _animator.GetBoneTransform(FingerBones[i]);
				if (t == null || t.parent == null) continue;
				Handles.DrawLine(t.parent.position, t.position, 1f);
			}
		}
	}

	void DrawBoneButtons()
	{
		// Draw clickable sphere on each bone
		var bones = _showFingers ? CombineBones() : MajorBones;

		foreach (var bone in bones)
		{
			Transform t = _animator.GetBoneTransform(bone);
			if (t == null) continue;

			float baseSize = HandleUtility.GetHandleSize(t.position);
			bool isSelected = _selectedBone.HasValue && bone == _selectedBone.Value;

			Handles.color = isSelected
				? new Color(1f, 0.6f, 0f, 1f)   // orange for selected
				: new Color(1f, 1f, 0f, 0.7f);   // yellow for others

			float sphereSize = isSelected ? _selectedHandleSize : _handleSize;

			if (Handles.Button(t.position, Quaternion.identity,
					baseSize * sphereSize, baseSize * sphereSize,
					Handles.SphereHandleCap))
			{
				_selectedBone = bone;
				_rootSelected = false;
				Repaint();
			}
		}
	}

	void DrawRootHandle()
	{
		Vector3 pos = _animator.transform.position;
		float baseSize = HandleUtility.GetHandleSize(pos);

		Handles.color = _rootSelected
			? new Color(1f, 0.2f, 0.2f, 1f)    // red for selected
			: new Color(1f, 0.4f, 0.4f, 0.7f);  // dim red for unselected

		float sphereSize = _rootSelected ? _selectedHandleSize : _handleSize;

		if (Handles.Button(pos, Quaternion.identity,
				baseSize * sphereSize, baseSize * sphereSize,
				Handles.SphereHandleCap))
		{
			_rootSelected = true;
			_selectedBone = null;
			Repaint();
		}
	}

	void DrawSelectedBoneHandle()
	{
		if (!_selectedBone.HasValue) return;
		Transform bone = _animator.GetBoneTransform(_selectedBone.Value);
		if (bone == null) return;

		GetAnimationWindowState(out AnimationClip clip, out float time);

		if (Tools.current == Tool.Rotate)
		{
			EditorGUI.BeginChangeCheck();
			Quaternion newRot = Handles.RotationHandle(bone.rotation, bone.position);
			if (EditorGUI.EndChangeCheck())
			{
				if (_poseMode) Undo.RecordObject(bone, "Pose Bone Rotation");
				bone.rotation = newRot;
				ReadAndWritePose(clip, time);
			}
		}
		else if (Tools.current == Tool.Move && _selectedBone.Value == HumanBodyBones.Hips)
		{
			EditorGUI.BeginChangeCheck();
			Vector3 newPos = Handles.PositionHandle(bone.position, bone.rotation);
			if (EditorGUI.EndChangeCheck())
			{
				if (_poseMode) Undo.RecordObject(bone, "Pose Hips Position");
				bone.position = newPos;
				ReadAndWritePose(clip, time);
			}
		}
	}

	void DrawSelectedRootHandle()
	{
		if (!_rootSelected) return;

		Transform hipBone = _animator.GetBoneTransform(HumanBodyBones.Hips);
		if (hipBone == null) return;

		GetAnimationWindowState(out AnimationClip clip, out float time);

		// Handle is drawn at the animator transform (feet), but moves the hip bone
		Vector3 handlePos = _animator.transform.position;

		if (Tools.current == Tool.Move)
		{
			if (GUIUtility.hotControl == 0)
				_rootPosDragging = false;
			if (!_rootPosDragging)
			{
				_rootDragHipPos = hipBone.position;
				_rootDragHandlePos = handlePos;
			}

			EditorGUI.BeginChangeCheck();
			Vector3 newPos = Handles.PositionHandle(handlePos, Quaternion.identity);
			if (EditorGUI.EndChangeCheck())
			{
				_rootPosDragging = true;
				Vector3 delta = newPos - _rootDragHandlePos;
				if (_poseMode) Undo.RecordObject(hipBone, "Pose Root Position");
				hipBone.position = _rootDragHipPos + delta;
				ReadAndWriteRoot(clip, time);
			}
		}
		else if (Tools.current == Tool.Rotate)
		{
			if (GUIUtility.hotControl == 0)
				_rootRotDragging = false;
			if (!_rootRotDragging)
			{
				_rootDragHipPos = hipBone.position;
				_rootDragHipRot = hipBone.rotation;
				_rootDragHandlePos = handlePos;
			}

			EditorGUI.BeginChangeCheck();
			Quaternion newRot = Handles.RotationHandle(Quaternion.identity, handlePos);
			if (EditorGUI.EndChangeCheck())
			{
				_rootRotDragging = true;
				if (_poseMode) Undo.RecordObject(hipBone, "Pose Root Rotation");
				// Apply cumulative rotation to cached start values
				hipBone.position = newRot * (_rootDragHipPos - _rootDragHandlePos) + _rootDragHandlePos;
				hipBone.rotation = newRot * _rootDragHipRot;
				ReadAndWriteRoot(clip, time);
			}
		}
	}

	void ReadAndWriteRoot(AnimationClip clip, float time)
	{
		if (_poseHandler == null) return;

		if (_poseMode)
		{
			Repaint();
			return;
		}

		HumanPose pose = new HumanPose();
		_poseHandler.GetHumanPose(ref pose);

		if (clip != null)
		{
			Undo.RecordObject(clip, "Pose Editor: Write Root");
			WriteRootPositionToClip(pose, clip, time);
			WriteRootRotationToClip(pose, clip, time);
		}

		Repaint();
	}

	// ---------------------------------------------
	//  POSE <-> CLIP
	// ---------------------------------------------

	void WriteRootPositionToClip(HumanPose pose, AnimationClip clip, float time)
	{
		WriteCurveValue(clip, "RootT.x", time, pose.bodyPosition.x);
		WriteCurveValue(clip, "RootT.y", time, pose.bodyPosition.y);
		WriteCurveValue(clip, "RootT.z", time, pose.bodyPosition.z);
		EditorUtility.SetDirty(clip);
		ResampleClip(clip, time);
	}

	void WriteRootRotationToClip(HumanPose pose, AnimationClip clip, float time)
	{
		WriteCurveValue(clip, "RootQ.x", time, pose.bodyRotation.x);
		WriteCurveValue(clip, "RootQ.y", time, pose.bodyRotation.y);
		WriteCurveValue(clip, "RootQ.z", time, pose.bodyRotation.z);
		WriteCurveValue(clip, "RootQ.w", time, pose.bodyRotation.w);
		EditorUtility.SetDirty(clip);
		ResampleClip(clip, time);
	}

	void ResampleClip(AnimationClip clip, float time)
	{
		if (_animator == null || clip == null) return;
		if (AnimationMode.InAnimationMode())
			AnimationMode.SampleAnimationClip(_animator.gameObject, clip, time);
	}

	void ReadAndWritePose(AnimationClip clip, float time)
	{
		if (_poseHandler == null) return;

		if (_poseMode)
		{
			Repaint();
			return;
		}

		HumanPose pose = new HumanPose();
		_poseHandler.GetHumanPose(ref pose);

		if (clip != null && _selectedBone.HasValue)
			WritePoseToClip(pose, clip, time, _selectedBone.Value);

		Repaint();
	}

	void WritePoseToClip(HumanPose pose, AnimationClip clip, float time, HumanBodyBones editedBone)
	{
		Undo.RecordObject(clip, "Pose Editor: Write Muscles");

		int boneIndex = (int)editedBone;

		for (int i = 0; i < pose.muscles.Length; i++)
		{
			string muscleName = HumanTrait.MuscleName[i];

			// Always write muscles belonging to the edited bone.
			bool isBoneMuscle = false;
			for (int dof = 0; dof < 3; dof++)
			{
				if (HumanTrait.MuscleFromBone(boneIndex, dof) == i)
				{
					isBoneMuscle = true;
					break;
				}
			}

			if (isBoneMuscle)
			{
				WriteCurveValue(clip, muscleName, time, pose.muscles[i]);
				continue;
			}

			// For other muscles, only update if a curve already exists in the clip.
			var binding = EditorCurveBinding.FloatCurve("", typeof(Animator), muscleName);
			if (AnimationUtility.GetEditorCurve(clip, binding) != null)
				WriteCurveValue(clip, muscleName, time, pose.muscles[i]);
		}

		// Write body position/rotation
		WriteCurveValue(clip, "RootT.x", time, pose.bodyPosition.x);
		WriteCurveValue(clip, "RootT.y", time, pose.bodyPosition.y);
		WriteCurveValue(clip, "RootT.z", time, pose.bodyPosition.z);
		WriteCurveValue(clip, "RootQ.x", time, pose.bodyRotation.x);
		WriteCurveValue(clip, "RootQ.y", time, pose.bodyRotation.y);
		WriteCurveValue(clip, "RootQ.z", time, pose.bodyRotation.z);
		WriteCurveValue(clip, "RootQ.w", time, pose.bodyRotation.w);

		EditorUtility.SetDirty(clip);
	}

	void WriteAllMusclesToClip(HumanPose pose, AnimationClip clip, float time)
	{
		Undo.RecordObject(clip, "Pose Editor: Write All Muscles");

		for (int i = 0; i < pose.muscles.Length; i++)
			WriteCurveValue(clip, HumanTrait.MuscleName[i], time, pose.muscles[i]);

		WriteCurveValue(clip, "RootT.x", time, pose.bodyPosition.x);
		WriteCurveValue(clip, "RootT.y", time, pose.bodyPosition.y);
		WriteCurveValue(clip, "RootT.z", time, pose.bodyPosition.z);
		WriteCurveValue(clip, "RootQ.x", time, pose.bodyRotation.x);
		WriteCurveValue(clip, "RootQ.y", time, pose.bodyRotation.y);
		WriteCurveValue(clip, "RootQ.z", time, pose.bodyRotation.z);
		WriteCurveValue(clip, "RootQ.w", time, pose.bodyRotation.w);

		EditorUtility.SetDirty(clip);
	}

	void WriteCurveValue(AnimationClip clip, string propertyName, float time, float value)
	{
		var binding = EditorCurveBinding.FloatCurve("", typeof(Animator), propertyName);
		AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);

		if (curve == null)
			curve = new AnimationCurve();

		// Find existing key at this time (within tolerance) and replace, or add new
		int existingKey = -1;
		for (int k = 0; k < curve.keys.Length; k++)
		{
			if (Mathf.Abs(curve.keys[k].time - time) < 0.0001f)
			{
				existingKey = k;
				break;
			}
		}

		if (existingKey >= 0)
			curve.MoveKey(existingKey, new Keyframe(time, value));
		else
			curve.AddKey(new Keyframe(time, value));

		AnimationUtility.SetEditorCurve(clip, binding, curve);
	}

	// ---------------------------------------------
	//  UTILITY
	// ---------------------------------------------

	void ResetToTPose(AnimationClip clip, float time)
	{
		if (_poseHandler == null) return;

		Undo.RecordObject(clip, "Pose Editor: Reset T-Pose");

		HumanPose pose = new HumanPose();
		pose.bodyPosition = new Vector3(0, 1, 0);
		pose.bodyRotation = Quaternion.identity;
		pose.muscles = new float[HumanTrait.MuscleCount];

		_poseHandler.SetHumanPose(ref pose);
		WriteAllMusclesToClip(pose, clip, time);
		SceneView.RepaintAll();
	}

	void SnapFullPoseToClip(AnimationClip clip, float time)
	{
		// Reads the current character pose and writes it fully to the clip.
		// Useful after manually tweaking transforms or loading from another source.
		if (_poseHandler == null) return;

		HumanPose pose = new HumanPose();
		_poseHandler.GetHumanPose(ref pose);
		WriteAllMusclesToClip(pose, clip, time);
	}

	List<float> GetUniqueKeyframeTimes(AnimationClip clip)
	{
		var times = new HashSet<float>();
		var bindings = AnimationUtility.GetCurveBindings(clip);
		foreach (var binding in bindings)
		{
			AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
			if (curve == null) continue;
			foreach (var key in curve.keys)
				times.Add(Mathf.Round(key.time * 10000f) / 10000f);
		}
		var sorted = times.ToList();
		sorted.Sort();
		return sorted;
	}

	void ExtractPosesToClips(AnimationClip sourceClip)
	{
		if (_animator == null || _poseHandler == null) return;

		string sourcePath = AssetDatabase.GetAssetPath(sourceClip);
		if (string.IsNullOrEmpty(sourcePath)) return;

		string directory = Path.GetDirectoryName(sourcePath);
		string baseName = Path.GetFileNameWithoutExtension(sourcePath);

		var times = GetUniqueKeyframeTimes(sourceClip);
		if (times.Count < 2) return;

		var bindings = AnimationUtility.GetCurveBindings(sourceClip);

		for (int i = 0; i < times.Count; i++)
		{
			float t = times[i];
			string clipName = baseName + (i + 1);
			string clipPath = Path.Combine(directory, clipName + ".anim").Replace("\\", "/");

			// Load existing or create new
			AnimationClip targetClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
			if (targetClip == null)
			{
				targetClip = new AnimationClip();
				AssetDatabase.CreateAsset(targetClip, clipPath);
			}

			// Clear existing curves
			targetClip.ClearCurves();

			// Copy each curve's value at this time into a single keyframe at time 0
			foreach (var binding in bindings)
			{
				AnimationCurve sourceCurve = AnimationUtility.GetEditorCurve(sourceClip, binding);
				if (sourceCurve == null) continue;

				float value = sourceCurve.Evaluate(t);
				AnimationCurve targetCurve = new AnimationCurve(new Keyframe(0f, value));
				AnimationUtility.SetEditorCurve(targetClip, binding, targetCurve);
			}

			// Apply clip settings
			AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(targetClip);
			settings.loopTime = false;
			settings.loopBlend = false;
			settings.loopBlendOrientation = true;
			settings.loopBlendPositionY = true;
			settings.loopBlendPositionXZ = true;
			settings.keepOriginalOrientation = true;
			settings.keepOriginalPositionY = true;
			settings.keepOriginalPositionXZ = true;
			AnimationUtility.SetAnimationClipSettings(targetClip, settings);

			EditorUtility.SetDirty(targetClip);
		}

		AssetDatabase.SaveAssets();
		Debug.Log($"[Pose Editor] Extracted {times.Count} poses from '{sourceClip.name}'");
	}

	HumanBodyBones[] CombineBones()
	{
		var list = new HumanBodyBones[MajorBones.Length + FingerBones.Length];
		MajorBones.CopyTo(list, 0);
		FingerBones.CopyTo(list, MajorBones.Length);
		return list;
	}

	// ---------------------------------------------
	//  ANIMATION WINDOW REFLECTION
	// ---------------------------------------------

	static void InitReflection()
	{
		if (s_ReflectionReady) return;
		try
		{
			s_AnimWindowType = typeof(Editor).Assembly.GetType("UnityEditor.AnimationWindow");
			if (s_AnimWindowType == null) return;

			// AnimationWindow exposes 'state' internally
			s_StateProp = s_AnimWindowType.GetProperty("state",
				BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

			if (s_StateProp != null)
			{
				Type stateType = s_StateProp.PropertyType;
				s_ClipProp = stateType.GetProperty("activeAnimationClip",
					BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				s_TimeProp = stateType.GetProperty("currentTime",
					BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			}

			s_ReflectionReady = true;
		}
		catch (Exception e)
		{
			Debug.LogWarning($"[PoseEditor] Animation Window reflection failed: {e.Message}");
		}
	}

	void GetAnimationWindowState(out AnimationClip clip, out float time)
	{
		clip = null;
		time = 0f;

		if (!s_ReflectionReady || s_AnimWindowType == null) return;

		try
		{
			var windows = Resources.FindObjectsOfTypeAll(s_AnimWindowType);
			if (windows.Length == 0) return;

			var window = windows[0];
			var state = s_StateProp?.GetValue(window);
			if (state == null) return;

			clip = s_ClipProp?.GetValue(state) as AnimationClip;
			var rawTime = s_TimeProp?.GetValue(state);
			if (rawTime != null) time = Convert.ToSingle(rawTime);
		}
		catch { /* swallow -- reflection can be fragile across versions */ }
	}
}
