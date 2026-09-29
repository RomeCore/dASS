using System;
using System.Collections.Generic;
using System.Text;

namespace LLMDesktopAssistant.Utils
{
	public static class DebugHelper
	{
#if DEBUG
		public const bool IsDebug = true;
#else
		public const bool IsDebug = false;
#endif
	}
}
