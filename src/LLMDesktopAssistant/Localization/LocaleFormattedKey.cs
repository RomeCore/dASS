using System.Collections.Immutable;
using System.ComponentModel;

namespace LLMDesktopAssistant.Localization
{
	/// <summary>
	/// A localization key that carries immutable format arguments and renders itself through
	/// <see cref="string.Format(string, object?[])"/>.
	/// </summary>
	/// <remarks>
	/// Unlike <see cref="LocaleKey"/>, instances are <em>not</em> cached by the facade: the arguments are part of the
	/// value, so a consumer creates one wherever a formatted value is needed. The instance caches its own rendered
	/// value (like <see cref="LocaleKey"/>) and drops the cache when the language changes, raising
	/// <see cref="PropertyChanged"/> so bindings refresh.
	/// </remarks>
	public sealed class LocaleFormattedKey : LocaleKeyBase, IEquatable<LocaleFormattedKey>
	{
		/// <summary>
		/// The immutable format arguments, applied to the localized template.
		/// </summary>
		public ImmutableArray<string?> FormatArgs { get; }

		private bool _isValueCached = false;
		private string? _cachedValue = null;

		/// <summary>
		/// Creates a formatted key.
		/// </summary>
		/// <param name="key">The full localization key.</param>
		/// <param name="formatArgs">The format arguments substituted into the localized template.</param>
		public LocaleFormattedKey(string key, ImmutableArray<string?> formatArgs) : base(key)
		{
			FormatArgs = formatArgs;
			LocalizationManager.StaticLanguageChanged += OnStaticLanguageChanged;
		}

		/// <inheritdoc/>
		public override string? RawValue
		{
			get
			{
				if (!_isValueCached)
				{
					var format = LocalizationManager.TryLocalizeStatic(Key);
					_cachedValue = format is null
						? null
						: string.Format(format, FormatArgs.Cast<object?>().ToArray());
					_isValueCached = true;
				}
				return _cachedValue;
			}
		}

		/// <inheritdoc/>
		public override event PropertyChangedEventHandler? PropertyChanged;

		private void OnStaticLanguageChanged(object? sender, string language)
		{
			_isValueCached = false;
			_cachedValue = null;
			PropertyChanged?.Invoke(this, _cachedValuePropertyChangedArgs);
			PropertyChanged?.Invoke(this, _cachedRawValuePropertyChangedArgs);
		}

		/// <inheritdoc/>
		public bool Equals(LocaleFormattedKey? other)
		{
			return other is not null
				&& string.Equals(Key, other.Key, StringComparison.Ordinal)
				&& FormatArgs.SequenceEqual(other.FormatArgs);
		}

		/// <inheritdoc/>
		public override bool Equals(object? obj)
		{
			return Equals(obj as LocaleFormattedKey);
		}

		/// <inheritdoc/>
		public override int GetHashCode()
		{
			var hash = new HashCode();
			hash.Add(Key, StringComparer.Ordinal);
			foreach (var arg in FormatArgs)
				hash.Add(arg, StringComparer.Ordinal);
			return hash.ToHashCode();
		}
	}
}
