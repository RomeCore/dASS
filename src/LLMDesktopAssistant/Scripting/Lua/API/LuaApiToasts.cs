using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Services.Instances;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for showing toast notifications in the UI: <c>dass.toast.*</c>.
	/// Thin wrapper over <see cref="IToastService"/>.
	/// </summary>
	[LuaApi(chatScoped: false)]
	public class LuaApiToasts : LuaApiBase
	{
		private readonly IToastService _toastService;

		public override string? Namespace => "dass.toast";

		public override string? Manuals => """
			--- dass.toast - toast notification API

			Shows toast notifications (popup messages) in the application UI.

			FUNCTIONS:

			--- dass.toast.info(title, [description], [durationSeconds])
			  Shows an informational toast.
			  Parameters:
			    - title: string - toast title (supports Markdown)
			    - description: string (optional) - additional text
			    - durationSeconds: number (optional, default 5) - auto-dismiss seconds
			  Returns: nil

			--- dass.toast.warning(title, [description], [durationSeconds])
			  Shows a warning toast (default duration: 6 seconds).

			--- dass.toast.error(title, [description], [durationSeconds])
			  Shows an error toast (default duration: 8 seconds).

			--- dass.toast.success(title, [description], [durationSeconds])
			  Shows a success toast (default duration: 5 seconds).

			EXAMPLES:

			  dass.toast.info("File saved!")
			  dass.toast.warning("Disk almost full", "Only 500 MB remaining", 10)
			  dass.toast.error("Connection lost", "Check your network settings")
			  dass.toast.success("**Build complete**", "All 42 tests passed")
			""";

		public LuaApiToasts(IToastService toastService)
		{
			_toastService = toastService;
		}

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["info"] = new LuaCallbackFunction(Info);
			ns["warning"] = new LuaCallbackFunction(Warning);
			ns["error"] = new LuaCallbackFunction(Error);
			ns["success"] = new LuaCallbackFunction(Success);
			return null;
		}

		private LuaTuple Info(LuaCallingContext ctx, LuaValue[] args)
		{
			_toastService.ShowInfo(
				GetString(args, 0, "title"),
				GetOptionalString(args, 1),
				GetOptionalNumber(args, 2) ?? 5.0);
			return new LuaTuple(LuaNil.Instance);
		}

		private LuaTuple Warning(LuaCallingContext ctx, LuaValue[] args)
		{
			_toastService.ShowWarning(
				GetString(args, 0, "title"),
				GetOptionalString(args, 1),
				GetOptionalNumber(args, 2) ?? 6.0);
			return new LuaTuple(LuaNil.Instance);
		}

		private LuaTuple Error(LuaCallingContext ctx, LuaValue[] args)
		{
			_toastService.ShowError(
				GetString(args, 0, "title"),
				GetOptionalString(args, 1),
				GetOptionalNumber(args, 2) ?? 8.0);
			return new LuaTuple(LuaNil.Instance);
		}

		private LuaTuple Success(LuaCallingContext ctx, LuaValue[] args)
		{
			_toastService.ShowSuccess(
				GetString(args, 0, "title"),
				GetOptionalString(args, 1),
				GetOptionalNumber(args, 2) ?? 5.0);
			return new LuaTuple(LuaNil.Instance);
		}

		private static string GetString(LuaValue[] args, int index, string paramName)
		{
			if (args.Length <= index || args[index] is not LuaString str)
				throw new LuaRuntimeException($"dass.toast: argument #{index + 1} ({paramName}) must be a string.");
			return str.Value;
		}

		private static string? GetOptionalString(LuaValue[] args, int index)
		{
			return args.Length > index && args[index] is LuaString str ? str.Value : null;
		}

		private static double? GetOptionalNumber(LuaValue[] args, int index)
		{
			return args.Length > index && args[index] is LuaNumber num ? num.Value : null;
		}
	}
}
