# tools/packet-capture

Tooling for external packet verification (`agent_start.md` section 12.5): capturing and
summarizing traffic **outside** the Windows guest (hypervisor, gateway, or capture host) to
prove that no unauthorized packet escaped in locked mode. A Windows-local log is not
sufficient proof.

Will contain capture orchestration and assertion/summary tooling consumed by the locked-mode
acceptance test.

_Placeholder until the external packet-capture acceptance test is implemented (Milestone 3)._
