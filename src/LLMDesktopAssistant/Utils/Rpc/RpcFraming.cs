namespace LLMDesktopAssistant.Utils.Rpc
{
	/// <summary>
	/// The way JSON-RPC messages are delimited on a byte stream.
	/// </summary>
	/// <remarks>
	/// Two dialects exist in the wild: a <c>Content-Length</c> header block before every message (LSP)
	/// and one JSON object per line (MCP). <see cref="Auto"/> detects the dialect from the first frame
	/// the reader sees, which is only reliable when the peer speaks first; a client that sends before
	/// it reads writes <see cref="ContentLength"/> until the detection flips it.
	/// </remarks>
	public enum RpcFraming
	{
		/// <summary>
		/// Detect the framing from the first frame read from the peer.
		/// </summary>
		Auto,

		/// <summary>
		/// Every message is preceded by a <c>Content-Length</c> header block, as LSP requires.
		/// </summary>
		ContentLength,

		/// <summary>
		/// Every message is one line, as MCP requires.
		/// </summary>
		Lines
	}
}
