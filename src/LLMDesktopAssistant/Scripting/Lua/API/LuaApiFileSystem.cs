using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Utils.Files;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for filesystem operations: <c>fs.*</c>.
	/// All paths are resolved relative to the chat's working directory.
	/// </summary>
	[LuaApi(chatScoped: true)]
	public class LuaApiFileSystem : LuaApiBase
	{
		public override string? Namespace => "fs";

		public override string? Manuals => """
			--- fs — filesystem operations API

			Provides safe filesystem access within the chat's working directory.
			All paths are relative to the working directory.
			Path traversal outside the working directory is blocked.

			FUNCTIONS:

			--- fs.read(path)
			  Reads a text file and returns its full content as a string.
			  Parameters:
			    - path: string — path relative to working directory
			  Returns: string

			--- fs.read_lines(path)
			  Reads a text file and returns lines as an array of strings.
			  Parameters:
			    - path: string — path relative to working directory
			  Returns: table — array of strings

			--- fs.read_binary(path)
			  Reads a binary file and returns bytes as an array of numbers (0-255).
			  Parameters:
			    - path: string — path relative to working directory
			  Returns: table — array of integers
			
			--- fs.is_text(path)
			  Checks whether a file is text-only (non-binary).
			  Parameters:
			    - path: string — path relative to working directory
			  Returns: boolean — true when the file is non-binary;
			           false when it is binary, missing or outside the working directory
			
			--- fs.is_binary(path)
			  Checks whether a file is binary (contains non-text characters).
			  Parameters:
			    - path: string — path relative to working directory
			  Returns: boolean — true when the file looks binary;
			           false when it is text, missing or outside the working directory

			--- fs.write(path, content)
			  Writes text content to a file (overwrites if exists).
			  Parameters:
			    - path: string — path relative to working directory
			    - content: string — text content to write
			  Returns: nil

			--- fs.write_binary(path, bytes)
			  Writes binary content to a file (overwrites if exists).
			  Parameters:
			    - path: string — path relative to working directory
			    - bytes: table — array of integers (0-255)
			  Returns: nil

			--- fs.append(path, content)
			  Appends text content to the end of a file.
			  Parameters:
			    - path: string — path relative to working directory
			    - content: string — text content to append
			  Returns: nil

			--- fs.exists(path)
			  Checks if a file or directory exists.
			  Parameters:
			    - path: string — path relative to working directory
			  Returns: boolean

			--- fs.is_file(path)
			  Returns true if the path points to an existing file.
			  Parameters:
			    - path: string — path relative to working directory
			  Returns: boolean

			--- fs.is_dir(path)
			  Returns true if the path points to an existing directory.
			  Parameters:
			    - path: string — path relative to working directory
			  Returns: boolean

			--- fs.list(path)
			  Lists the contents of a directory.
			  Parameters:
			    - path: string — directory path relative to working directory (default: ".")
			  Returns: table — array of entry names (strings)

			--- fs.detail(path)
			  Returns detailed information about a file or directory.
			  Parameters:
			    - path: string — path relative to working directory
			  Returns: table or nil
			  Result fields:
			    - name: string — file/directory name
			    - path: string — absolute path
			    - is_file: boolean
			    - is_dir: boolean
			    - is_binary: boolean - true when the file is binary
				- line_count: number — number of lines (only for text files)
			    - size: number — file size in bytes (0 for directories)
			    - created: string — creation time (ISO format)
			    - modified: string — last write time (ISO format)

			--- fs.size(path)
			  Returns the size of a file in bytes.
			  Parameters:
			    - path: string — path relative to working directory
			  Returns: number or nil

			--- fs.copy(src, dest)
			  Copies a file or directory.
			  Parameters:
			    - src: string — source path
			    - dest: string — destination path
			  Returns: nil

			--- fs.move(src, dest)
			  Moves/renames a file or directory.
			  Parameters:
			    - src: string — source path
			    - dest: string — destination path
			  Returns: nil

			--- fs.delete(path)
			  Deletes a file.
			  Parameters:
			    - path: string — path relative to working directory
			  Returns: nil

			--- fs.delete_dir(path)
			  Deletes a directory and all its contents.
			  Parameters:
			    - path: string — path relative to working directory
			  Returns: nil

			--- fs.create_dir(path)
			  Creates a directory (and any missing parent directories).
			  Parameters:
			    - path: string — path relative to working directory
			  Returns: nil

			--- fs.join(...)
			  Joins path components with the platform separator.
			  Parameters:
			    - ...: string or number — one or more path components
			  Returns: string

			--- fs.dirname(path)
			  Returns the parent directory of the given path.
			  Parameters:
			    - path: string — path
			  Returns: string

			--- fs.basename(path)
			  Returns the file/directory name from a path.
			  Parameters:
			    - path: string — path
			  Returns: string

			--- fs.extname(path)
			  Returns the file extension (with dot).
			  Parameters:
			    - path: string — path
			  Returns: string or empty string

			--- fs.abs(path)
			  Resolves path to an absolute path inside the working directory.
			  Parameters:
			    - path: string — relative path
			  Returns: string or nil if outside working directory

			EXAMPLES:

			  -- Read a file
			  local content = fs.read("README.md")
			  print(content)

			  -- List directory
			  for _, name in ipairs(fs.list(".")) do
			    print(name)
			  end

			  -- Check and write
			  if not fs.exists("output") then
			    fs.create_dir("output")
			  end
			  fs.write("output/data.txt", "hello world")

			  -- File info
			  local info = fs.detail("README.md")
			  print(info.size, info.modified)
			""";

		private readonly IWorkingDirectoryAccessService _fileAccess;

		public LuaApiFileSystem(IWorkingDirectoryAccessService fileAccess)
		{
			_fileAccess = fileAccess;
		}

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["read"] = new LuaCallbackFunction(Read);
			ns["read_lines"] = new LuaCallbackFunction(ReadLines);
			ns["read_binary"] = new LuaCallbackFunction(ReadBinary);
			ns["is_text"] = new LuaCallbackFunction(IsText);
			ns["is_binary"] = new LuaCallbackFunction(IsBinary);
			ns["write"] = new LuaCallbackFunction(Write);
			ns["write_binary"] = new LuaCallbackFunction(WriteBinary);
			ns["append"] = new LuaCallbackFunction(Append);
			ns["exists"] = new LuaCallbackFunction(Exists);
			ns["is_file"] = new LuaCallbackFunction(IsFile);
			ns["is_dir"] = new LuaCallbackFunction(IsDir);
			ns["list"] = new LuaCallbackFunction(List);
			ns["detail"] = new LuaCallbackFunction(Detail);
			ns["size"] = new LuaCallbackFunction(Size);
			ns["copy"] = new LuaCallbackFunction(Copy);
			ns["move"] = new LuaCallbackFunction(Move);
			ns["delete"] = new LuaCallbackFunction(Delete);
			ns["delete_dir"] = new LuaCallbackFunction(DeleteDir);
			ns["create_dir"] = new LuaCallbackFunction(CreateDir);
			ns["join"] = new LuaCallbackFunction(Join);
			ns["dirname"] = new LuaCallbackFunction(DirName);
			ns["basename"] = new LuaCallbackFunction(BaseName);
			ns["extname"] = new LuaCallbackFunction(ExtName);
			ns["abs"] = new LuaCallbackFunction(Abs);
			return null;
		}

		private LuaTuple Read(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("fs.read(path): at least 1 argument expected.");
			var path = GetPath(args, 0);
			return new LuaTuple(new LuaString(File.ReadAllText(path)));
		}

		private LuaTuple ReadLines(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("fs.read_lines(path): at least 1 argument expected.");
			var path = GetPath(args, 0);
			var lines = File.ReadAllLines(path);
			var result = new LuaTable();
			for (int i = 0; i < lines.Length; i++)
				result[i + 1] = new LuaString(lines[i]);
			return new LuaTuple(result);
		}

		private LuaTuple ReadBinary(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("fs.read_binary(path): at least 1 argument expected.");
			var path = GetPath(args, 0);
			var bytes = File.ReadAllBytes(path);
			var result = new LuaTable();
			for (int i = 0; i < bytes.Length; i++)
				result[i + 1] = new LuaNumber(bytes[i]);
			return new LuaTuple(result);
		}

		private LuaTuple IsText(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("fs.is_text(path): at least 1 argument expected.");
			if (args[0] is not LuaString pathVal)
				throw new LuaRuntimeException("fs.is_text(): first argument must be a string.");

			var fullPath = _fileAccess.TryAccessPath(pathVal.Value, DirectoryAccessMode.Read);
			if (fullPath == null || !File.Exists(fullPath))
				return new LuaTuple(LuaBoolean.FromBoolean(false));

			return new LuaTuple(LuaBoolean.FromBoolean(!FileUtils.IsBinaryFile(fullPath)));
		}

		private LuaTuple IsBinary(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("fs.is_binary(path): at least 1 argument expected.");
			if (args[0] is not LuaString pathVal)
				throw new LuaRuntimeException("fs.is_binary(): first argument must be a string.");

			var fullPath = _fileAccess.TryAccessPath(pathVal.Value, DirectoryAccessMode.Read);
			if (fullPath == null || !File.Exists(fullPath))
				return new LuaTuple(LuaBoolean.FromBoolean(false));

			return new LuaTuple(LuaBoolean.FromBoolean(FileUtils.IsBinaryFile(fullPath)));
		}

		private LuaTuple Write(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("fs.write(path, content): at least 2 arguments expected.");
			var path = GetPath(args, 0);
			if (args[1] is not LuaString contentVal)
				throw new LuaRuntimeException("fs.write(): second argument must be a string.");
			File.WriteAllText(path, contentVal.Value);
			return new LuaTuple(LuaNil.Instance);
		}

		private LuaTuple WriteBinary(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("fs.write_binary(path, bytes): at least 2 arguments expected.");
			var path = GetPath(args, 0);
			if (args[1] is not LuaTable bytesTable)
				throw new LuaRuntimeException("fs.write_binary(): second argument must be a table of bytes.");
			// NOTE: iterate by numeric indexes (1..Length) instead of Entries,
			// because LuaTable stores entries in a ConcurrentDictionary which does NOT
			// guarantee enumeration order - bytes would be written in random order!
			var bytes = new byte[bytesTable.Length];
			for (int i = 0; i < bytesTable.Length; i++)
			{
				if (bytesTable.Get(i + 1) is LuaNumber num)
					bytes[i] = (byte)num.Value;
			}
			File.WriteAllBytes(path, bytes);
			return new LuaTuple(LuaNil.Instance);
		}

		private LuaTuple Append(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("fs.append(path, content): at least 2 arguments expected.");
			var path = GetPath(args, 0);
			if (args[1] is not LuaString contentVal)
				throw new LuaRuntimeException("fs.append(): second argument must be a string.");
			File.AppendAllText(path, contentVal.Value);
			return new LuaTuple(LuaNil.Instance);
		}

		private LuaTuple Exists(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("fs.exists(path): at least 1 argument expected.");
			if (args[0] is not LuaString pathVal)
				throw new LuaRuntimeException("fs.exists(): first argument must be a string.");
			var fullPath = _fileAccess.TryAccessPath(pathVal.Value, DirectoryAccessMode.Read);
			return new LuaTuple(LuaBoolean.FromBoolean(fullPath != null && (File.Exists(fullPath) || Directory.Exists(fullPath))));
		}

		private LuaTuple IsFile(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("fs.is_file(path): at least 1 argument expected.");
			if (args[0] is not LuaString pathVal)
				throw new LuaRuntimeException("fs.is_file(): first argument must be a string.");
			var fullPath = _fileAccess.TryAccessPath(pathVal.Value, DirectoryAccessMode.Read);
			return new LuaTuple(LuaBoolean.FromBoolean(fullPath != null && File.Exists(fullPath)));
		}

		private LuaTuple IsDir(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("fs.is_dir(path): at least 1 argument expected.");
			if (args[0] is not LuaString pathVal)
				throw new LuaRuntimeException("fs.is_dir(): first argument must be a string.");
			var fullPath = _fileAccess.TryAccessPath(pathVal.Value, DirectoryAccessMode.Read);
			return new LuaTuple(LuaBoolean.FromBoolean(fullPath != null && Directory.Exists(fullPath)));
		}

		private LuaTuple List(LuaCallingContext ctx, LuaValue[] args)
		{
			var path = args.Length > 0 && args[0] is LuaString pathStr ? pathStr.Value : ".";
			var fullPath = _fileAccess.TryAccessPath(path, DirectoryAccessMode.Read);
			if (fullPath == null)
				throw new LuaRuntimeException($"fs.list(): path '{path}' is outside working directory.");
			if (!Directory.Exists(fullPath))
				throw new LuaRuntimeException($"fs.list(): directory '{path}' does not exist.");

			var entries = Directory.GetFileSystemEntries(fullPath);
			var result = new LuaTable();
			for (int i = 0; i < entries.Length; i++)
				result[i + 1] = new LuaString(Path.GetFileName(entries[i]));
			return new LuaTuple(result);
		}

		private LuaTuple Detail(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("fs.detail(path): at least 1 argument expected.");
			if (args[0] is not LuaString pathVal)
				throw new LuaRuntimeException("fs.detail(): first argument must be a string.");
			var fullPath = _fileAccess.TryAccessPath(pathVal.Value, DirectoryAccessMode.Read);
			if (fullPath == null)
				return new LuaTuple(LuaNil.Instance);

			var t = new LuaTable();
			t["name"] = new LuaString(Path.GetFileName(fullPath));
			t["path"] = new LuaString(fullPath);
			t["is_file"] = LuaBoolean.FromBoolean(File.Exists(fullPath));
			t["is_dir"] = LuaBoolean.FromBoolean(Directory.Exists(fullPath));

			try
			{
				var info = new FileInfo(fullPath);
				if (info.Exists)
				{
					var fileMetrics = FileUtils.GetFileMetrics(fullPath);
					t["is_binary"] = LuaBoolean.FromBoolean(fileMetrics.IsBinary);
					t["line_count"] = new LuaNumber(fileMetrics.LineCount ?? 0);
					t["size"] = new LuaNumber(info.Length);
					t["created"] = new LuaString(info.CreationTimeUtc.ToString("O"));
					t["modified"] = new LuaString(info.LastWriteTimeUtc.ToString("O"));
				}
				else
				{
					var dirInfo = new DirectoryInfo(fullPath);
					if (dirInfo.Exists)
					{
						t["size"] = new LuaNumber(0);
						t["created"] = new LuaString(dirInfo.CreationTimeUtc.ToString("O"));
						t["modified"] = new LuaString(dirInfo.LastWriteTimeUtc.ToString("O"));
					}
				}
			}
			catch
			{
			}

			return new LuaTuple(t);
		}

		private LuaTuple Size(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("fs.size(path): at least 1 argument expected.");
			if (args[0] is not LuaString pathVal)
				throw new LuaRuntimeException("fs.size(): first argument must be a string.");
			var fullPath = _fileAccess.TryAccessPath(pathVal.Value, DirectoryAccessMode.Read);
			if (fullPath == null || !File.Exists(fullPath))
				return new LuaTuple(LuaNil.Instance);
			return new LuaTuple(new LuaNumber(new FileInfo(fullPath).Length));
		}

		private LuaTuple Copy(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("fs.copy(src, dest): at least 2 arguments expected.");
			var src = GetPath(args, 0);
			var dest = GetPath(args, 1);

			if (Directory.Exists(src))
				CopyDirectoryRecursive(src, dest);
			else
				File.Copy(src, dest, overwrite: true);

			return new LuaTuple(LuaNil.Instance);
		}

		private LuaTuple Move(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("fs.move(src, dest): at least 2 arguments expected.");
			var src = GetPath(args, 0);
			var dest = GetPath(args, 1);

			if (Directory.Exists(src))
				Directory.Move(src, dest);
			else
				File.Move(src, dest, overwrite: true);

			return new LuaTuple(LuaNil.Instance);
		}

		private LuaTuple Delete(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("fs.delete(path): at least 1 argument expected.");
			var path = GetPath(args, 0);
			File.Delete(path);
			return new LuaTuple(LuaNil.Instance);
		}

		private LuaTuple DeleteDir(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("fs.delete_dir(path): at least 1 argument expected.");
			var path = GetPath(args, 0);
			Directory.Delete(path, recursive: true);
			return new LuaTuple(LuaNil.Instance);
		}

		private LuaTuple CreateDir(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("fs.create_dir(path): at least 1 argument expected.");
			var path = GetPath(args, 0);
			Directory.CreateDirectory(path);
			return new LuaTuple(LuaNil.Instance);
		}

		private static LuaTuple Join(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("fs.join(...): at least 1 argument expected.");
			var parts = new string[args.Length];
			for (int i = 0; i < args.Length; i++)
			{
				if (args[i] is not LuaString s)
					throw new LuaRuntimeException($"fs.join(): argument {i + 1} must be a string or number.");
				parts[i] = s.Value;
			}
			return new LuaTuple(new LuaString(Path.Combine(parts)));
		}

		private static LuaTuple DirName(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("fs.dirname(path): at least 1 argument expected.");
			if (args[0] is not LuaString pathVal)
				throw new LuaRuntimeException("fs.dirname(): first argument must be a string.");
			return new LuaTuple(new LuaString(Path.GetDirectoryName(pathVal.Value) ?? ""));
		}

		private static LuaTuple BaseName(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("fs.basename(path): at least 1 argument expected.");
			if (args[0] is not LuaString pathVal)
				throw new LuaRuntimeException("fs.basename(): first argument must be a string.");
			return new LuaTuple(new LuaString(Path.GetFileName(pathVal.Value)));
		}

		private static LuaTuple ExtName(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("fs.extname(path): at least 1 argument expected.");
			if (args[0] is not LuaString pathVal)
				throw new LuaRuntimeException("fs.extname(): first argument must be a string.");
			return new LuaTuple(new LuaString(Path.GetExtension(pathVal.Value)));
		}

		private LuaTuple Abs(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("fs.abs(path): at least 1 argument expected.");
			if (args[0] is not LuaString pathVal)
				throw new LuaRuntimeException("fs.abs(): first argument must be a string.");
			var full = _fileAccess.TryAccessPath(pathVal.Value, DirectoryAccessMode.Read);
			if (full == null)
				return new LuaTuple(LuaNil.Instance);
			return new LuaTuple(new LuaString(full));
		}

		// --- Helpers ---

		private string GetPath(LuaValue[] args, int index)
		{
			if (args[index] is not LuaString pathVal)
				throw new LuaRuntimeException($"Argument {index + 1} must be a string (path).");
			return _fileAccess.AccessPath(pathVal.Value, DirectoryAccessMode.Read);
		}

		private static void CopyDirectoryRecursive(string source, string dest)
		{
			Directory.CreateDirectory(dest);
			foreach (var file in Directory.GetFiles(source))
			{
				var destFile = Path.Combine(dest, Path.GetFileName(file));
				File.Copy(file, destFile, overwrite: true);
			}
			foreach (var dir in Directory.GetDirectories(source))
			{
				var destDir = Path.Combine(dest, Path.GetFileName(dir));
				CopyDirectoryRecursive(dir, destDir);
			}
		}
	}
}
