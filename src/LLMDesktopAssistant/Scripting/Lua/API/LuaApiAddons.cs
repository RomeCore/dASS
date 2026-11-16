using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Loading;
using LLMDesktopAssistant.Addons.Management;
using LLMDesktopAssistant.Utils;
using LLMDesktopAssistant.Utils.Files;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for reloading addons from inside a running script - <c>dass.addon.*</c>.
	/// </summary>
	/// <remarks>
	/// A file tool only marks the addon managers invalid: the chat reloads them at the tool-batch barrier, so a
	/// script that writes an addon file and reads it back in the same run still sees the old copy. This API reloads
	/// on the spot. Runtime sources are the exception - a hook registered with <c>dass.hook.register</c> is live the
	/// moment the call returns and needs no reload.
	/// </remarks>
	[LuaApi(chatScoped: true)]
	public class LuaApiAddons(
		IAddonManagerInvalidator invalidator,
		IAddonPathImpactDetector impactDetector,
		IEnumerable<IAddonTypeDescriptor> typeDescriptors) : LuaApiBase
	{
		private readonly List<(AddonKind Kind, string Name)> _kindNames = BuildKindNames(typeDescriptors);
		private readonly Dictionary<string, AddonKind> _kindByName = BuildKindByName(typeDescriptors);

		/// <inheritdoc/>
		public override string? Namespace => "dass.addon";

		/// <inheritdoc/>
		public override string? Manuals => """
			--- dass.addon - reloading addons from inside a script

			A file tool only marks the addon managers invalid: the chat reloads them when the running tool batch
			ends, so a script that writes 'packs/my_pack/skills/x/SKILL.md' and then reads it back in the same run still
			sees the old copy. `dass.addon.reload` reloads now, inside the run.

			Runtime sources are different. For example, a hook registered with `dass.hook.register` is live the moment the call
			returns, and removing it takes effect as immediately, so runtime addons never need a reload.

			FUNCTIONS:

			--- dass.addon.reload([target]) -> table
			  Reloads the addon managers addressed by the target and reports what was reloaded.

			  Parameters:
			    - target: nil, string or table (optional)
			        nil - every addon kind, pack folders rescanned
			        string - a path: the kinds are detected from it exactly as a file tool detects them
			          (operation "edit", file or directory unknown)
			        table - options:
			            all: boolean (default false) - every addon kind, pack folders rescanned
			            pack: boolean (default false) - rescan the pack folders of the chat and the application,
			              so packs that appeared, disappeared or were renamed are picked up
			            path: string - detect the kinds from this path
			            operation: string (default "edit") - "create", "edit" or "delete", used by the detection
			            is_file: boolean - whether the path is a file; omitted means unknown, so both
			              interpretations are applied
			            kinds: array of strings - explicit addon type names, for example { "skills", "tools" }
			          All of them combine: every part adds to the set that is reloaded.

			  Returns: table with
			    - kinds: array of strings - the addon type names that were reloaded
			    - reloaded: boolean - false when nothing matched, so no manager was touched
			    - reason: string or nil - why nothing matched

			  The valid type names are the addon type descriptors of this build: skills, agents, tools,
			  templates, context, commands, hooks, scripts/lua, plus "pack" and "all". A singular form is
			  accepted too ("skill", "tool"), and the match ignores case.

			--- dass.addon.detect_kinds(path, [operation], [is_file]) -> array of strings
			  Detects which addon kinds a path would reload, without reloading anything.

			  Parameters:
			    - path: string - the touched path; relative paths resolve against the chat's working directory
			    - operation: string (optional, default "edit") - "create", "edit" or "delete"
			    - is_file: boolean (optional) - whether the path is a file; omitted means unknown

			  Returns: array of strings - the detected type names, empty when the path is outside every addon
			  folder of this chat.

			NOTES:

			  - Reloading parses the addon files, so call it once after the last write of a batch, not after every
			    file: ten calls to reload are ten parses of the same packs.
			  - A path outside every addon folder is not an error: `reloaded` is false and `reason` says why.
			  - Reloading "scripts/lua" while a script runs re-reads the script files but does not re-run the
			    running state: the refresh takes a non-blocking lock and a busy state is left alone. Write the new
			    script, reload, and call into it from a later run.
			  - The change is announced to the agent as an addon delta in the next prompt, so a skill, sub-agent,
			    tool or memory block that appeared or disappeared is visible without a new chat.
			  - This does not touch the state of the calling script: only the addon managers recollect their
			    addons.
			  - Use reloading only if your script uses just created addons (e.g. call a 'my-tool' tool after just created
			    'my-tool.lua' file), if not, dass.addon.reload is REDUNDANT, because addons are ALWAYS reloaded after
			    script execution (lua-execute, shell-* tools and scripted tools), so no reload is needed.

			EXAMPLES:

			  -- A tool that writes its own skill and wants to use it in this run
			  fs.write(".agents/skills/mine/SKILL.md", body)
			  local report = dass.addon.reload(".agents/skills/mine/SKILL.md")
			  if not report.reloaded then print(report.reason) end

			  -- Ask before reloading
			  local kinds = dass.addon.detect_kinds(".agents/tools/build.py")
			  print(table.concat(kinds, ", "))   -- "tools"

			  -- A whole pack appears: create the folder, then rescan the packs
			  fs.create_dir(".agents/packs/new-pack/skills/x")
			  fs.write(".agents/packs/new-pack/skills/x/SKILL.md", body)
			  dass.addon.reload{ path = ".agents/packs/new-pack/skills/x/SKILL.md", operation = "create" }

			  -- Everything, including the pack folders
			  dass.addon.reload{ all = true }
			  dass.addon.reload()
			""";

		/// <inheritdoc/>
		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["reload"] = new LuaCallbackFunction(Reload);
			ns["detect_kinds"] = new LuaCallbackFunction(DetectKinds);
			return null;
		}

		private LuaTuple Reload(LuaCallingContext ctx, LuaValue[] args)
		{
			const string call = "dass.addon.reload([target])";

			AddonKind kinds;
			string? reason;

			switch (args.Length > 0 ? args[0] : LuaNil.Instance)
			{
				case LuaNil:
					kinds = AddonKind.All;
					reason = null;
					break;

				case LuaString path:
					(kinds, reason) = DetectPath(path.Value, FileOperation.Edit, null);
					break;

				case LuaTable options:
					(kinds, reason) = ReadOptions(options, call);
					break;

				default:
					throw new LuaRuntimeException(
						$"{call}: the argument must be a path string, an options table, or omitted.");
			}

			if (kinds != AddonKind.None)
				invalidator.Reload(kinds);

			return new LuaTuple(BuildReloadResult(kinds, reason));
		}

		private LuaTuple DetectKinds(LuaCallingContext ctx, LuaValue[] args)
		{
			const string call = "dass.addon.detect_kinds(path, [operation], [is_file])";

			if (args.Length == 0 || args[0] is not LuaString path)
				throw new LuaRuntimeException($"{call}: argument 1 must be a string (path).");

			var operation = FileOperation.Edit;
			if (args.Length >= 2 && args[1] is not LuaNil)
				operation = ParseOperation(args[1], "argument 2", call);

			bool? isFile = null;
			if (args.Length >= 3 && args[2] is not LuaNil)
			{
				if (args[2] is not LuaBoolean file)
					throw new LuaRuntimeException($"{call}: argument 3 must be a boolean (is_file).");

				isFile = file.Value;
			}

			var detected = impactDetector.Detect(path.Value, isFile, operation);

			var names = new LuaTable();
			int index = 1;
			foreach (var (kind, name) in _kindNames)
				if ((detected & kind) == kind)
					names.Set(index++, new LuaString(name));

			return new LuaTuple(names);
		}

		/// <summary>
		/// Reads the options table of <c>dass.addon.reload</c> into the set of kinds to reload, the option keys being
		/// matched ignoring case, '-' and '_' like every other Lua options table.
		/// </summary>
		private (AddonKind Kinds, string? Reason) ReadOptions(LuaTable options, string call)
		{
			var values = new Dictionary<string, LuaValue>(StringComparer.Ordinal);
			foreach (var entry in options.Entries)
				if (entry.Key is LuaString key)
					values[key.Value] = entry.Value;

			var kinds = AddonKind.None;

			if (ReadBool(values, "all", call) == true)
				kinds |= AddonKind.All;

			if (ReadBool(values, "pack", call) == true)
				kinds |= AddonKind.Pack;

			string? reason = null;

			if (ReadString(values, "path", call) is { } path)
			{
				var detected = impactDetector.Detect(path, ReadBool(values, "is-file", call),
					ReadOperation(values, call));

				if (detected == AddonKind.None)
					reason = $"'{path}' is not inside an addon folder reachable from this chat.";
				else
					kinds |= detected;
			}

			if (values.TryGetValue("kinds", out var kindList) && kindList is not LuaNil)
			{
				if (kindList is not LuaTable list)
					throw new LuaRuntimeException($"{call}: 'kinds' must be an array of addon type names.");

				for (int index = 1; index <= list.Length; index++)
					kinds |= ParseKindName(list.Get(index), call);
			}

			// A reason is only worth reporting when it explains an actual no-op; a partial detection stays silent.
			reason = kinds == AddonKind.None
				? reason ?? "nothing in the arguments named an addon type reachable from this chat."
				: null;

			return (kinds, reason);
		}

		private (AddonKind Kinds, string? Reason) DetectPath(string path, FileOperation operation, bool? isFile)
		{
			var detected = impactDetector.Detect(path, isFile, operation);

			return detected == AddonKind.None
				? (AddonKind.None, $"'{path}' is not inside an addon folder reachable from this chat.")
				: (detected, null);
		}

		private LuaTable BuildReloadResult(AddonKind kinds, string? reason)
		{
			var names = new LuaTable();
			int index = 1;
			foreach (var (kind, name) in _kindNames)
				if ((kinds & kind) == kind)
					names.Set(index++, new LuaString(name));

			var result = new LuaTable();
			result.Set("kinds", names);
			result.Set("reloaded", kinds != AddonKind.None ? LuaBoolean.True : LuaBoolean.False);
			result.Set("reason", reason is null ? LuaNil.Instance : new LuaString(reason));
			return result;
		}

		private AddonKind ParseKindName(LuaValue value, string call)
		{
			if (value is not LuaString text)
				throw new LuaRuntimeException($"{call}: every entry of 'kinds' must be a string.");

			if (_kindByName.TryGetValue(text.Value, out var kind))
				return kind;

			var names = string.Join(", ", _kindNames.Select(entry => entry.Name));
			throw new LuaRuntimeException($"{call}: unknown addon type '{text.Value}'. Valid types: {names}, all.");
		}

		private static FileOperation ReadOperation(Dictionary<string, LuaValue> values, string call)
		{
			if (!values.TryGetValue("operation", out var value) || value is LuaNil)
				return FileOperation.Edit;

			return ParseOperation(value, "'operation'", call);
		}

		private static FileOperation ParseOperation(LuaValue value, string subject, string call)
		{
			if (value is not LuaString text)
				throw new LuaRuntimeException($"{call}: {subject} must be a string.");

			return text.Value.Trim().ToLowerInvariant() switch
			{
				"create" => FileOperation.Create,
				"edit" => FileOperation.Edit,
				"delete" => FileOperation.Delete,
				_ => throw new LuaRuntimeException($"{call}: {subject} must be one of: create, edit, delete.")
			};
		}

		private static bool? ReadBool(Dictionary<string, LuaValue> values, string key, string call)
		{
			if (!values.TryGetValue(key, out var value) || value is LuaNil)
				return null;

			if (value is LuaBoolean flag)
				return flag.Value;

			throw new LuaRuntimeException($"{call}: '{key}' must be a boolean.");
		}

		private static string? ReadString(Dictionary<string, LuaValue> values, string key, string call)
		{
			if (!values.TryGetValue(key, out var value) || value is LuaNil)
				return null;

			if (value is LuaString text)
				return string.IsNullOrWhiteSpace(text.Value) ? null : text.Value;

			throw new LuaRuntimeException($"{call}: '{key}' must be a string.");
		}

		/// <summary>
		/// Builds the rendering order of the kinds: pack first, then the registered kinds in enum order, each named
		/// by the first registered addon type descriptor that carries it.
		/// </summary>
		private static List<(AddonKind Kind, string Name)> BuildKindNames(
			IEnumerable<IAddonTypeDescriptor> descriptors)
		{
			var nameByKind = new Dictionary<AddonKind, string>();
			foreach (var descriptor in descriptors.OrderBy(entry => entry.Type, IdentifierIgnoreCaseComparer.Instance))
				nameByKind.TryAdd(descriptor.Kind, descriptor.Type);

			var names = new List<(AddonKind, string)> { (AddonKind.Pack, "pack") };
			names.AddRange(nameByKind
				.OrderBy(entry => (int)entry.Key)
				.Select(entry => (entry.Key, entry.Value)));
			return names;
		}

		/// <summary>
		/// Builds the type-name lookup: every descriptor type, its singular form as an alias, plus "pack" and "all".
		/// </summary>
		private static Dictionary<string, AddonKind> BuildKindByName(
			IEnumerable<IAddonTypeDescriptor> descriptors)
		{
			var kinds = new Dictionary<string, AddonKind>(IdentifierIgnoreCaseComparer.Instance)
			{
				["pack"] = AddonKind.Pack,
				["all"] = AddonKind.All
			};

			foreach (var descriptor in descriptors)
			{
				var name = descriptor.Type;
				kinds.TryAdd(name, descriptor.Kind);
				if (name.Length > 1 && name.EndsWith('s'))
					kinds.TryAdd(name[..^1], descriptor.Kind);
			}

			return kinds;
		}
	}
}
