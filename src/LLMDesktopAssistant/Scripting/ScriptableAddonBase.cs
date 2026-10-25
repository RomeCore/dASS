using LLMDesktopAssistant.Addons;

namespace LLMDesktopAssistant.Scripting
{
	/// <summary>
	/// An addon whose body is a script: a scriptable tool, slash command or prompt context.
	/// </summary>
	/// <remarks>
	/// The flags are ordinary addon properties: they are set by the parser before the addon is frozen and travel with
	/// <c>Clone()</c> into the per-chat copy, exactly like every other addon property. There is no nested settings
	/// object on purpose - a readonly property over a mutable class is a shared-state trap.
	/// </remarks>
	public abstract class ScriptableAddonBase<TAddon, TChange> : AddonChangedBase<TAddon, TChange>
		where TAddon : ScriptableAddonBase<TAddon, TChange>
		where TChange : AddonChangeBase
	{
		/// <summary>
		/// The language of the script, resolved from the file extension, or <see langword="null"/> for an addon
		/// that is not script-backed (a native tool created in code).
		/// </summary>
		public ScriptLanguageType? ScriptLanguage
		{
			get => field;
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// How the kind's variables reach the script.
		/// </summary>
		public ScriptInputMode InputMode
		{
			get => field;
			set => SetProperty(ref field, value);
		} = ScriptInputMode.Auto;

		/// <summary>
		/// Where the structured result is taken from.
		/// </summary>
		public ScriptOutputMode OutputMode
		{
			get => field;
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// The extraction pattern, only with <see cref="ScriptOutputMode.Pattern"/>: <c>"&lt;BEGIN&gt;*&lt;END&gt;"</c>,
		/// where <c>\*</c> is a literal asterisk.
		/// </summary>
		public string? OutputPattern
		{
			get => field;
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// Whether the addon runs in an interactive terminal instead of a managed process.
		/// </summary>
		public bool RunTerminal
		{
			get => field;
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// Whether a terminal run is awaited. <see langword="false"/> detaches it: the call returns with the output
		/// shown so far and the exit code reaches the card later.
		/// </summary>
		public bool TerminalWait
		{
			get => field;
			set => SetProperty(ref field, value);
		} = true;
	}
}
