---
description: 'Standardizes how Markdown documentation is authored, structured, and maintained under `./docs/` so it stays readable, navigable, and consistent over time.'
applyTo: 'docs/**/*.md'
---

# Documentation authoring rules (docs)

## Overview

These instructions define how to create and maintain documentation under `./docs/`.
They are for contributors who add, update, move, or rename documentation.

## Scope

Applies to: `docs/**/*.md`

- These rules cover document structure, Markdown conventions, linking, and maintenance expectations.
- When multiple instruction files apply, prefer the more specific scope.

## Instructions

### MUST

- Create documentation under `./docs/` as Markdown files with the `.md` extension.
- Use semantic Markdown structure with ATX headings (`#` style).
- Include one (and only one) H1 (`#`) heading per file.
- Make the H1 the first content in the file.
- Use heading levels in order (do not skip levels, for example, from H2 to H4).
- Use fenced code blocks for code samples and specify the language (for example, `csharp`, `json`, `xml`, `bash`, `powershell`).
- Use descriptive link text that explains the target (use the page title or meaningful nouns/verbs).
- Use `https` links where the target supports it.
- When linking to `learn.microsoft.com`, omit locale segments (for example, do not include `/en-us/` in the URL).
- When linking to other repo files, prefer relative links and use forward slashes (`/`) in link paths.
- Provide meaningful alt text for images using Markdown image syntax (`![alt text](path)`).
- When you move or rename documentation files, update inbound and outbound links so navigation remains correct.

### SHOULD

- Use a short introduction paragraph immediately after the H1 to explain purpose and audience.
- Use H2 (`##`) headings to organize major sections so readers can scan and navigate quickly.
- Prefer numbered lists for sequential procedures and bullet lists for non-sequential items.
- Keep paragraphs concise and focused on one idea.
- Keep line lengths reasonable to make diffs readable (reflow prose when practical).
- Prefer file names that are lowercase kebab-case (digits allowed) to keep paths predictable.

### MUST NOT

- MUST NOT create documentation under `./docs/` in non-Markdown formats (for example, `.docx`, `.pdf`).
- MUST NOT use multiple H1 headings in a single document.
- MUST NOT use HTML heading tags (for example, `<h1>`, `<h2>`) as a substitute for Markdown headings.
- MUST NOT apply inline formatting (bold/italic/inline code) inside headings.
- MUST NOT apply inline formatting (bold/italic/inline code) to link text.
- MUST NOT use vague link text like "click here".
- MUST NOT leave broken links after documentation changes.

## Output and Validation (optional)

- Expected artifacts: Markdown files under `./docs/` that use a single H1 and consistent heading hierarchy.
- Validate success by checking that:
  - any new/changed files are under `./docs/` and end in `.md`
  - each file has exactly one H1 at the top
  - internal links resolve after moves/renames

## References (optional)

- https://learn.microsoft.com/contribute/content/markdown-reference
- https://learn.microsoft.com/contribute/content/how-to-write-links
- https://learn.microsoft.com/contribute/content/text-formatting-guidelines
