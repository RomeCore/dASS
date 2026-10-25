using LLMDesktopAssistant.Agents.Memory;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Tools;

namespace LLMDesktopAssistant.Utils
{
	public static class Directories
	{
		/// <summary>
		/// The general home directory used for files inside the working directory. Used for copied attachments and other files.
		/// </summary>
		public const string WorkingHome = ".llmassist";

		/// <summary>
		/// The root directory of the application data, typically located at %LOCALAPPDATA%\.llmassist.
		/// </summary>
		public static string LocalAppData { get; }

		/// <summary>
		/// The path to the user's profile directory, typically located at %USERPROFILE%.
		/// </summary>
		public static string UserProfile { get; }

		/// <summary>
		/// The path where to store the temporary files for the application. They will be cleared upon application exit.
		/// </summary>
		public static string TempFiles { get; }

		/// <summary>
		/// The path where to store lua scripts to be loaded into the Lua runtime at chat loading.
		/// </summary>
		public static string LuaScripts { get; }

		/// <summary>
		/// The path where to store the temporary scripts to be executed and instantly deleted after execution.
		/// </summary>
		public static string TempScripts { get; }

		/// <summary>
		/// The path where to store the plugins for the application. These are typically .dll files or other executable components that extend the functionality of the application.
		/// </summary>
		public static string Plugins { get; }

		/// <summary>
		/// The path where to store the addon packs for the application.
		/// </summary>
		public static string AddonPacks { get; }

		/// <summary>
		/// The path where to store the additional templates for prompting, they are usually has .llt extension.
		/// </summary>
		public static string Templates { get; }

		/// <summary>
		/// The path where to store the skills usable for agentic tasks. Skills are stored in separate folders with SKILL.md inside root folder.
		/// </summary>
		public static string Skills { get; }

		/// <summary>
		/// The path where to store the sub-agents usable for agentic tasks.
		/// </summary>
		public static string Agents { get; }

		/// <summary>
		/// The path where to store the agentic memory data. This directory contains subdirectories, where each subdirectory represents a data associated with <see cref="MemoryBlock"/>.
		/// </summary>
		public static string Memory { get; }

		/// <summary>
		/// The path where to store the user locale files (.loc) that override the built-in localization.
		/// </summary>
		public static string LocaleFiles { get; }

		/// <summary>
		/// The path where to store the settings files. These are usually configuration files that need to persist across sessions.
		/// </summary>
		public static string Settings { get; }

		/// <summary>
		/// The path where to store the database-related files, such as chat and usage data.
		/// </summary>
		public static string Data { get; }

		/// <summary>
		/// The path where to store the models and other machine learning related files.
		/// </summary>
		public static string Models { get; }

		/// <summary>
		/// The default working directory for the application.
		/// </summary>
		public static string DefaultWorkingDirectory { get; }

		/// <summary>
		/// The localized name of the default working directory. Typically used for tools and UI.
		/// </summary>
		public static string DefaultWorkingDirectoryName => LocalizationManager.LocalizeStatic("common.default");

		/// <summary>
		/// The path where to store log files.
		/// </summary>
		public static string LogFiles { get; }

		static Directories()
		{
			LocalAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), WorkingHome);
			UserProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			TempFiles = Path.Combine(LocalAppData, "temp");
			LuaScripts = Path.Combine(LocalAppData, "scripts", "lua");
			TempScripts = Path.Combine(LocalAppData, "temp", "scripts");
			Plugins = Path.Combine(LocalAppData, "plugins");
			AddonPacks = Path.Combine(LocalAppData, "packs");
			Templates = Path.Combine(LocalAppData, "templates");
			Skills = Path.Combine(LocalAppData, "skills");
			Agents = Path.Combine(LocalAppData, "agents");
			Memory = Path.Combine(LocalAppData, "memory");
			LocaleFiles = Path.Combine(LocalAppData, "locale");
			Settings = Path.Combine(LocalAppData, "settings");
			Data = Path.Combine(LocalAppData, "data");
			Models = Path.Combine(LocalAppData, "models");
			DefaultWorkingDirectory = Path.Combine(LocalAppData, "working_directory");
			LogFiles = Path.Combine(LocalAppData, "logs");
		}

		public static void EnsureAll()
		{
			Directory.CreateDirectory(LocalAppData);
			Directory.CreateDirectory(TempFiles);
			Directory.CreateDirectory(LuaScripts);
			Directory.CreateDirectory(TempScripts);
			Directory.CreateDirectory(Plugins);
			Directory.CreateDirectory(AddonPacks);
			Directory.CreateDirectory(Templates);
			Directory.CreateDirectory(Skills);
			Directory.CreateDirectory(Agents);
			Directory.CreateDirectory(Memory);
			Directory.CreateDirectory(LocaleFiles);
			Directory.CreateDirectory(Settings);
			Directory.CreateDirectory(Data);
			Directory.CreateDirectory(Models);
			Directory.CreateDirectory(DefaultWorkingDirectory);
			Directory.CreateDirectory(LogFiles);
		}

		public static void ClearTempFiles()
		{
			try
			{
				if (Directory.Exists(TempFiles))
				{
					Directory.Delete(TempFiles, recursive: true);
				}
			}
			catch (UnauthorizedAccessException ex)
			{
				System.Diagnostics.Debug.WriteLine($"Cannot clear temp files: {ex.Message}");
			}
			catch (IOException ex)
			{
				System.Diagnostics.Debug.WriteLine($"Cannot clear temp files (file in use?): {ex.Message}");
			}
			finally
			{
				Directory.CreateDirectory(TempFiles);
				Directory.CreateDirectory(TempScripts);
			}
		}
	}
}