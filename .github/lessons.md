# Repository lessons

- Add shared operating rules once in `./.github/instructions/` before patching multiple agents or prompts to follow them.
- Keep prompts as transitional support for existing workflows; prefer instructions, agents, and skills for new reusable behavior.
- Allow structured executive summaries in durable markdown reports, but avoid forcing broad summary sections in ordinary chat responses.
- Prefer file-and-line evidence in review outputs so follow-on work can be executed without re-discovery.
- Stop after three failed attempts on the same slice and re-plan rather than continuing with the same approach.
- Prefer repository scripts or tasks for repeated validation and maintenance workflows instead of institutionalizing the same ad hoc command sequences across multiple assets.
- Record explicit routine-or-complex tier intent in agent metadata or the central tier registry when only one approved model identifier is available.
- Clean Architecture adoption is defined by responsibility ownership and dependency direction, not by project names, project count, or canonical folders.