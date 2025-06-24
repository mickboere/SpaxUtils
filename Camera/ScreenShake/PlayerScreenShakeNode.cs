using UnityEngine;
using SpaxUtils.StateMachines;

namespace SpaxUtils
{
	public class PlayerScreenShakeNode : StateComponentNodeBase
	{
		[SerializeField] private ShakeSettings incomingHits;
		[SerializeField] private ShakeSettings outgoingHits;

		private CameraHandler cameraHandler;
		private IHittable hittable;
		private ICommunicationChannel comms;

		public void InjectDependencies(CameraHandler cameraHandler, IHittable hittable, ICommunicationChannel comms)
		{
			this.cameraHandler = cameraHandler;
			this.hittable = hittable;
			this.comms = comms;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();
			hittable.Subscribe(this, OnReceivedHit);
			comms.Listen<HitData>(this, OnLandedHit);
		}

		public override void OnStateExit()
		{
			base.OnStateExit();
			hittable.Unsubscribe(this);
			comms.StopListening(this);
		}

		private void OnReceivedHit(HitData hitData)
		{
			Send(hitData, incomingHits);
		}

		private void OnLandedHit(HitData hitData)
		{
			Send(hitData, outgoingHits);
		}

		private void Send(HitData hitData, ShakeSettings settings)
		{
			Vector3 magnitude = cameraHandler.transform.InverseTransformDirection(hitData.Direction).normalized.Multiply(settings.Magnitude);
			magnitude.z = settings.Magnitude.z * (hitData.Result_Force > 0f ? Mathf.Log10(hitData.Result_Force) : 1f);
			float frequency = settings.Frequency * hitData.Result_BlockedWeight.Invert().Clamp(0.5f, 1f);
			cameraHandler.ScreenShaker.Shake(magnitude, frequency, settings.Duration, settings.Falloff);
		}
	}
}
