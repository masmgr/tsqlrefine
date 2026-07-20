# T-SQL QA corpus

Every `.sql` file under `sql/` must have a corresponding entry in `manifest.json`.
The QA tests verify the file checksum, run all built-in rules, exercise formatting and
fixing, and parse the input across supported compatibility levels.

Only add SQL that can legally be redistributed. Third-party additions must include
the upstream project, author, source URL, immutable revision, license, modification
status, SHA-256 checksum, and minimum compatibility level in the manifest. Include
all license, copyright, NOTICE, and modification notices required by that project.

`project/` contains original test fixtures written for tsqlrefine and is covered by
the repository's MIT license.
