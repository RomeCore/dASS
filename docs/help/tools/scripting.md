# Scripting

dASS has two scripting engines available to the model:

| Engine | Extension | Description |
|---|---|---|
| **AsyncLua** | `.lua` | Lua interpreter with async/await and a wide API |
| **Python** | `.py` | Python in the configured `.venv` or global environment |

## Streaming results

Long-running scripts can stream output, set status icons and progress bars
through the `dass.tool.result` API — the UI shows them live.

> [!NOTE]
> Scripts are executed in a separate process/sandbox where possible and always
> go through the same approval pipeline as any other tool.
