--[[
name: secret-guard
title: Secret guard
description: Denies any filesystem tool call whose arguments name a secret file - an environment file, a private key, a credential store.
category: Hooks
hook-type: tool.pre-use
hook-name: "fs-*"
execution-flow: sequential
timeout: 2000
]]

-- The contract this hook is built on.
--
-- Input, the `payload` global:
--   tool          - the tool name `hook-name` was matched against, canonical or emitted
--   behaviour     - declared behaviour flags in kebab case, or nil
--   source        - "chat-run", "chat-restart", "command" or "agent-task"
--   arguments     - the effective argument table, what the tool would really run with
--   chat_id       - the chat, or nil
--   agent_id      - the agent whose message owns the call, or nil
--
-- Input, the `previous` global: the verdict of the earlier sequential hook, or nil.
--
-- Output: the verdict document. `decision = "disallow"` with a `reason` fails the call and the reason
-- becomes the text the model reads. `arguments` would replace the argument set. Anything else, and
-- most of all returning nothing, means "no opinion".
--
-- A hook that has nothing to add must return `previous`, not an empty table: the chain replaces the
-- verdict instead of merging it, so an empty table would reset what an earlier hook decided.
--
-- The guard never calls a tool, so it cannot recurse into its own event.

-- The templates exist to be committed and read; shipping them is their whole point.
local templates = { "%.env%.example$", "%.env%.sample$", "%.env%.template$" }

local secret_markers = {
	{ "%.env$", "environment file" },
	{ "%.env%.", "environment file" },
	{ "%.pem$", "PEM key" },
	{ "%.key$", "private key" },
	{ "%.pfx$", "certificate store" },
	{ "%.p12$", "certificate store" },
	{ "%.kdbx$", "password database" },
	{ "%.jks$", "keystore" },
	{ "%.pgpass$", "credential file" },
	{ "%.netrc$", "credential file" },
	{ "%.htpasswd$", "credential file" },
	{ "[/\\]id_rsa", "SSH private key" },
	{ "[/\\]id_ed25519", "SSH private key" },
	{ "[/\\]id_ecdsa", "SSH private key" },
	{ "[/\\]secrets?%.", "secrets file" },
	{ "[/\\]credentials?%.", "credentials file" },
}

local function is_template(text)
	for _, pattern in ipairs(templates) do
		if text:find(pattern) then
			return true
		end
	end
	return false
end

-- Every string in the argument document is a candidate: a secret path can arrive as the `path` of
-- fs-read_entry, as the `pattern` of fs-grep, or inside the body of fs-write_file.
local candidates = {}

local function collect(node)
	local node_type = type(node)
	if node_type == "string" then
		candidates[#candidates + 1] = node
	elseif node_type == "table" then
		for _, value in pairs(node) do
			collect(value)
		end
	end
end

collect(payload and payload.arguments)

-- A whole file body in the reason would bury the message.
local function preview(text)
	return #text > 120 and text:sub(1, 117) .. "..." or text
end

for _, text in ipairs(candidates) do
	local lowered = text:lower()

	if not is_template(lowered) then
		for _, marker in ipairs(secret_markers) do
			if lowered:find(marker[1]) then
				return {
					decision = "disallow",
					reason = string.format(
						"Blocked by the secret-guard hook: the %s call would touch a secret (%s: %s). " ..
						"Secrets are not readable or writable through tools - ask the user to change them by hand.",
						payload.tool or "tool",
						marker[2],
						preview(text)),
				}
			end
		end
	end
end

return previous
