using System.Runtime.InteropServices;
using AsyncLua;
using AsyncLua.Values;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for operating system information: <c>os.*</c>.
	/// Provides platform info, environment, and sleep.
	/// </summary>
	[LuaApi(chatScoped: false)]
	public class LuaApiOs : LuaApiBase
	{
		public override string? Namespace => "os";

		public override string? Manuals => """
			--- os - operating system API

			Provides information about the system and basic OS operations.

			FUNCTIONS:

			--- os.platform()
			  Returns the current platform name (note that not all platforms are supported on the dASS runtime).
			  Returns: string - "win", "linux", "freebsd", "android", "osx", "maccatalyst", "macos", "ios",
			    "tvos", "wasi", "watchos", "browser", or "unknown"

			--- os.arch()
			  Returns the processor architecture.
			  Returns: string - e.g. "x64", "arm64", "x86", "arm"

			--- os.env(name)
			  Returns the value of an environment variable, or nil if not set.
			  Parameters:
			    - name: string - environment variable name (case-insensitive on Windows)
			  Returns: string or nil

			--- os.username()
			  Returns the current user's login name.
			  Returns: string

			--- os.hostname()
			  Returns the machine's host name.
			  Returns: string

			--- os.sleep(seconds)
			  Pauses execution for the specified duration.
			  Parameters:
			    - seconds: number - duration in seconds (can be fractional, e.g. 0.5)
			  Returns: nil

			--- os.pid()
			  Returns the current process ID.
			  Returns: number

			--- os.uptime()
			  Returns the system uptime in seconds.
			  Returns: number

			EXAMPLES:

			  print("Platform:", os.platform())
			  print("Arch:", os.arch())
			  print("User:", os.username())
			  print("Host:", os.hostname())
			  print("PID:", os.pid())
			  print("Uptime:", os.uptime(), "seconds")
			  local path = os.env("PATH")
			  os.sleep(0.5) -- pause 500ms
			""";

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["platform"] = new LuaCallbackFunction(Platform);
			ns["arch"] = new LuaCallbackFunction(Arch);
			ns["env"] = new LuaCallbackFunction(Env);
			ns["getenv"] = ns["env"];
			ns["username"] = new LuaCallbackFunction(Username);
			ns["hostname"] = new LuaCallbackFunction(Hostname);
			ns["sleep"] = new LuaCallbackFunction(Sleep);
			ns["pid"] = new LuaCallbackFunction(Pid);
			ns["uptime"] = new LuaCallbackFunction(Uptime);
			return null;
		}

		private static LuaTuple Platform(LuaCallingContext ctx, LuaValue[] args)
		{
			if (OperatingSystem.IsWindows())
				return new LuaTuple(new LuaString("win"));
			if (OperatingSystem.IsLinux())
				return new LuaTuple(new LuaString("linux"));
			if (OperatingSystem.IsFreeBSD())
				return new LuaTuple(new LuaString("freebsd"));
			if (OperatingSystem.IsAndroid())
				return new LuaTuple(new LuaString("android"));
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				return new LuaTuple(new LuaString("osx"));
			if (OperatingSystem.IsMacCatalyst())
				return new LuaTuple(new LuaString("maccatalyst"));
			if (OperatingSystem.IsMacOS())
				return new LuaTuple(new LuaString("macos"));
			if (OperatingSystem.IsIOS())
				return new LuaTuple(new LuaString("ios"));
			if (OperatingSystem.IsTvOS())
				return new LuaTuple(new LuaString("tvos"));
			if (OperatingSystem.IsWasi())
				return new LuaTuple(new LuaString("wasi"));
			if (OperatingSystem.IsWatchOS())
				return new LuaTuple(new LuaString("watchos"));
			if (OperatingSystem.IsBrowser())
				return new LuaTuple(new LuaString("browser"));
			return new LuaTuple(new LuaString("unknown"));
		}

		private static LuaTuple Arch(LuaCallingContext ctx, LuaValue[] args)
		{
			var arch = RuntimeInformation.ProcessArchitecture switch
			{
				System.Runtime.InteropServices.Architecture.X64 => "x64",
				System.Runtime.InteropServices.Architecture.X86 => "x86",
				System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
				System.Runtime.InteropServices.Architecture.Arm => "arm",
				_ => "unknown"
			};
			return new LuaTuple(new LuaString(arch));
		}

		private static LuaTuple Env(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("os.env(name): at least 1 argument expected.");
			if (args[0] is not LuaString nameValue)
				throw new LuaRuntimeException("os.env(): first argument must be a string.");
			var value = Environment.GetEnvironmentVariable(nameValue.Value);
			if (value == null)
				return new LuaTuple(LuaNil.Instance);
			return new LuaTuple(new LuaString(value));
		}

		private static LuaTuple Username(LuaCallingContext ctx, LuaValue[] args)
		{
			return new LuaTuple(new LuaString(Environment.UserName));
		}

		private static LuaTuple Hostname(LuaCallingContext ctx, LuaValue[] args)
		{
			return new LuaTuple(new LuaString(Environment.MachineName));
		}

		private static LuaTuple Sleep(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("os.sleep(seconds): at least 1 argument expected.");
			if (args[0] is not LuaNumber secondsVal)
				throw new LuaRuntimeException("os.sleep(): first argument must be a number.");
			System.Threading.Thread.Sleep(TimeSpan.FromSeconds(secondsVal.Value));
			return new LuaTuple(LuaNil.Instance);
		}

		private static LuaTuple Pid(LuaCallingContext ctx, LuaValue[] args)
		{
			return new LuaTuple(new LuaNumber(Environment.ProcessId));
		}

		private static LuaTuple Uptime(LuaCallingContext ctx, LuaValue[] args)
		{
			return new LuaTuple(new LuaNumber(Environment.TickCount64 / 1000.0));
		}
	}
}
