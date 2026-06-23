# Verdict — Roadmap a versioni (finire l'app, non solo "V1 solida")

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

## 🎯 V4 — "Intelligenza per-gioco"
**Ottimizza per [gioco]** completo: tweak di sistema + impostazioni in-game/NVIDIA ottimali per QUEL
titolo (Valorant, CS2, Apex...). **Network Duel** verso i server dei tuoi giochi (ping/jitter/
bufferbloat). Detection giochi più profonda. *Posso costruirlo io (research per-titolo).*

## 🛡 V5 — "Automazione & fiducia" (l'app ti guarda le spalle)
**Watchdog** (icona tray): ti avvisa se l'EXPO si spegne, un tweak salta, le temp spikano. **Regression
Sentinel**: ri-benchmarka da solo e avvisa se le prestazioni PEGGIORANO (es. dopo un Windows Update).
**Time Machine**: timeline "cos'è cambiato" + rewind. *Posso costruirlo io (serve un tray host).*

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
