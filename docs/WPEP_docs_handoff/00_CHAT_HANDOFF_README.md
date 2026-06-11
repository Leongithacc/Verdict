# 00 — LEGGIMI PRIMA · Pacchetto handoff dalla chat (2026-06-11)
*Lavoro di design/ricerca fatto da Léon + Claude in chat (claude.ai), da integrare
nel repo WPEP. Copiare tutti i .md in `docs/` e il mockup in `docs/mockup/`.*

## Prompt di apertura per Claude Code (incollare così)

> Nella cartella docs/ trovi 8 nuovi documenti dalla sessione di design in chat
> (indice: 00_CHAT_HANDOFF_README.md). Leggili nell'ordine indicato lì. Contengono
> DECISIONI PRESE: eseguire, non ridiscutere — salvo problema tecnico reale, da
> documentare come "domanda aperta". Ordine di lavoro per R7:
> 1. Noise gate in compare/bench (HANDOFF_R7 §1)
> 2. Comando validate: A/A + known-effect (HANDOFF_R7 §2)
> 3. Validazione ambiente tra run: blocco verdetto se display/driver cambiati
>    (EDGE_CASES F10)
> 4. UI WPF verdict-first (HANDOFF_R7 §4 + DESIGN_DIRECTION + R7_COPY_AND_KB3 §1
>    + mockup HTML come riferimento di layout, NON di qualità esecutiva)
> 5. Refactor portable + failure modes (PORTABILITY + EDGE_CASES)
> 6. Predisposizione V2: campo `apply` nullable nello schema KB (EXECUTION_ENGINE_V2 §3-4)
> Vincoli invariati: V1 read-only, statistica obbligatoria, niente claim input
> latency, niente overlay in-game (mai). Per ogni blocco: build verde, test,
> report breve (decisioni/assunzioni/rischi/alternative/domande).

## Ordine di lettura

| # | File | Contenuto | Stato |
|---|---|---|---|
| 1 | `HANDOFF_R7.md` | Noise gate, comando validate, protocollo benchmark, spec UI base | decisioni |
| 2 | `SCENARIO_VALIDITY.md` | Replay ≠ live (fonte UE); matrice scenario→categoria tweak; CORREGGE HANDOFF §3 | ricerca+decisioni |
| 3 | `R7_COPY_AND_KB3.md` | UI copy EN definitiva; confound shader warm-up; 9 candidati KB Fortnite | decisioni+ricerca |
| 4 | `DESIGN_DIRECTION.md` | Estetica "premium gaming × devtool", token theming, NO doppio layout | decisioni |
| 5 | `EDGE_CASES_AND_TRUST.md` | First-run, 10 failure modes (F10!), posizione anti-cheat, NO overlay | decisioni |
| 6 | `PORTABILITY.md` | Leave-no-trace portable, probe low-end/laptop, managed device notice | decisioni |
| 7 | `EXECUTION_ENGINE_V2.md` | Destinazione finale (applica tweak): gate, principi, predisposizioni V1 | visione+vincoli |
| 8 | `mockup/WPEP_UI_Mockup.html` | Riferimento visivo navigabile (layout/gerarchia) | riferimento |
| — | `WPEP_BuildSpec_V1.md` | Spec originaria (già nota, inclusa per completezza) | storico |

## Decisioni chiave prese in chat (riassunto per umani)

1. **Noise gate**: verdetto a 3 stati; mai verdetti da scenari troppo rumorosi.
2. **validate**: la pipeline si autocertifica (A/A + known-effect) prima di giudicare tweak.
3. **Scenari**: CP2077 benchmark (install pulita) = certificazione; Fortnite = mappa
   creativa route fissa (i replay NON valgono per tweak rete/input — fonte UE docs).
4. **Lingua UI: inglese** (destinazione GitHub pubblico). Copy completa già scritta.
5. **Design**: premium gaming × devtool; token theming dal giorno 1 (4-5 preset,
   Violet default); colori semantici NON personalizzabili; UN layout (no toggle densità).
6. **Anti-cheat**: WPEP è passivo (ETW, zero injection) → niente overlay, MAI.
7. **Portable leave-no-trace**: un exe, una cartella, zero servizi/registry;
   PresentMon console app (mai la variante Service). Delete folder = mai esistito.
8. **Low-end**: probe throttling termico, background load, batteria vs AC (bench
   su batteria = invalido), storage health. Advisor ripesato su hardware debole.
9. **Managed device notice**: su macchine aziendali, banner "chiedi all'IT".
   (Per Léon: il test sul laptop Schindler si fa SOLO dopo l'ok del formatore.)
10. **Destinazione confermata**: il prodotto finito APPLICA i tweak (V2), con
    dry-run, undo-journal, verify-after-write, ri-misura. Mai "ottimizza tutto".
    V1 predispone lo schema. Gate: pipeline certificata + 2 macchine + open source.

## Da fare a casa (Léon, non Claude Code)
- Chiedere ok al formatore per il test sul laptop Schindler (prima di toccarlo).
- Certificazione pipeline su CP2077 (protocollo in HANDOFF_R7 §3, install PULITA).
- Costruire la route creativa Fortnite e misurarne il noise floor.
- Decidere il nome pubblico prima del repo GitHub (WPEP è tecnico; pensaci).
