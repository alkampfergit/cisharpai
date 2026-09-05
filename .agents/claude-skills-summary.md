# Riepilogo delle skill Claude

Analisi del 5 settembre 2026, effettuata da un subagent in sola lettura:
23 skill in `.claude/skills`, senza eseguirne i workflow.

| Skill | Scopo |
|---|---|
| `speckit-constitution` | Definisce principi del progetto e allinea i template. |
| `speckit-specify` | Crea specifiche con requisiti e criteri di successo. |
| `speckit-clarify` | Risolve ambiguità con domande mirate e aggiorna la specifica. |
| `speckit-plan` | Produce piano tecnico, ricerca, modello dati e contratti. |
| `speckit-tasks` | Genera attività ordinate per dipendenze e user story. |
| `speckit-checklist` | Verifica qualità e completezza dei requisiti. |
| `speckit-analyze` | Analizza coerenza tra specifica, piano e attività senza modificarli. |
| `speckit-implement` | Esegue il piano e aggiorna l'avanzamento delle attività. |
| `speckit-retrospec` | Ricostruisce specifiche e documentazione dal codice esistente. |
| `speckit-taskstoissues` | Converte attività in issue GitHub. |
| `speckit-git-initialize` | Inizializza repository e commit iniziale quando necessari. |
| `speckit-git-feature` | Crea branch; supporta nomi espliciti tramite GIT_BRANCH_NAME. |
| `speckit-git-validate` | Verifica nome branch e directory della specifica. |
| `speckit-git-commit` | Gestisce commit sugli eventi Spec Kit abilitati. |
| `speckit-git-remote` | Identifica remote, proprietario e repository. |
| `speckit-gh` | Gestisce una issue fino a implementazione, PR, CI e revisione. |
| `speckit-full` | Cerca issue con etichetta e le affida a speckit-gh. |
| `gh-cli-guide` | Riferimento per CLI GitHub: issue, PR, revisioni e workflow. |
| `gh-actions-debug` | Diagnostica e corregge errori dei workflow GitHub Actions. |
| `github-pr-fixer` | Corregge CI e commenti di revisione, fino a cinque cicli. |
| `sonarcloud` | Analizza quality gate, problemi e duplicazioni, conserva memoria. |
| `sonarcloud-pr-fix` | Ciclo di analisi SonarCloud, correzione, verifica e push. |
| `gitflow` | Gestisce release, versioni, merge, tag e push Git Flow. |

## Compatibilità e dipendenze

- I symlink alle cartelle preservano `references/` e `sonarcloud/memory/`.
- Mancano `.specify/templates/pr-report-template.md`, richiesto da `speckit-gh`,
  e la skill `fixer` richiamata dagli orchestratori.
- `/loop`, `ScheduleWakeup`, `CronCreate` e `Agent` sono riferimenti a capacità
  Claude: vanno tradotti nelle capacità effettivamente disponibili nel runtime.
- `speckit-plan` aggiorna esplicitamente `CLAUDE.md`; il contesto Codex usa anche
  `AGENTS.md` e va mantenuto coerente quando si applica quel workflow.
- Il branch richiesto `rag101` non supera `speckit-git-validate`, che prevede
  nomi numerati o con timestamp. Il nome è stato mantenuto come richiesto.
- `sonarcloud-pr-fix` contiene default di un altro progetto (`alkampfergit_azdo-cli`),
  Beads/Dolt e verifiche TypeScript; `gh-actions-debug` contiene un esempio Vitest.
  Occorre verificare e adattare questi riferimenti prima dell'uso sul progetto .NET.
- I workflow dipendono, secondo i casi, da `gh` autenticato, GitHub MCP,
  accesso alle API SonarCloud e `git flow` AVH. I link non installano queste dipendenze.
- `github-pr-fixer` richiede invocazione esplicita. Gli orchestratori prevedono
  approvazioni tramite issue/PR; collegare le skill non avvia quei workflow.

Per un eventuale lavoro RAG: `speckit-specify` → `speckit-plan` → `speckit-tasks`
→ `speckit-analyze` → `speckit-implement`, usando `speckit-clarify` per le decisioni
ancora aperte. Questa analisi non avvia l'implementazione RAG.
