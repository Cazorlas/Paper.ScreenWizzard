# One host per worktree

Several sessions work on one project at the same time, each in its own worktree, and each wants to try its
build in a running host application. Left alone they step on each other, and the measured ways are:

- **Callers choose the host differently.** One tool took "the newest host", another took "the first announce
  file in the folder". On the same machine in the same minute they chose two different hosts, and neither was
  sure to be the one the session was measuring.
- **A deployed build is often one per host version for the whole machine.** The last worktree to publish
  decides what every host of that version runs next.

A host pack says how its host is found, bound and switched on, in its live skill's references. This file
is the rule every pack follows.

## The rule

Each worktree gets its own host process. Every caller chooses the host in the same order:

1. the host **named** by its process number, when the call names one;
2. else the host **bound to this worktree**, and there is at most one (see "Binding" below);
3. else the **only free** host, meaning one bound to no worktree (typically the one the user opened by hand).
   A worktree that uses a free host **binds it first**, in the same step. From then on it is that worktree's
   host, and a second worktree that looks for a free host finds none. Two worktrees acting on one unbound host
   is the same collision this rule exists to end;
4. else **nothing is sent**, and the message lists every host seen: process number, version, open document,
   and the worktree each belongs to.

A host bound to another worktree is never called, never reloaded into, and never switched on. Two free hosts
are a question for the user, not a guess. "Newest wins" is exactly the guess that sent one session's calls
into another session's host.

## Binding

- **One record per host process.** Every session on the machine can read it, and it names the worktree by a
  stable key. Make it **the same record** the add-in reads to decide which worktree's builds that host runs.
  Two records for one fact drift apart, and then a host runs one worktree's build while the agent calls it
  from another.
- **The key is computed from a normalized path**, the same way in every language that reads it: the full
  path (`GetFullPath`), trailing separators trimmed, lower-cased, then hashed. A bridge that hashes
  `D:\Repo\wt` while a script hashes `d:\repo\wt\` sees its own host as "bound to another worktree".
- **Record the process start time** with the binding. Windows reuses process numbers, so a record whose
  process is gone, **or whose process started at a different time**, belongs to a closed host. It is removed
  on the next read, and it never makes a new process "bound" or blocks a bind.
- **One host per worktree at a time.** Binding a second host to a worktree moves the binding: the first host
  becomes free. A session never has its baseline taken in one host and its verification in another without
  having bound the second itself.
- **A host started from a worktree is bound as it starts**, by the project's start script, with no extra
  step. An open host is bound by the project's bind command, which the profile names as a step of `live.loop`
  (`<live.loop> bind`). Skills name no command of their own. Binding a host another worktree holds is refused
  unless forced, and forcing is the user's call.

## Tests run in the worktree's host

A test suite that runs inside the host is a caller like any other, and picks its host in the same order.
The host pack ships the decision as a pure function; the project feeds it every running host of the
version under test with its owner (this worktree, free, or another worktree's name, from the binding
record) and says whether its test runner can pin one process.

- **A runner that pins a process** runs in this worktree's host even while another host of the same version
  is open. A free host is bound first, then pinned; no host of that version: one is started for this
  worktree (the user approves the start), bound, then pinned.
- **A runner that cannot pin** attaches to whichever host of that version it finds, so it runs only when this
  worktree's host is the only one of that version. Otherwise nothing runs, and the message names **every**
  host of that version: its process number and the worktree holding it, or "free". Two free hosts, or two
  bound to this worktree, are refused the same way, pinned or not.
- **A started host is ready** when an announce file carrying **its** process number appears, written at or
  after that process started. A window title, or an announce file of an older process with the same number,
  says nothing about which build is loaded.

## Switching a server on

A host whose server is off has no announce file yet, so it is found by its process and its binding record.
**A switch-on request names the process it is for**, and an add-in ignores a request that names another
process. A request file that every add-in of that version watches, with no target, switches on the server
of every such host, including one that another worktree holds.

## What is not isolated until the project makes it so

The order above isolates **calls**. A **publish** is isolated only when the add-in's reload mechanism reads
the binding record and loads the build of its own worktree. Until the project has that, a publish still
reaches every host of that version on the machine. Say so in the evidence row, and do not publish while
another worktree's host of the same version is running a measurement.

## Where the rule has to live

- **In the MCP bridge** the agent's client starts. It finds the worktree from the session's folder by
  walking up to the folder holding `.git` (a folder in the main checkout, a file in a worktree). Outside any
  worktree it behaves as a worktree with no host of its own.
- **In every project script that talks to a host**: the reload after a build, the live loop's `ensure`, and
  the bind command. Use one shared module, and hold the bridge's copy to the same table of test cases.
- **Tests never write a stand-in host into the real announce folder**, because another session's bridge
  would call it. Let an environment variable move the announce and binding folders, and test there.

A bridge that the whole machine starts from one checkout's build picks up a change to this rule only after
that build is refreshed. Say so when you change it.
