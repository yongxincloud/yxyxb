# Preserve planned backup times across catch-up, retries, and manual runs

**Status:** accepted
**Date:** 2026-11-10

A task's planned time is its recurring `Asia/Shanghai` wall-clock execution time, independent of the engine's actual next attempt time. When the task engine starts after a planned occurrence was missed, it runs one catch-up backup immediately and then schedules the next planned occurrence after the current time; manual runs and failed-run retries also leave the planned time unchanged. Planned occurrences crossed by a long-running backup are skipped rather than executed consecutively.

## Considered Options

- Wait for the next planned occurrence after restart: avoids an immediate workload, but leaves the outage period without a fresh backup.
- Rebase the recurrence on the actual run time: preserves the current behavior, but silently changes the customer's chosen schedule after any catch-up, manual run, or retry.
- Execute every missed occurrence: guarantees one run per period, but can create a backup storm after a long outage.

## Consequences

- The persisted next attempt time cannot also serve as the recurrence anchor.
- Existing task data needs a migration that derives an initial recurrence anchor without changing the next scheduled attempt during deployment.
- Task editors and schedule displays must show the planned time rather than exposing a retry time as the customer's configuration.
