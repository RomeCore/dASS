# Frontmatter line prefix for shell engines

Status: done
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

The implemented mode is **prefix plus markers**, not a leading run of prefixed lines: the opening and closing marker
lines carry the prefix (`#---`) and every line between them must start with it. A leading run was rejected because it
would swallow a shebang and every ordinary comment at the top of a shell file. Spec §11 was amended to say so.

## Comments

- **Closed 2026-10-25** by the first shell language (ticket 11): `.sh` addons parse through the `#` prefix, and the
  descriptor cache key carries `FrontmatterLinePrefix`, so a prefixed and an unprefixed descriptor cannot collide.
