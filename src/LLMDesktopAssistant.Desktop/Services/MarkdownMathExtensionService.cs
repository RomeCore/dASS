using LiveMarkdown.Avalonia;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Desktop.Services
{
	/// <summary>
	/// The dummy service that just adds Math nodes to the markdown renderer.
	/// </summary>
	[Service]
	public class MarkdownMathExtensionService
	{
		public MarkdownMathExtensionService()
		{
			MarkdownRenderer.ConfigurePipeline += x => x.UseExtendedMathematics();
			MarkdownNode.Register<MathInlineNode>();
			MarkdownNode.Register<MathBlockNode>();
		}
	}
}