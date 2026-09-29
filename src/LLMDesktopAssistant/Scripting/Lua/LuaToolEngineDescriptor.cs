using LLMDesktopAssistant.Tools.Scripting;

namespace LLMDesktopAssistant.Scripting.Lua
{
	public class LuaToolEngineDescriptor : IScriptableToolEngineDescriptor
	{
		public ScriptLanguageType Language => ScriptLanguageType.Lua;

		public string[] Extensions => [ ".lua", ".alua" ];

		public string FrontmatterStart => "--[[";

		public string FrontmatterEnd => "]]";

		public string Examples => """
			Example arguments:
			{"location": "New York", "days": 3}

			Example code:
			-- Fetch weather data
			local url = "https://api.weather.com/forecast?q=" .. tool_args.location .. "&days=" .. tool_args.days
			local result = await web.fetch(url)
			print("Weather in " .. tool_args.location .. ": " .. result)
			""";

		public string Template => """
			--[[
			title: My Tool
			description: Describe what this tool does and when to use it.
			category: general
			approval-level: policy-based
			behaviours:
			  - file-read
			argument-schema: |
			  {
			    "type": "object",
			    "properties": {
			      "input": {
			        "type": "string",
			        "description": "The input to process"
			      }
			    },
			    "additionalProperties": false
			  }
			]]

			-- Arguments come as a table: tool_args.input
			local input = tool_args.input

			-- Return the result to the LLM (string or table)
			return "Processed: " .. tostring(input)
			""";
	}
}