# Shared execution services and their scopes

Status: open
Type: grilling
Blocked by: 01

## Question

Enumerate the reusable services that make up the decomposed core — the units every orchestrator calls: argument validation, synchronization, preview, policy and specifiers, confirmation, execution, and settlement (status and result reporting). For each one settle its responsibility, its boundaries, what it needs from the domain model, and which dependency-injection scope it lives in (chat versus application). This ticket also settles the fate of the existing consent machinery — the approval decision service, the memorization service, the consent panel, and the persisted "always" decisions: do they survive as they are, move into the new core, or get replaced. This is where the duplication between the two worlds is actually removed.

## Answer

<!-- appended on resolution -->

## Comments

<!-- appended conversation -->
