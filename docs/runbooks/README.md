# Runbooks

Operational recovery procedures required by [`agent_start.md`](../../agent_start.md) section
16. Each runbook will be a step-by-step, tested procedure that an operator can follow under
stress, with verification steps and explicit fail-closed expectations.

Planned runbooks:

- **Locked-mode recovery** - restore normal networking via the local, authenticated emergency
  path without creating a permanent bypass (see `scripts/restore-network.ps1`). _(Milestone 3)_
- **Database recovery** - recover from a locked or corrupted local SQLite store without
  relaxing enforcement. _(Milestone 2)_
- **Failed update-window recovery** - restore locked mode after a cancelled, crashed, or
  partially completed update window. _(Milestone 6)_
- **Policy rollback** - revert a policy to its recorded original state and verify. _(Milestone 2)_
- **Diagnostic collection** - gather local logs and state for troubleshooting without
  capturing secrets or unrelated user data. _(Milestone 1)_

_No runbooks are finalized yet; they are authored alongside the milestones noted above._
