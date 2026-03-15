using System;

namespace SpaxUtils
{
	/// <summary>
	/// Single-shot <see cref="IAct"/> implementation with no value or two-stage input.
	/// Used for actions that should prepare and perform immediately upon receipt.
	/// </summary>
	public struct ActSignal : IAct
	{
		public const float DEFAULT_BUFFER = 0.15f;

		public string Title { get; }
		public bool Interuptable { get; }
		public bool Interuptor { get; }
		public float Buffer { get; }
		public Action<IPerformer> Callback { get; }

		public ActSignal(string title, bool interuptable = false, bool interuptor = true,
			float buffer = DEFAULT_BUFFER, Action<IPerformer> callback = null)
		{
			Title = title;
			Interuptable = interuptable;
			Interuptor = interuptor;
			Buffer = buffer;
			Callback = callback;
		}

		public override string ToString()
		{
			return $"ActSignal(\"{Title}\", interuptable={Interuptable}, interuptor={Interuptor}, buffer={Buffer})";
		}
	}
}
