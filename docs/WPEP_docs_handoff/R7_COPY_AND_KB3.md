# WPEP — R7 UI Copy (EN) + KB Round 3 Candidates
*Preparato in chat il 2026-06-11. Salvare nel repo come `docs/R7_COPY_AND_KB3.md`.
Terzo file del pacchetto handoff (con HANDOFF_R7.md e SCENARIO_VALIDITY.md).*

---

## Parte 1 — UI Copy (inglese, tono secco e onesto)

Decisione: UI in **inglese** (destinazione GitHub pubblico). Il wording è identità
di prodotto: non inventare sinonimi, usare questi testi.

### Naming interno delle classificazioni (Advisor → UI)
| Stato Advisor | Label UI | Colore |
|---|---|---|
| consigliato | **Worth doing** | verde |
| opzionale | **Maybe — judge for yourself** | blu |
| già attivo | **Already optimal** | verde spento |
| placebo | **Skip it — placebo** | grigio |
| rischioso | **Risky — read first** | rosso |
| non applicabile | **Not applicable to this PC** | grigio scuro |

### Home / Verdict screen
- Header: `Your system: {n}/{total} checks optimal`
- Sub: `Last scanned {time} · Read-only — WPEP never modifies your system`
- Card 1: `Worth doing ({n})` → sub: `Free improvements with real evidence`
- Card 2: `Already optimal ({n})` → sub: `Nothing to do here. That's good news.`
- Card 3: `Placebo avoided ({n})` → sub: `Popular tweaks that don't actually work`
- Empty state (tutto a posto): `Nothing left to optimize. Seriously. Go play.`

### Measure wizard (step labels)
1. `Pick your game & scenario` — helper: `Live matches are too noisy to measure
   anything. Use a repeatable scenario — we'll check it for you.`
2. `Baseline — {n} runs` — helper: `Don't change anything yet. Warm up first:
   shader caches and temperatures need a few minutes of play before run 1.`
3. `Apply ONE change` — helper: `One change at a time, or you won't know what did what.`
4. `Post — {n} runs`
5. `Verdict`

### Verdict copy (i tre stati del compare, §1 HANDOFF)
- Effetto: `Detected: {metric} improved by {x}% (CI {a}–{b}%). This one's real.`
- Nessun effetto: `No measurable effect on this system (detection threshold: {y}%).
  Roll it back unless you have another reason to keep it.`
- Scenario rumoroso: `No verdict. This scenario is too noisy to detect effects
  below {y}%. Switch to a repeatable scenario and try again.`
- Scenario invalido per categoria (da SCENARIO_VALIDITY): `No verdict. {category}
  tweaks can't be tested in a replay — replays skip networking and player input.
  Use a Creative-map route instead.`

### Diagnostics
- Pulito: `No DPC offender found. Your driver stack is healthy — stutter, if any,
  is coming from the game itself.`
- Colpevole: `{driver}: max DPC {x}µs during capture. This is worth investigating.`

### KB screen
- Filter labels: `Strong evidence / Plausible / Controversial / Placebo / Risky`
- Footer fisso: `Every entry cites a primary source. No source, no recommendation.`

### About / tagline
- `The only optimizer that tells you when to stop optimizing.`
- Sezione "What WPEP will never do": `Write to your system · Claim to measure
  end-to-end input latency · Show you an improvement that isn't statistically real`

---

## Parte 2 — Confound di misurazione nuovo (aggiorna protocollo)

**Shader compilation stutter (DX12/UE5):** documentato che le prime partite dopo
un cambio di driver/patch/rendering mode soffrono di stutter da compilazione shader.
Conseguenze operative:
1. Protocollo: PRIMA dei run di benchmark, sessione di warm-up (5-10 min nello
   scenario) per scaldare shader cache E temperatura. Vale per CP2077 e Fortnite.
2. Run 1 dopo qualsiasi cambio di rendering mode/driver va SCARTATO o marcato.
3. Il wizard Misura deve dirlo (già nel copy sopra, step 2).
4. Idea feature: `bench` potrebbe flaggare automaticamente un run anomalo rispetto
   agli altri dello stesso gruppo (outlier detection semplice, es. mediana del run
   oltre 2×IQR dal gruppo) e proporre di escluderlo — MAI escluderlo in silenzio.

---

## Parte 3 — KB Round 3: candidati per-gioco (Fortnite) con grading preliminare

Categoria nuova proposta: `game:fortnite`. Grading PRELIMINARE da verificare con
fonti primarie (Epic/NVIDIA) come da regola spec. Fonti trovate in ricerca 2026-06-11.

| id proposto | grading prelim. | note |
|---|---|---|
| `fn-rendering-performance-mode` | evidence_strong | Epic ufficiale 2026: Performance Mode ora gira su DX12 e disabilita gran parte del rendering; fonti terze riportano +20-50% FPS vs DX12 pieno. Su un 5080 il guadagno FPS è meno rilevante della consistenza: DA MISURARE col tool (ottimo primo test vero!). |
| `fn-reflex-on-boost` | evidence_strong | consenso totale per competitive; NVIDIA è fonte primaria. |
| `fn-reflex-vs-ullm-no-stack` | evidence_strong | Reflex On+Boost e ULLM driver-level NON si sommano; mischiare i due può causare frame pacing incoerente su certi driver. Voce "configura bene, non doppione". |
| `fn-frame-cap-vs-unlimited` | **controversial (caso da manuale)** | CONFLITTO documentato: guida ufficiale Epic 2026 dice "Unlimited", la prassi latency (e NVIDIA per G-SYNC) dice cap sotto il refresh. Con G-SYNC+Reflex il cap è gestito. Voce perfetta per mostrare come WPEP tratta fonti in disaccordo. |
| `fn-vsync-off` | evidence_strong | Epic stessa: V-Sync off per input lag, tearing gestito da G-SYNC/FreeSync. |
| `fn-nanite-lumen-off` | evidence_strong (per competitive) | costi documentati pesanti (GI/ombre virtuali/Nanite); per competitive si spengono. Trade-off visivo esplicito. |
| `fn-multithreaded-rendering` | plausible | consigliato con 6+ core; verificare fonte primaria Epic. |
| `fn-tsr-vs-dlss` | plausible | su RTX: DLSS preferibile a TSR per costo/qualità; dipende dal target. |
| `fn-shader-warmup-before-judging` | evidence_strong | non è un tweak ma una nota metodologica: dopo patch/driver nuovo, le prime 2-3 partite stutterano per shader compilation. Non giudicare (né misurare) in quella finestra. |

Nota di coerenza: queste voci sono quasi tutte "configure well what you have" —
in linea col bilancio KB esistente (l'evidenza forte sta nella configurazione,
non nei registry hack). Rafforza la tesi del tool.

### Per Léon specificamente (9800X3D + 5080 + 240Hz, già G-SYNC/Reflex da verificare)
Primo esperimento consigliato col tool certificato: **Performance Mode vs DX12+DLSS**
sulla route creativa fissa — è il confronto con l'effetto atteso più grande e quindi
il più facile da misurare sopra il noise floor. Domanda interessante e non ovvia:
su hardware top, Performance Mode migliora la *consistenza* (0.2% low) o solo l'FPS
medio che già avanza? Nessuna fonte lo misura seriamente su un 5080. Sarebbe il primo
risultato originale di WPEP.

## Domande aperte
- Schema KB: aggiungere campo `game` (null = system-wide) — ok?
- Le voci per-gioco entrano nel conteggio del Verdict header o vista separata per gioco?
  (Proposta: sezione separata "Game-specific", attiva solo se il gioco è installato.)
