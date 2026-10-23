using System.Collections;
using System.Reflection;
using Avalonia;
using LiveMarkdown.Avalonia;
using LLMDesktopAssistant.Markdown.Controls;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Services.Instances;
using Serilog;
using TextMateSharp.Grammars;

namespace LLMDesktopAssistant.Markdown;

[Service]
public class MarkdownCodeBlockHighlightConfigurator
{
	public MarkdownCodeBlockHighlightConfigurator(TextMateLoader loader)
	{
		ExtendedCodeBlock.LanguageProperty.Changed.AddClassHandler<ExtendedCodeBlock>((block, e) =>
		{
			var lang = block.Language?.ToLowerInvariant();
			block.ColorTheme = lang switch
			{
				"csharp" or "cs" or "c#" => ThemeName.Monokai,
				"c++" or "cpp" or "c" => ThemeName.OneDark,
				"java" => ThemeName.Dracula,
				"kotlin" or "kt" => ThemeName.Dracula,
				"scala" => ThemeName.SolarizedDark,
				"swift" => ThemeName.TomorrowNightBlue,
				"objective-c" or "objc" or "m" or "mm" => ThemeName.AtomOneDark,

				"javascript" or "js" => ThemeName.TomorrowNightBlue,
				"typescript" or "ts" => ThemeName.TomorrowNightBlue,
				"jsx" => ThemeName.TomorrowNightBlue,
				"tsx" => ThemeName.TomorrowNightBlue,
				"vue" => ThemeName.Monokai,
				"svelte" => ThemeName.Dracula,
				"coffeescript" or "coffee" => ThemeName.QuietLight,

				"python" or "py" => ThemeName.Dracula,
				"ruby" or "rb" => ThemeName.Red,
				"php" => ThemeName.AtomOneDark,
				"perl" or "pl" => ThemeName.SolarizedDark,
				"lua" => ThemeName.AtomOneLight,

				"fsharp" or "fs" or "f#" => ThemeName.SolarizedDark,
				"haskell" or "hs" => ThemeName.KimbieDark,
				"rust" or "rs" => ThemeName.DarkPlus,
				"go" or "golang" => ThemeName.Dracula,
				"ocaml" or "ml" => ThemeName.SolarizedLight,
				"erlang" or "erl" => ThemeName.KimbieDark,
				"elixir" or "ex" => ThemeName.AtomOneDark,
				"clojure" or "clj" => ThemeName.QuietLight,
				"julia" or "jl" => ThemeName.DimmedMonokai,
				"dart" => ThemeName.Dracula,
				"nim" => ThemeName.AtomOneDark,
				"crystal" => ThemeName.Dracula,
				"zig" => ThemeName.AtomOneLight,
				"racket" or "rkt" => ThemeName.VisualStudioLight,

				"html" => ThemeName.Monokai,
				"css" => ThemeName.Monokai,
				"scss" or "sass" => ThemeName.VisualStudioLight,
				"less" => ThemeName.VisualStudioLight,
				"xml" => ThemeName.Monokai,
				"json" => ThemeName.LightPlus,
				"yaml" or "yml" => ThemeName.LightPlus,
				"toml" => ThemeName.LightPlus,
				"markdown" or "md" => ThemeName.QuietLight,

				"bash" or "sh" or "zsh" => ThemeName.AtomOneDark,
				"powershell" or "ps1" => ThemeName.DimmedMonokai,
				"batch" or "bat" or "cmd" => ThemeName.DimmedMonokai,
				"awk" => ThemeName.AtomOneDark,
				"sed" => ThemeName.AtomOneDark,

				"sql" => ThemeName.SolarizedDark,
				"mysql" => ThemeName.SolarizedDark,
				"postgresql" or "pgsql" => ThemeName.SolarizedDark,
				"sqlite" => ThemeName.SolarizedDark,
				"mongodb" => ThemeName.DimmedMonokai,
				"graphql" or "gql" => ThemeName.Dracula,

				"dockerfile" or "docker" => ThemeName.AtomOneDark,
				"makefile" or "make" => ThemeName.Dracula,
				"cmake" => ThemeName.Dracula,
				"terraform" or "tf" => ThemeName.AtomOneDark,
				"hcl" => ThemeName.AtomOneDark,
				"yara" => ThemeName.KimbieDark,
				"proto" or "protobuf" => ThemeName.VisualStudioLight,
				"thrift" => ThemeName.VisualStudioLight,

				"assembly" or "asm" or "s" => ThemeName.AtomOneDark,
				"nasm" => ThemeName.AtomOneDark,
				"masm" => ThemeName.AtomOneDark,
				"arm" => ThemeName.AtomOneDark,
				"wasm" => ThemeName.AtomOneDark,

				"fortran" or "f" or "f90" or "f95" => ThemeName.SolarizedLight,
				"cobol" or "cob" => ThemeName.VisualStudioLight,
				"ada" => ThemeName.VisualStudioLight,
				"pascal" => ThemeName.VisualStudioLight,
				"lisp" or "lsp" => ThemeName.QuietLight,
				"scheme" or "scm" => ThemeName.QuietLight,
				"prolog" => ThemeName.SolarizedLight,
				"smalltalk" => ThemeName.QuietLight,

				"r" => ThemeName.DimmedMonokai,
				"matlab" or "m" => ThemeName.DimmedMonokai,
				"jupyter" or "ipynb" => ThemeName.Dracula,

				"text" or "plain" or "txt" => ThemeName.VisualStudioLight,
				"diff" or "patch" => ThemeName.AtomOneDark,
				"regex" or "regexp" => ThemeName.AtomOneDark,
				"dot" or "dotnet" => ThemeName.Monokai,
				"git" => ThemeName.KimbieDark,
				"solidity" or "sol" => ThemeName.Dracula,
				"vba" => ThemeName.VisualStudioLight,
				"verilog" or "v" => ThemeName.SolarizedLight,
				"vhdl" or "vhd" => ThemeName.SolarizedLight,
				"tex" or "latex" => ThemeName.QuietLight,

				"llt" => ThemeName.Monokai,

				_ => ThemeName.DarkPlus
			};
		});

		LoadGrammars(loader);
	}

	private static void LoadGrammars(TextMateLoader loader)
	{
		try
		{
			var shType = typeof(SyntaxHighlighting);

			var registryField = shType.GetField("RegistryOptions",
				BindingFlags.Static | BindingFlags.NonPublic);

			if (registryField?.GetValue(null) is not RegistryOptions registry)
			{
				Log.Error("[MarkdownCodeBlockHighlightConfigurator] RegistryOptions not found via reflection");
				return;
			}

			loader.LoadGrammarsInto(registry);

			var cacheField = shType.GetField("LanguageCache",
				BindingFlags.Static | BindingFlags.NonPublic);

			if (cacheField?.GetValue(null) is IDictionary cache)
			{
				cache.Clear();
			}
		}
		catch (Exception ex)
		{
			Log.Error(ex, "[MarkdownCodeBlockHighlightConfigurator] Failed to load grammars");
		}
	}

}
