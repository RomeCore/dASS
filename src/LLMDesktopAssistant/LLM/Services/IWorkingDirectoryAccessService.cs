using LLMDesktopAssistant.LLM.Settings;

namespace LLMDesktopAssistant.LLM.Services
{
	public interface IWorkingDirectoryAccessService
	{
		string CheckedAccessPath(string path, DirectoryAccessMode mode, out bool isAccessed);
		string GetWorkingDirectory();
	}
}