# Frontmatter line prefix for shell engines

Status: open
Type: task
Blocked by: 01

## Question

Add `FrontmatterLinePrefix` to `IScriptEngineDescriptor` and `AddonParserDescriptor`, and give
`FrontmatterBasedAddonParser` its second mode (spec §11):

- **no prefix** — today's behaviour, slicing between the start and end markers: Python `"""`, Lua `--[[ ]]`;
- **with a prefix** — the frontmatter is the leading run of lines starting with the prefix, the prefix is
  stripped per line before YAML parsing, and the rest of the file is the body.

Prefixes: `#` for Bash and PowerShell, `REM ` for Batch. Every frontmatter line is a real comment, so the block
stays inert by construction and the file stays runnable as-is.

## Answer

## Comments
