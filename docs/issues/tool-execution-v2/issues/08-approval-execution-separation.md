# Separating approval from execution

Status: open
Type: grilling
Blocked by: 02

## Question

Today the file-editing tools carry the approval inside their own execution: they show their confirmation interface and then do the work in one step. This ticket settles how approval becomes a step of its own, upstream of execution; how those tools stop handling approval themselves; and what contract lets a tool still contribute a custom approval interface (the diff-based one) into the approval step without owning that step. This is feature goal two, and it is the hinge that argument editing, the approval helper, and cascade approval all hang off.

## Answer

<!-- appended on resolution -->

## Comments

<!-- appended conversation -->
