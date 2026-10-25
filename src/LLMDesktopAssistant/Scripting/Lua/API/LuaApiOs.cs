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
			  Returns the current platform name.
			  Returns: string - "win", "linux", "osx", "freebsd", or "unknown"

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
			ns["username"] = new LuaCallbackFunction(Username);
			ns["hostname"] = new LuaCallbackFunction(Hostname);
			ns["sleep"] = new LuaCallbackFunction(Sleep);
			ns["pid"] = new LuaCallbackFunction(Pid);
			ns["uptime"] = new LuaCallbackFunction(Uptime);
			return null;
		}

		private static LuaTuple Platform(LuaCallingContext ctx, LuaValue[] args)
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				return new LuaTuple(new LuaString("win"));
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				return new LuaTuple(new LuaString("linux"));
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				return new LuaTuple(new LuaString("osx"));
			if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
				return new LuaTuple(new LuaString("freebsd"));
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
