namespace LLMDesktopAssistant.Utils.Rpc
{
	/// <summary>
	/// What a transport provider needs to open a channel: the target, and the settings a process-shaped
	/// transport cares about.
	/// </summary>
	/// <remarks>
	/// The target is a string every provider interprets for itself - a quoted-free executable with its
	/// arguments for <c>stdio</c>, <c>host:port</c> for <c>tcp</c> - because the two have nothing in common
	/// beyond "where the peer is".
	/// </remarks>
	public sealed class RpcTransportRequest
	{
		/// <summary>
		/// Gets what the transport connects to.
		/// </summary>
		public required string Target { get; init; }

		/// <summary>
		/// Gets the arguments of a process-shaped peer, one array element per argument.
		/// </summary>
		/// <remarks>
		/// The provider escapes each element for the platform. When <see cref="CommandLine"/> is set this list
		/// is ignored, because the caller already wrote the whole argument string.
		/// </remarks>
		public IReadOnlyList<string> Arguments { get; init; } = [];

		/// <summary>
		/// Gets the arguments of a process-shaped peer as one command line, or <see langword="null"/> when they
		/// are given as <see cref="Arguments"/>.
		/// </summary>
		/// <remarks>
		/// Passed to the process as is, so the caller's quoting is what the program parses.
		/// </remarks>
		public string? CommandLine { get; init; }

		/// <summary>
		/// Gets the working directory of a process-shaped peer, or <see langword="null"/> to use the
		/// application's current one.
		/// </summary>
		public string? WorkingDirectory { get; init; }

		/// <summary>
		/// Gets the extra environment variables of a process-shaped peer; a <see langword="null"/> value
		/// removes one.
		/// </summary>
		public IReadOnlyDictionary<string, string?> Environment { get; init; } =
			new Dictionary<string, string?>();
	}
}
