# Tools

Tools are the assistant's hands. They let the model act on your system instead of
only talking.

## Tool categories

| Category | Examples |
|---|---|
| Filesystem | read, write, edit, search files and directories |
| Web | fetch URLs, search the web, download files |
| Database | query SQLite, PostgreSQL and SQL Server databases |
| Scripting | run Lua and Python scripts |
| Memory | store and retrieve facts and logs |
| Math | evaluate expressions, solve equations |
| Random | dice rolls, random numbers, GUIDs |

## Human-in-the-loop

Dangerous tools ask for **confirmation** before running. The request shows a preview
of what the tool will do — you can approve or decline.

> [!WARNING]
> Tools that modify or delete files always require confirmation. Review the diff
> carefully before accepting it.

See also: [Meta tools](tools/metatools.md), [Scripting](tools/scripting.md).
