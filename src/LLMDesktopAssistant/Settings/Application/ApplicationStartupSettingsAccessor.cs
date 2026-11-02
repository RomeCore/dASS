using System.Text.Json;
using System.Text.Json.Serialization;
using LLMDesktopAssistant.Utils;
using Serilog;

namespace LLMDesktopAssistant.Settings.Application
{
	/// <summary>
	/// Owns <c>startup.json</c>: the only reader and writer of <see cref="ApplicationStartupSettings"/>.
	/// It deliberately uses plain JSON options instead of the ones in <see cref="SettingsManager"/>,
	/// because those carry a polymorphic type resolver that cannot be built before the reflection scan.
	/// </summary>
	public static class ApplicationStartupSettingsAccessor
	{
		private const string fileName = "startup.json";

		private static readonly JsonSerializerOptions _options = new()
		{
			WriteIndented = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		};

		private static ApplicationStartupSettings? _settings;
		/// <summary>
		/// Gets the startup settings, loading them on first access.
		/// </summary>
		public static ApplicationStartupSettings Current
		{
			get
			{
				Load();
				return _settings!;
			}
		}

		/// <summary>
		/// Loads the settings from disk and starts persisting every change. Called once from the
		/// shared initialization; later calls do nothing.
		/// </summary>
		public static void Load()
		{
			if (_settings is not null)
				return;

			_settings = ReadFile();
			_settings.DeepChanged += (_, _) => Save();
		}

		/// <summary>
		/// Writes the current values to disk.
		/// </summary>
		public static void Save()
		{
			if (_settings is null)
				return;

			try
			{
				File.WriteAllText(GetPath(), JsonSerializer.Serialize(_settings, _options));
			}
			catch (Exception exception)
			{
				Log.Warning(exception, "Failed to save the startup settings.");
			}
		}

		private static string GetPath() => Path.Combine(Directories.Settings, fileName);

		private static ApplicationStartupSettings ReadFile()
		{
			try
			{
				var path = GetPath();

				if (!File.Exists(path))
					return new ApplicationStartupSettings();

				return JsonSerializer.Deserialize<ApplicationStartupSettings>(File.ReadAllText(path), _options)
					?? new ApplicationStartupSettings();
			}
			catch (Exception exception)
			{
				Log.Warning(exception, "Failed to read the startup settings, using defaults.");
				return new ApplicationStartupSettings();
			}
		}
	}
}
