namespace LLMDesktopAssistant.Data.Connectors
{
	/// <summary>
	/// A lease on a cached database connection: one user's claim on it, which decides how long the
	/// connection may live and who is allowed to close it.
	/// </summary>
	/// <remarks>
	/// The cache is application-wide, so the same connection is shared by every chat that names it.
	/// A lease therefore never closes the connection on its own: <see cref="DisposeAsync"/> only drops
	/// the claim, while <see cref="CloseAsync"/> is the explicit, permission-checked close that succeeds
	/// for the last remaining user or with <c>force</c>.
	/// </remarks>
	public interface IDatabaseConnectionLease : IAsyncDisposable
	{
		/// <summary>
		/// Gets the type of the database connector behind this lease.
		/// </summary>
		DatabaseConnectorType Type { get; }

		/// <summary>
		/// Gets the connection string this lease was acquired with.
		/// </summary>
		string ConnectionString { get; }

		/// <summary>
		/// Resolves the connection again, opening it when it is missing or was closed by someone else,
		/// and returns it ready to use.
		/// </summary>
		/// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
		/// <returns>The shared connection of this lease.</returns>
		Task<IDatabaseConnection> EnsureAsync(CancellationToken cancellationToken = default);

		/// <summary>
		/// Tests the connection without disturbing the cached one.
		/// </summary>
		/// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
		Task TestAsync(CancellationToken cancellationToken = default);

		/// <summary>
		/// Closes the underlying connection when this lease is its last user.
		/// </summary>
		/// <param name="force">Whether to close the connection even when other leases still use it.</param>
		/// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
		/// <returns>
		/// <see langword="true"/> when the connection was closed; <see langword="false"/> when other users
		/// kept it alive, which leaves this lease released all the same.
		/// </returns>
		Task<bool> CloseAsync(bool force, CancellationToken cancellationToken = default);
	}
}
