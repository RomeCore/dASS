# Synchronization model

Status: open
Type: grilling
Blocked by: 02

## Question

Synchronization today reduces to one named group per tool, backed by a single lock. This ticket designs the richer model: what keys an invocation emits — ordinary keys plus file-system keys that distinguish files from directories — who emits them (declared statically on the tool, or derived per invocation from the arguments), how the acquire/release scope wraps the inner steps, and how keys are ordered so that tools launched concurrently cannot deadlock each other. This is feature goal three.

## Answer

<!-- appended on resolution -->

## Comments

<!-- appended conversation -->
