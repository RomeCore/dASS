using System.ComponentModel;
using System.Text.Json.Serialization;
using Material.Icons;

namespace LLMDesktopAssistant.Controls.Icons
{
	/// <summary>
	/// A pack-agnostic icon reference: a pack plus its payload.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The canonical string form is <c>&lt;pack&gt;:&lt;data&gt;</c>, for example
	/// <c>material:Account</c> or <c>path:M12 2 L2 22 ...</c>. A bare string without a
	/// separator (e.g. <c>Plus</c>) is a shorthand for the material pack and is
	/// normalized to <c>material:Plus</c>. An unknown pack is preserved verbatim so
	/// that parsing never loses information.
	/// </para>
	/// <para>
	/// <see cref="Parse"/> / <see cref="TryParse"/> validate <em>syntax only</em>.
	/// Semantic validation (does the pack exist, are the data valid) is the job of
	/// <see cref="VisualIconDataHandler"/>.
	/// </para>
	/// </remarks>
	[TypeConverter(typeof(VisualIconKindAvaloniaConverter))]
	[JsonConverter(typeof(VisualIconKindJsonConverter))]
	public readonly record struct VisualIconKind(IconPackKind Pack, string Data)
	{
		/// <summary>The pack name used for the material icon pack.</summary>
		public const string MaterialPackName = "material";

		/// <summary>The pack name used for the raw SVG path pack.</summary>
		public const string PathPackName = "path";

		/// <summary>The "no icon" value.</summary>
		public static readonly VisualIconKind None = new(IconPackKind.None, string.Empty);

		/// <summary>Whether this value means "no icon".</summary>
		public bool IsNone => Pack == IconPackKind.None;

		/// <summary>Creates a material-pack icon from the strongly typed material enum.</summary>
		public static implicit operator VisualIconKind(MaterialIconKind kind)
			=> new(IconPackKind.Material, kind.ToString());

		/// <summary>Creates an inline path-pack icon from raw SVG path data.</summary>
		public static VisualIconKind FromPath(string path)
			=> new(IconPackKind.Path, path);

		/// <summary>
		/// Parses the canonical string form. Returns <see cref="None"/> for <see langword="null"/>
		/// or a whitespace-only string; throws only when <paramref name="value"/> is <see langword="null"/>.
		/// </summary>
		/// <exception cref="FormatException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
		public static VisualIconKind Parse(string? value)
		{
			if (!TryParse(value, out var kind))
				throw new FormatException($"'{value}' is not a valid visual icon kind.");

			return kind;
		}

		/// <summary>
		/// Parses the canonical string form. Parsing is permissive: any non-null string
		/// produces a value (an unrecognised pack becomes <see cref="IconPackKind.Unknown"/>
		/// with the original text kept in <see cref="Data"/>). Returns <see langword="false"/>
		/// only for <see langword="null"/>.
		/// </summary>
		public static bool TryParse(string? value, out VisualIconKind kind)
		{
			if (value is null)
			{
				kind = None;
				return false;
			}

			if (string.IsNullOrWhiteSpace(value))
			{
				kind = None;
				return true;
			}

			var separatorIndex = value.IndexOf(':');
			if (separatorIndex < 0)
			{
				// A bare name without a separator is a material icon ("Plus" -> "material:Plus").
				kind = new VisualIconKind(IconPackKind.Material, value);
				return true;
			}

			var packName = value[..separatorIndex];
			var data = value[(separatorIndex + 1)..];

			if (string.Equals(packName, MaterialPackName, StringComparison.OrdinalIgnoreCase))
				kind = new VisualIconKind(IconPackKind.Material, data);
			else if (string.Equals(packName, PathPackName, StringComparison.OrdinalIgnoreCase))
				kind = new VisualIconKind(IconPackKind.Path, data);
			else
				// Preserve the unknown pack verbatim so nothing is lost on round-trip.
				kind = new VisualIconKind(IconPackKind.Unknown, value);

			return true;
		}

		/// <inheritdoc />
		public override string ToString() => Pack switch
		{
			IconPackKind.None => string.Empty,
			// Unknown packs keep the original raw text (lossless round-trip).
			IconPackKind.Unknown => Data ?? string.Empty,
			_ => $"{GetPackName(Pack)}:{Data}",
		};

		private static string GetPackName(IconPackKind pack) => pack switch
		{
			IconPackKind.Material => MaterialPackName,
			IconPackKind.Path => PathPackName,
			_ => pack.ToString().ToLowerInvariant(),
		};
	}
}
