# Verdict — Roadmap a versioni (finire l'app, non solo "V1 solida")

> **STATO 2026-07-01**: V1–V8 **tutte completate**. v1.0 è live su
> [GitHub Releases](https://github.com/Leongithacc/Verdict/releases). v1.1 è
> pronta al tag (build in attesa di SDK). Cronologia completa in
> [CHANGELOG.md](../CHANGELOG.md). Per la visione post-v1.1 vedi la sezione
> **Post-v1.1** in fondo a questo file.
>
> Il resto del documento è preservato come reference storica del piano
> originale (2026-06-22 → 2026-06-30). Le tick "✅ FATTO" sono ora attendibili
> per V1–V5, V6 e V7 sono in v1.1, V8 è in v1.0.

> Idea di Léon (2026-06-22): andiamo avanti V per V finché l'app è COMPLETA. Molte sessioni.
> Ogni versione è un traguardo coerente e rilasciabile. Ordine indicativo, si può rimescolare.

## ✅ V1 — Fondazione solida (FATTO)
Motore apply/undo/journal/verify (registry/powercfg/bcdedit/nvidia-drs/dxuser). 28 tweak one-click,
incluso il pannello NVIDIA via NVAPI. Scan ricco + rilevamento stato reale (batch NVAPI). Viste
action-first ("che FA"). Lab leggibile ("appare in › pagina"). Profili. Engine bonificato da review
multi-agente (16 finding). Apply asincrono, non blocca la UI.

## 🎨 V2 — "L'arma prende forma" (identità & design)
Il pass **Claude Design**: dalla v1 funzionale-piatta a élite. Esecuzione premium (gerarchia, gauge
cockpit, toggle soddisfacenti, micro-interazioni), via le glyph-emoji, **temi personalizzabili**
(villain default + altri). + **localizzazione italiana completa** (stringhe inglesi rimaste).
→ La palette villain c'è già; è l'ESECUZIONE. Brief pronto: `docs/CLAUDE_DESIGN_BRIEF.md`. *Serve Léon
(giudizio look) + Claude Design.*

## 📊 V3 — "Misura e dimostra" (l'anti-placebo reso reale)
Il cuore onesto di Verdict: PROVARE che un tweak funziona. Robustizzare PresentMon (benchmark FPS/
frametime), **Latency Lab** before/after, **Ghost Tweak** (A/B alla cieca su te stesso). Verdict misura,
non promette. *Posso costruirlo io (PresentMon già parzialmente integrato).*

## 🎯 V4 — "Intelligenza per-gioco" (FATTO il grosso — 2026-06-23)
**Ottimizza per [gioco]** completo: tweak di sistema + impostazioni in-game/NVIDIA ottimali per QUEL
titolo. **Network Duel** verso i server dei tuoi giochi (ping/jitter). Detection giochi più profonda.
- ✅ Impostazioni in-game arricchite e oneste: Valorant 6, CS2 6, Apex 5, Overwatch2 4, Fortnite 9.
- ✅ Due titoli NUOVI first-class (rilevamento Steam + KB): **THE FINALS** (4) e **Rainbow Six
  Siege** (4). Totale KB 120 voci.
- ✅ **Network Duel game-aware**: `wpep network <gioco>` testa baseline + l'anchor di rotta
  dell'ecosistema del titolo; guardia di accoppiamento (ogni slug-gioco KB ha un anchor).
- ⏳ Restano (a richiesta): bufferbloat sotto-carico (idle-vs-loaded), altri titoli (Marvel Rivals,
  CoD…), selettore gioco nel Network Duel della GUI (rimandabile a V2 design). *Posso costruirlo io.*

## 🛡 V5 — "Automazione & fiducia" (l'app ti guarda le spalle) — FATTO il grosso (2026-06-23)
**Watchdog** (icona tray): ti avvisa se l'EXPO si spegne, un tweak salta, bloat all'avvio. **Regression
Sentinel**: ri-benchmarka da solo e avvisa se le prestazioni PEGGIORANO. **Time Machine**: timeline
"cos'è cambiato" + rewind.
- ✅ **Tray host** `WPEP.Tray` (wpep-tray.exe): agente WinForms isolato, poll ogni 10 min, balloon SOLO
  sui nuovi alert (WatchdogMonitor anti-spam). Avviabile dalla GUI ("Avvia in background"). Read-only.
- ✅ Core condiviso testato: `WatchdogProbe` (unica raccolta CLI/GUI/tray) + `WatchAlert.Key` + monitor.
- ✅ **Avvio automatico con Windows** (opt-in, reversibile): `TrayAutostart` (HKCU Run, no admin) +
  checkbox in GUI "Avvia la sorveglianza all'avvio di Windows".
- ✅ **Sentinel nel tray**: `wpep sentinel` salva il verdetto (`SentinelStatusStore`); il tray ricorda
  una REGRESSIONE con un balloon (onesto: il tray non può benchmarkare da solo).
- ⏳ Restano: intervallo poll configurabile; "rewind" guidato in GUI per Time Machine (engine
  `wpep timeline` c'è già) → attende il merge del design. *Io.*

## 🤖 V6 — "AI co-pilot"
Linguaggio naturale: "rendi Valorant più fluido" → Verdict spiega e propone. *Serve un LLM: o API key,
o un modello locale (Léon ha già qwen2.5vl/Ollama). Da decidere insieme.*

## 🌐 V7 — "Evidence community"
Dati anonimi aggregati: "ha aiutato il 73% dei rig simili". Onestà crowd-validata. *Serve un backend/
server. Infra più grande — versione tarda.*

## 📦 V8 — "Release" (prodotto vero)
Packaging, installer, auto-update, firma, onboarding, performance pass finale. Da tool a prodotto
distribuibile.

---
### Note
- Le versioni "funzionali" (V3/V4/V5) le costruisco io tra le sessioni; il pass **design (V2)** e le
  versioni con dipendenze esterne (V6 LLM, V7 server) le facciamo con Léon / decisioni dedicate.
- Si può interleave: es. V2 design + V3 misura in parallelo, perché toccano parti diverse.
- Ogni V chiude con: build 0/0, suite verde, artifact ripubblicato.

---

## Post-v1.1 (aggiornamento 2026-07-01)

### v1.2 — target Q3 2026

Feature grosse candidate, non ancora ordinate:

- **Localizzazione EN completa**: GUI + CLI + site/ attualmente misti IT/EN.
  Target: parity IT ↔ EN.
- **Placebo Museum come endpoint community**: mostrare quali tweak "popolari
  sul web" la community ha misurato come no-effect. Anti-marketing puro.
- **Nuovi giochi in catalogo**: Rocket League, Marvel Rivals, Deadlock,
  Battlefield 6, EFT (con fonti primarie o publisher docs).
- **Report PDF export**: HTML report c'è; PDF via Playwright è next step.
- **Espansione SessionMode**: Slack, Zoom, Teams, WhatsApp Desktop.

### v2.0 — 2027 (visione, non impegno)

Cose che cambierebbero forma al prodotto:

- **Kernel-mode diagnostics (opt-in)**: DPC/ISR già passivo. Un driver
  signed per metriche più profonde è tecnicamente possibile — decisione
  UNSIGNED verrebbe ripensata. Solo se la value prop è netta.
- **Multi-monitor benchmark**: Ghost Tweak attualmente single-monitor.
- **Overlay dashboard read-only**: nessun overlay oggi. Un overlay
  ETW-driven read-only che mostra frametime durante il gioco è possibile
  ma alza il rischio anti-cheat. Sperimentazione isolata prima.

### Non farò (esplicito)

- **"Optimize everything" button**: viola la filosofia.
- **Subscription / Premium tier**: MIT, gratis, sempre.
- **Tweak senza fonte primaria**: neanche uno.
- **Marketing "+30% FPS garantiti"**: mai.

### Come influenzare la roadmap

- Apri una [feature request](../.github/ISSUE_TEMPLATE/feature_request.md)
  con almeno una fonte primaria per il tweak / feature.
- Vota con 👍 sulle issue esistenti.
- Contribuisci: PR con test verdi + fonte verificata sono la via più veloce.
