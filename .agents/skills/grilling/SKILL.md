---
name: grilling
description: Grill the user relentlessly about a plan, decision, or idea. Use when the user wants to stress-test their thinking, or uses any 'grill' trigger phrases.
---

Interview the user relentlessly until you reach a shared understanding. Map this as a **design tree**: every decision branches into the decisions that hang off it. The tree and its frontier are the artifact; how you put the questions to the user is only a dispatch strategy (see Dispatch below).

Work the tree in **rounds**. The **frontier** is every decision whose prerequisites are already settled: the questions you can ask _now_ without guessing at answers you haven't heard yet. Ask the whole frontier in one round: number each question and give your recommended answer. Then wait for the user's answers before the next round.

Format a round like so:

```
❓ **Q1** - **<question title>**: <question body, might be multiple paragraphs, including multiple choices>

➡️ <your recommended answer>

---

❓ **Q2** - **<question title>**: <question body, might be multiple paragraphs, including multiple choices>

➡️ <your recommended answer>
```

Each round the user answers reshapes the tree: settled decisions push the frontier outward and unblock questions that depended on them. Recompute the frontier and ask the next round. A question whose answer depends on another question still open in this round belongs to a _later_ round, not this one.

## Dispatch: rounds or one at a time

The tree and the frontier are fixed; only how you put the questions varies.

Default: **rounds** - ask the whole frontier in one go (the format above).

Switch to **one at a time** - a single question per turn, then recompute - when any of these holds:

- **The question is a fork**: two or more plausible answers each open a _different_ subtree, so the questions below it cannot be priced until it lands.
- **The question is contested or scope-defining**: the user has pushed back on it, or it settles the destination or the out-of-scope boundary.
- **The last round came back hedged**: partial answers, or straight assent to your recommendations with no argument - the batch is being rubber-stamped, not answered.
- **The user asks for it** ("one at a time", "no, let's go one by one").

In one-at-a-time mode you still state where the question sits in the tree, still give your recommended answer, and still recompute the frontier after each reply. Keep the tally visible each turn ("3 settled, 2 open") so an open-ended conversation cannot drift: the session still ends only when the frontier is empty.

A caller may name the mode: when it does (`Mode: rounds` / `Mode: one-at-a-time`), obey it. Otherwise choose per the rules above and say which mode you are in.

Finding _facts_ is your job, never the user's. When a frontier question needs a fact from the environment (filesystem, tools, etc.), dispatch a sub-agent to find it; don't ask the user for anything you could look up yourself. Don't block on it: a running exploration is an unsettled prerequisite, so only the questions downstream of it wait for the sub-agent to report; ask the rest of the frontier now. The _decisions_ are the user's: put each to them and wait.

The session is done when the frontier is empty: every branch of the design tree visited, nothing left silently assumed. Do not act on it until the user confirms you have reached a shared understanding.
