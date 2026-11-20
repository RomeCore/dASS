namespace LLMDesktopAssistant.LLM.MVVM
{
	public static class ChatTopicHashColor
	{
		public static string GetHex(string? topic)
		{
			if (string.IsNullOrWhiteSpace(topic))
				return "#808080";

			var hash = GetDeterministicHashCode(topic);

			const double min = 0.35;
			const double range = 0.65;

			double rh = (hash * 397 % 255) / 255.0;
			byte r2 = (byte)(min * 255 + rh * range * 255);
			double rg = (hash * 137 * 397 % 255) / 255.0;
			byte g2 = (byte)(min * 255 + rg * range * 255);
			double rb = (hash * 37 * 397 % 255) / 255.0;
			byte b2 = (byte)(min * 255 + rb * range * 255);

			return $"#{r2:X2}{g2:X2}{b2:X2}";
		}

		private static int GetDeterministicHashCode(string str)
		{
			unchecked
			{
				int hash1 = (5381 << 16) + 5381;
				int hash2 = hash1;

				for (int i = 0; i < str.Length; i += 2)
				{
					hash1 = ((hash1 << 5) + hash1) ^ str[i];
					if (i + 1 < str.Length)
						hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
				}

				return hash1 + hash2 * 1566083941;
			}
		}
	}
}
