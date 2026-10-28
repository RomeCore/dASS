# AR-0028 - The current participant shows as anonymous right after login, until the page is reloaded

- **Status:** open
- **Severity:** visible
- **Lane:** user
- **Surface:** UI
- **First seen:** 2026-10-29

## What I expected

As soon as login succeeds, the room knows who I am: the header shows my name, my profile panel is filled, and my messages are attributed to me.

## What happened

Immediately after login the header shows the anonymous placeholder instead of the participant's name, and the participant's own identity is missing elsewhere. A full browser reload of the same page corrects it, and the corrected state does not regress afterwards.

Traced: login completes through a client-side call that sets the auth cookie, and then navigates to the chat page without a full document load, so the interactive session that serves the chat is the one that was created for the login page - a request that carried no identity. Participant resolution reads the identity from the request context, and an enhanced navigation does not re-acquire it. A reload starts a new session whose first request carries the cookie, which is why the reload fixes it.

## Why it costs

The first minutes of every session are wrong, and the wrongness is not cosmetic: the header, the profile panel and the attribution of the user's own messages all depend on the same resolution. Any authorization check built on participant identity would deny everything immediately after login and allow everything after a reload, which makes the bug a correctness problem for permissions rather than a display problem.

## Repro

Log in through the login page and look at the header without touching the browser. Reload the page and compare.

## Witnesses

- 2026-10-29 - this session - reported by the user after logging into the WebUI.

## What I did instead

Reload the page after every login.

## Proposed fix

Stop deriving the participant from the request context alone:

- after a successful login, either force a full document load, or re-resolve the participant from the now-authenticated principal and notify the components that hold it;
- resolve the participant once per interactive session from a source that survives navigation, and cache that result on success;
- this is the same seam the participant-permissions idea lists as its prerequisite, so fixing it once serves both.

## Unverified

The exact lifetime that currently holds the cached participant across prerender and interactive rendering. The navigation mode and the request-context dependency were read from the source; the resulting lifecycle was inferred, not observed.
