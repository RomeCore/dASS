using Avalonia.Controls;
using Avalonia.Platform.Storage;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Services;
using Material.Icons;

namespace LLMDesktopAssistant.Controls.Markdown.CodeBlocks.Actions
{
	/// <summary>
	/// Offers the save button for every fenced code block.
	/// </summary>
	[Service(typeof(IMarkdownCodeBlockActionProvider))]
	public sealed class SaveCodeBlockActionProvider : IMarkdownCodeBlockActionProvider
	{
		/// <inheritdoc />
		public IReadOnlyList<IMarkdownCodeBlockAction> GetActions(MarkdownCodeBlockContext context)
			=> [new SaveCodeBlockAction()];
	}

	/// <summary>
	/// Writes the source of the block to a file the user picks, suggesting the extension of the block's language.
	/// </summary>
	public sealed class SaveCodeBlockAction : MarkdownCodeBlockAction
	{
		/// <summary>
		/// Creates the action with its icon already set.
		/// </summary>
		public SaveCodeBlockAction()
		{
			Icon = MaterialIconKind.ContentSave;
		}

		/// <inheritdoc />
		public override LocaleKeyBase Title => Locale.GetKey("common.save");

		/// <inheritdoc />
		public override int Order => 1;

		/// <inheritdoc />
		public override async Task ExecuteAsync(MarkdownCodeBlockContext context, CancellationToken cancellationToken)
		{
			if (TopLevel.GetTopLevel(context.Renderer)?.StorageProvider is not { } storage)
				return;

			var (language, text) = (context.Content.Language, context.Content.Text);

			var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
			{
				FileTypeChoices =
				[
					GetFilePickerType(language),
					new FilePickerFileType("Any files") { Patterns = ["*.*"] }
				],
				ShowOverwritePrompt = true
			});

			if (file is null)
				return;

			await File.WriteAllTextAsync(file.Path.LocalPath, text, cancellationToken);
		}

		/// <summary>
		/// Maps the info string of a block to a file type: the language as typed, with the usual aliases resolved.
		/// </summary>
		private static FilePickerFileType GetFilePickerType(string? language)
		{
			var name = string.IsNullOrWhiteSpace(language) ? "txt" : language.Trim().ToLowerInvariant();

			return name switch
			{
				// === Web Development ===
				"javascript" or "js" => new FilePickerFileType("JavaScript files") { Patterns = ["*.js"] },
				"typescript" or "ts" => new FilePickerFileType("TypeScript files") { Patterns = ["*.ts"] },
				"jsx" or "react" => new FilePickerFileType("React JSX files") { Patterns = ["*.jsx"] },
				"tsx" => new FilePickerFileType("React TSX files") { Patterns = ["*.tsx"] },
				"html" or "htm" => new FilePickerFileType("HTML files") { Patterns = ["*.html", "*.htm"] },
				"css" => new FilePickerFileType("CSS stylesheets") { Patterns = ["*.css"] },
				"scss" or "sass" => new FilePickerFileType("Sass files") { Patterns = ["*.scss", "*.sass"] },
				"less" => new FilePickerFileType("Less files") { Patterns = ["*.less"] },
				"vue" or "vuejs" => new FilePickerFileType("Vue components") { Patterns = ["*.vue"] },
				"svelte" => new FilePickerFileType("Svelte components") { Patterns = ["*.svelte"] },
				"astro" => new FilePickerFileType("Astro components") { Patterns = ["*.astro"] },
				"php" => new FilePickerFileType("PHP files") { Patterns = ["*.php"] },
				"wasm" or "wat" => new FilePickerFileType("WebAssembly files") { Patterns = ["*.wasm", "*.wat"] },

				// === Backend & Compiled Languages ===
				"csharp" or "c#" or "cs" => new FilePickerFileType("C# files") { Patterns = ["*.cs"] },
				"java" => new FilePickerFileType("Java files") { Patterns = ["*.java"] },
				"kotlin" or "kt" => new FilePickerFileType("Kotlin files") { Patterns = ["*.kt", "*.kts"] },
				"scala" => new FilePickerFileType("Scala files") { Patterns = ["*.scala"] },
				"go" or "golang" => new FilePickerFileType("Go files") { Patterns = ["*.go"] },
				"rust" or "rs" => new FilePickerFileType("Rust files") { Patterns = ["*.rs"] },
				"c" => new FilePickerFileType("C source files") { Patterns = ["*.c", "*.h"] },
				"cpp" or "c++" => new FilePickerFileType("C++ files") { Patterns = ["*.cpp", "*.hpp", "*.cc", "*.cxx", "*.h"] },
				"swift" => new FilePickerFileType("Swift files") { Patterns = ["*.swift"] },
				"objective-c" or "objc" => new FilePickerFileType("Objective-C files") { Patterns = ["*.m", "*.h"] },
				"zig" => new FilePickerFileType("Zig files") { Patterns = ["*.zig"] },
				"nim" => new FilePickerFileType("Nim files") { Patterns = ["*.nim"] },

				// === Scripting Languages ===
				"python" or "py" => new FilePickerFileType("Python files") { Patterns = ["*.py"] },
				"ruby" or "rb" => new FilePickerFileType("Ruby files") { Patterns = ["*.rb"] },
				"perl" or "pl" => new FilePickerFileType("Perl files") { Patterns = ["*.pl", "*.pm"] },
				"lua" => new FilePickerFileType("Lua files") { Patterns = ["*.lua"] },
				"r" or "rlang" => new FilePickerFileType("R scripts") { Patterns = ["*.r"] },
				"julia" or "jl" => new FilePickerFileType("Julia files") { Patterns = ["*.jl"] },
				"dart" => new FilePickerFileType("Dart files") { Patterns = ["*.dart"] },

				// === Shell & Configuration ===
				"shell" or "bash" or "sh" => new FilePickerFileType("Shell scripts") { Patterns = ["*.sh", "*.bash"] },
				"powershell" or "ps1" => new FilePickerFileType("PowerShell scripts") { Patterns = ["*.ps1", "*.psm1", "*.psd1"] },
				"zsh" => new FilePickerFileType("Zsh scripts") { Patterns = ["*.zsh"] },
				"fish" => new FilePickerFileType("Fish shell scripts") { Patterns = ["*.fish"] },
				"cmd" or "batch" => new FilePickerFileType("Batch files") { Patterns = ["*.bat", "*.cmd"] },
				"dockerfile" or "docker" => new FilePickerFileType("Dockerfiles") { Patterns = ["Dockerfile", "*.dockerfile"] },
				"yaml" or "yml" => new FilePickerFileType("YAML files") { Patterns = ["*.yaml", "*.yml"] },
				"json" => new FilePickerFileType("JSON files") { Patterns = ["*.json"] },
				"xml" => new FilePickerFileType("XML files") { Patterns = ["*.xml"] },
				"toml" => new FilePickerFileType("TOML files") { Patterns = ["*.toml"] },
				"ini" or "cfg" => new FilePickerFileType("INI configuration files") { Patterns = ["*.ini", "*.cfg", "*.conf"] },
				"env" => new FilePickerFileType("Environment files") { Patterns = ["*.env"] },
				"gitignore" => new FilePickerFileType("Git ignore files") { Patterns = [".gitignore"] },

				// === Database & Query Languages ===
				"sql" => new FilePickerFileType("SQL files") { Patterns = ["*.sql"] },
				"plsql" => new FilePickerFileType("PL/SQL files") { Patterns = ["*.pls", "*.plsql"] },
				"tsql" or "transact-sql" => new FilePickerFileType("T-SQL files") { Patterns = ["*.sql"] },
				"graphql" or "gql" => new FilePickerFileType("GraphQL files") { Patterns = ["*.graphql", "*.gql"] },
				"prisma" => new FilePickerFileType("Prisma schema") { Patterns = ["*.prisma"] },
				"mongodb" or "mongo" => new FilePickerFileType("MongoDB scripts") { Patterns = ["*.js"] },

				// === Functional Languages ===
				"haskell" or "hs" => new FilePickerFileType("Haskell files") { Patterns = ["*.hs", "*.lhs"] },
				"elixir" or "ex" => new FilePickerFileType("Elixir files") { Patterns = ["*.ex", "*.exs"] },
				"erlang" or "erl" => new FilePickerFileType("Erlang files") { Patterns = ["*.erl", "*.hrl"] },
				"clojure" or "clj" => new FilePickerFileType("Clojure files") { Patterns = ["*.clj", "*.cljs", "*.cljc", "*.edn"] },
				"fsharp" or "f#" or "fs" => new FilePickerFileType("F# files") { Patterns = ["*.fs", "*.fsx", "*.fsi"] },
				"ocaml" or "ml" => new FilePickerFileType("OCaml files") { Patterns = ["*.ml", "*.mli"] },
				"reason" or "re" => new FilePickerFileType("ReasonML files") { Patterns = ["*.re", "*.rei"] },

				// === Markup & Documentation ===
				"markdown" or "md" => new FilePickerFileType("Markdown files") { Patterns = ["*.md", "*.markdown"] },
				"mdx" => new FilePickerFileType("MDX files") { Patterns = ["*.mdx"] },
				"rst" or "restructuredtext" => new FilePickerFileType("reStructuredText files") { Patterns = ["*.rst"] },
				"tex" or "latex" => new FilePickerFileType("LaTeX files") { Patterns = ["*.tex", "*.sty", "*.cls"] },
				"asciidoc" or "adoc" => new FilePickerFileType("AsciiDoc files") { Patterns = ["*.adoc", "*.asciidoc"] },
				"text" or "txt" or "plaintext" => new FilePickerFileType("Plain text files") { Patterns = ["*.txt"] },

				// === Other ===
				"groovy" => new FilePickerFileType("Groovy files") { Patterns = ["*.groovy", "*.gvy", "*.gy", "*.gsh"] },
				"apex" => new FilePickerFileType("Apex files") { Patterns = ["*.apex", "*.cls", "*.trigger"] },
				"vhdl" or "vhd" => new FilePickerFileType("VHDL files") { Patterns = ["*.vhd", "*.vhdl"] },
				"verilog" or "v" => new FilePickerFileType("Verilog files") { Patterns = ["*.v", "*.sv"] },
				"cmake" => new FilePickerFileType("CMake files") { Patterns = ["CMakeLists.txt", "*.cmake"] },
				"makefile" or "make" => new FilePickerFileType("Makefiles") { Patterns = ["Makefile", "*.mk"] },
				"nginx" => new FilePickerFileType("Nginx config files") { Patterns = ["*.conf"] },

				_ => new FilePickerFileType($"{name} files") { Patterns = [$"*.{name}"] }
			};
		}
	}
}
