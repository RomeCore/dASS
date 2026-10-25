# AR-0023 - `fs-edit` leaves a whitespace-only line where a deleted block used to be

- **Status:** open
- **Severity:** friction
- **Lane:** agent
- **Surface:** tool `fs-edit`
- **First seen:** 2026-10-25

## What I expected

`replace: ""` deletes a member cleanly: the block goes, and so does the blank line that separated it, so the file
reads as if the member was never there. The description promises that leading and trailing whitespace and common
indentation are ignored when matching, which reads as "you do not have to reproduce the spacing exactly".

## What happened

Four times in one session, a block deletion left a line containing only tabs where the separator blank line used to
be:

- `src/LLMDesktopAssistant/Utils/Directories.cs` - removing the `Metatools` property;
- `src/LLMDesktopAssistant/Addons/Parsers/FrontmatterBasedAddonParser.cs` - removing the private
  `CountUnescapedStars`, twice in the same file;
- `src/LLMDesktopAssistant.Desktop/Scripting/ExternalProcess/ExternalProcessScriptEngineBase.cs` - removing
  `ExtractJson` and `SplitPattern`.

Each time the result diff showed the residue as `+		`, so it was visible - but only to an agent who reads the diff
looking for damage. Each time it cost exactly one follow-up `fs-edit` matching the whitespace line alone, and the
first one also cost a moment of "did I break the file?".

## Why it costs

One extra call per block deletion, plus trailing whitespace left in the source until someone notices. Every member
deletion hits it, and the whole point of a patch tool is that deleting a member is one call.

## Repro

1. In a file where a member is separated from its neighbour by a blank line, `fs-edit` with `match` covering the
   member and ending at its last newline, and `replace: ""`.
2. Read the changed region: a line of tabs remains between the neighbours.

## Witnesses

- 2026-10-25 - bash engine session - four block deletions, each followed by a cleanup patch.

## What I did instead

A second `fs-edit` per occurrence, with `match` set to the whitespace-only line and `replace: ""`.

## Proposed fix

Cheapest first:

1. Document the idiom in the tool description: "an empty replacement keeps the blank line that separated the block -
   include it in `match`". One sentence, no behaviour change.
2. When `replace` is empty and the match ends at a newline, consume one following blank line as well. That is the
   behaviour an author means nine times out of ten, but it is a small surprise for the tenth.

## Unverified

Whether the residue is tabs or spaces in general - here it inherited the indentation of the removed block, and I did
not check a case where the neighbours are indented differently.
