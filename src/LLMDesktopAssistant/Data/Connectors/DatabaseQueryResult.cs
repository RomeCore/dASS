namespace LLMDesktopAssistant.Data.Connectors
{
	/// <summary>
	/// The result of executing a query against a database connection.
	/// </summary>
	public sealed class DatabaseQueryResult
	{
		/// <summary>
		/// The column names of the returned result set, or an empty array for statements
		/// that do not return rows (e.g. <c>INSERT</c>, <c>UPDATE</c>, DDL).
		/// </summary>
		public required string[] Columns { get; init; }

		/// <summary>
		/// The data rows of the result set. Each row has the same length as <see cref="Columns"/>;
		/// a <see langword="null"/> cell is SQL <c>NULL</c>, which callers must keep distinct from
		/// the text <c>"NULL"</c>.
		/// </summary>
		public required string?[][] Rows { get; init; }

		/// <summary>
		/// The number of rows affected by the statement, or <c>-1</c> when not applicable.
		/// </summary>
		public int RowsAffected { get; init; } = -1;

		/// <summary>
		/// Gets a value indicating whether the result set was truncated because it exceeded
		/// the row limit the caller asked for.
		/// </summary>
		public bool Truncated { get; init; }
	}
}
