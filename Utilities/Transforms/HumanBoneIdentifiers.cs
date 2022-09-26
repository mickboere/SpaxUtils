using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Collection of bone identifiers, implements <see cref="ITransformLookupIdentifiers"/>.
	/// All entries in this identifier collection should correspond to a <see cref="HumanBodyBones"/>.
	/// </summary>
	public class HumanBoneIdentifiers : ITransformLookupIdentifiers
	{
		public const string HIPS = "Hips"; // This is the Hips bone.
		public const string LEFT_UPPER_LEG = "LeftUpperLeg"; // This is the Left Upper Leg bone.
		public const string RIGHT_UPPER_LEG = "RightUpperLeg"; // 	This is the Right Upper Leg bone.
															   //LeftLowerLeg // This is the Left Knee bone.
															   //RightLowerLeg // This is the Right Knee bone.
		public const string LEFT_FOOT = "LeftFoot"; // This is the Left Ankle bone.
		public const string RIGHT_FOOT = "RightFoot"; // This is the Right Ankle bone.
													  //Spine // This is the first Spine bone.
													  //Chest // This is the Chest bone.
													  //UpperChest // This is the Upper Chest bone.
													  //Neck // This is the Neck bone.
		public const string HEAD = "Head"; // This is the Head bone.
		public const string LEFT_SHOULDER = "LeftShoulder"; // This is the Left Shoulder bone.
		public const string RIGHT_SHOULDER = "RightShoulder"; // This is the Right Shoulder bone.
		public const string LEFT_UPPER_ARM = "LeftUpperArm"; // This is the Left Upper Arm bone.
		public const string RIGHT_UPPER_ARM = "RightUpperArm"; // This is the Right Upper Arm bone.
		public const string LEFT_LOWER_ARM = "LeftLowerArm"; // This is the Left Elbow bone.
		public const string RIGHT_LOWER_ARM = "RightLowerArm"; // This is the Right Elbow bone.
		public const string LEFT_HAND = "LeftHand"; // This is the Left Wrist bone.
		public const string RIGHT_HAND = "RightHand"; // This is the Right Wrist bone.
													  //LeftToes // This is the Left Toes bone.
													  //RightToes // This is the Right Toes bone.
													  //LeftEye // This is the Left Eye bone.
													  //RightEye // This is the Right Eye bone.
													  //Jaw // This is the Jaw bone.
		public const string LEFT_THUMB_PROXIMAL = "LeftThumbProximal"; // This is the left thumb 1st phalange.
																	   //LeftThumbIntermediate // This is the left thumb 2nd phalange.
																	   //LeftThumbDistal // This is the left thumb 3rd phalange.
																	   //LeftIndexProximal // This is the left index 1st phalange.
																	   //LeftIndexIntermediate // This is the left index 2nd phalange.
																	   //LeftIndexDistal // This is the left index 3rd phalange.
		public const string LEFT_MIDDLE_PROXIMAL = "LeftMiddleProximal"; // This is the left middle 1st phalange.
																		 //LeftIndexIntermediate // This is the left index 2nd phalange.
																		 //LeftIndexDistal // This is the left index 3rd phalange.
																		 //LeftMiddleProximal // This is the left middle 1st phalange.
																		 //LeftMiddleIntermediate // This is the left middle 2nd phalange.
																		 //LeftMiddleDistal // This is the left middle 3rd phalange.
																		 //LeftRingProximal // This is the left ring 1st phalange.
																		 //LeftRingIntermediate // This is the left ring 2nd phalange.
																		 //LeftRingDistal // This is the left ring 3rd phalange.
																		 //LeftLittleProximal // This is the left little 1st phalange.
																		 //LeftLittleIntermediate // This is the left little 2nd phalange.
																		 //LeftLittleDistal // This is the left little 3rd phalange.
		public const string RIGHT_THUMB_PROXIMAL = "RightThumbProximal"; // This is the right thumb 1st phalange.
																		 //RightThumbIntermediate // This is the right thumb 2nd phalange.
																		 //RightThumbDistal // This is the right thumb 3rd phalange.
																		 //RightIndexProximal // This is the right index 1st phalange.
																		 //RightIndexIntermediate // This is the right index 2nd phalange.
																		 //RightIndexDistal // This is the right index 3rd phalange.
		public const string RIGHT_MIDDLE_PROXIMAL = "RightMiddleProximal"; // This is the right middle 1st phalange.
																		   //RightMiddleIntermediate // This is the right middle 2nd phalange.
																		   //RightMiddleDistal // This is the right middle 3rd phalange.
																		   //RightRingProximal // This is the right ring 1st phalange.
																		   //RightRingIntermediate // This is the right ring 2nd phalange.
																		   //RightRingDistal // This is the right ring 3rd phalange.
																		   //RightLittleProximal // This is the right little 1st phalange.
																		   //RightLittleIntermediate // This is the right little 2nd phalange.
																		   //RightLittleDistal // This is the right little 3rd phalange.
																		   //LastBone // This is the Last bone index delimiter.
	}
}