using AsyncLua.Values;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Parsers;
using LLMDesktopAssistant.Addons.Parsers.Frontmatter;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.StructuredValues.Parameterization;

namespace LLMDesktopAssistant.Scripting.Lua
{
	[Service(typeof(IAddonFileParser<LuaScriptInfo>))]
	public class LuaScriptParser(IParameterSchemaParserManager paramsSchemaParser)
		: FrontmatterBasedAddonParser<LuaScriptInfo, LuaScriptChange>(paramsSchemaParser)
	{
		/// <inheritdoc/>
		protected override bool SupportsParamsSchema => true;

		protected override AddonParserDescriptor GetDescriptorFor(string content, AddonPathInfo fileInfo)
		{
			return new AddonParserDescriptor
			{
				FrontmatterStart = "--[[",
				FrontmatterEnd = "]]",
				RequiresFrontmatter = true,
				UseMarkdownFallback = false
			};
		}

		protected override void Populate(LuaScriptInfo addon, AddonFrontmatterDocument frontmatter, ref AddonDiagnostic? diagnostic)
		{
			base.Populate(addon, frontmatter, ref diagnostic);

			addon.IsNative = false;
			addon.Namespace = frontmatter.Get<string?>("namespace");
			addon.Manuals = frontmatter.Get<string?>("manuals");

			addon.Loader = (globals, ns, lua) =>
			{
				// Load body dynamically
				var originalCode = addon.Body;

				var lines = originalCode.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
				int cleanupMarkerIndex = -1;
				for (var i = 0; i < lines.Length; i++)
					if (lines[i].TrimStart().StartsWith("--$"))
					{
						cleanupMarkerIndex = i;
						break;
					}

				string code;
				if (cleanupMarkerIndex != -1 && cleanupMarkerIndex != lines.Length - 1)
				{
					var initCode = string.Join("\n", lines.AsSpan(0, cleanupMarkerIndex));
					var cleanupCode = string.Join("\n", lines.AsSpan(cleanupMarkerIndex + 1));

					code = $"""
						{initCode}
						return function()
						{cleanupCode}
						end
						""";
				}
				else
				{
					code = originalCode;
				}

				globals["_NS"] = (LuaValue?)ns ?? LuaNil.Instance;
				try
				{
					var ret = lua.Execute(code);

					if (ret.Count > 0 && ret[0] is LuaFunction cleanupFunc)
					{
						return () =>
						{
							globals["_NS"] = (LuaValue?)ns ?? LuaNil.Instance;
							try
							{
								cleanupFunc.Invoke(lua.GetState().CreateContext(globals));
							}
							finally
							{
								globals.Remove("_NS");
							}
						};
					}

					return null;
				}
				finally
				{
					globals.Remove("_NS");
				}
			};
		}
	}
}
