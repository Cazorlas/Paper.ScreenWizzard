# /qa security lane

You read the files of your list for vulnerabilities. A finding is a place where **a value an attacker
controls** reaches something that trusts it. INPUT names that value and where it comes in (a file name in
a dialog, a field of a request, a cell of an imported sheet, a setting a user edits); a place where you
cannot name one is a suspicion - write INPUT `-`. Report in the format of [findings](findings.md), KIND
`vulnerability`, LANE `security`; do not fix anything.

What to look for -> what the input is:

- A secret in code or config: a key, a token, a connection string, a password -> anyone who reads the
  repository or the shipped file.
- A command or script built from data: `Process.Start`, `cmd /c`, `Invoke-Expression`, `& $x`,
  `Start-Process` with a joined string -> the part of the string that came from outside.
- A path built from data: `Path.Combine` with a name from outside, a zip entry extracted where its name
  says, a `..` that climbs out -> the name.
- Untrusted data deserialised: `BinaryFormatter`, `TypeNameHandling` other than `None`, a type read from
  the data -> the file or message being read.
- XML read with DTDs or external entities on -> the XML document.
- SQL joined from strings -> the value joined in.
- Weak hashing or encryption used for security: MD5, SHA1, DES -> the data it protects or compares.
- A secret kept by encoding, not encryption - Base64, hex, XOR with a constant, or the key stored beside what it
  locks; a password stored plain or under a fast hash (SHA-256, salted or not) instead of a slow one (PBKDF2,
  bcrypt, Argon2) -> anyone who reads the file or the database.
- TLS certificate checks turned off -> whoever sits on the network path.
- An assembly or DLL loaded from a folder anyone can write -> a file dropped in that folder.
- A secret or personal data written to a log -> whoever reads the log.
- Web: `Html.Raw` of user data, a missing authorization check, no CSRF protection, an open redirect,
  CORS `*` -> the request field or the page that sends it.
- A credential that never runs out or can be sent twice: a token with no expiry, a JWT signed with a secret short
  enough to guess, a signed request with no timestamp and nonce, a login, licence-key or one-time-code endpoint
  with no rate limit -> whoever captured the request, or can guess fast enough.
- An application that calls a language model: data sent off the machine beyond what the SPEC lists -> the
  data that leaves.

When the static lane ran, your list may end with `Already reported by analyzers (do not repeat)`: those
rules are already in the report, do not report them again; look for what an analyzer cannot see.

Severity: `critical` when the input is reachable from outside the machine with no account; `major` when
it needs a local user or a crafted file; `minor` or `info` otherwise.
