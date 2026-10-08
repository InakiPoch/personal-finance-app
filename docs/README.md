# Documentation index

Feature-by-feature plans were consolidated here once shipped (they remain in git history). Read in this order:

| Doc | Use it for |
|---|---|
| [`GLOSSARY.md`](GLOSSARY.md) | Domain terms and the UI vocabulary |
| [`BUSINESS-RULES.md`](BUSINESS-RULES.md) | Current behavior: endpoints, error→HTTP map, rules per area |
| [`adr/`](adr/) | Why things are the way they are (15 decisions) |
| [`STATUS.md`](STATUS.md) | What's shipped, open items, backlog |
| [`TIMELINE.md`](TIMELINE.md) | What shipped when (phases ↔ dates ↔ ADRs) |
| [`RELEASING.md`](RELEASING.md) | Cutting a release, CI, container machinery |
| [`agents/`](agents/) | Config for agent skills (issue tracker, labels, domain docs) |

Elsewhere: product scope and design per stack in `app/api/docs/{PRD,DESIGN}.md` and `app/client/docs/{PRD,DESIGN,SYSTEM}.md`; working guidance in `CLAUDE.md` files; end-user instructions in the root `README.md`.

Trace notes in the PRD/DESIGN files that cite `docs/<feature>/slice-*.md` point at the removed plans. Recover any of them with `git show 702a717:docs/<feature>/<file>` (last commit that has them).

**Maintenance**: new decision → add an ADR; behavior change → edit `BUSINESS-RULES.md` and the glossary; finished work → one row in `TIMELINE.md`, adjust `STATUS.md`. Don't add per-feature plan folders back; use issues for planning.
