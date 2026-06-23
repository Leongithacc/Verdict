# Verdict — Piano d'attacco POST-DESIGN (cosa manca per essere "prodotto élite")

> Scritto 2026-06-23 mentre Claude Design fa il pass V2. Obiettivo: essere pronti ad attaccare
> appena il design rientra. Tesi netta, niente yes-man.

## 0. La tesi (cosa significa davvero "manca")
Le feature ci sono quasi tutte. Il vero gap tra Verdict e un **prodotto élite** NON è "più feature":
è **FIDUCIA + DISTRIBUZIONE**. Un tool che SCRIVE nel sistema e dice "fidati, è onesto" vale solo se
(a) ogni scrittura è provata e reversibile sul campo, e (b) un umano normale riesce a installarlo,
aprirlo la prima volta e capirlo senza che Windows urli "app sospetta". Tutto il resto (AI, community)
è ciliegina. Quindi si attacca nell'ordine: **Trust → Release/Onboarding → profondità → AI → community.**

## 1. Mappa dello stato
- **V1** Fondazione ✅ · **V2** Design 🔄 (Claude Design ORA) · **V3** Misura ✅ (motore già eccellente)
- **V4** Per-gioco ✅ · **V5** Watchdog tray ✅ (+coda) · **V6** AI ⏳ (serve decisione LLM)
- **V7** Community ⏳ (serve server) · **V8** Release ⏳ (il pezzo grosso mancante)

## 2. Regola anti-collisione col design
Claude Design tocca: `Themes/Theme.xaml`, `MainWindow.xaml`, forse converter e stringhe nei ViewModel.
**Finché lavora, io sto LONTANO da quei file.** Lavoro conflict-free = backend/engine/KB/CLI/tray/docs.
Il lavoro che tocca la GUI (onboarding, help, UX misura) si fa DOPO il merge del design.

---

## TIER 0 — TRUST & VERITÀ (la spina dorsale della credibilità) — PRIORITÀ MASSIMA
Per un tool "anti-placebo/onesto" questo è IL fossato difensivo. Conflict-free col design.
1. **Field-validation di OGNI write-path** sulla macchina reale (serve Léon admin):
   - registry ✅ (provato), powercfg ✅, **nvidia-drs ✅** (validato), dxuser ⚠️, **bcdedit WRITE ❌ MAI
     provato dal vivo** (audit #29: pattern identico, coperto da fake, ma zero conferma reale).
   → Costruisco io un **comando/checklist di self-test esteso** (`wpep selftest --writes`) che esercita
     ogni metodo su una chiave/valore usa-e-getta con write→verify→undo, così la conferma è ripetibile.
2. **Smaltire il backlog "DA RIVEDERE"** dell'audit log: deep-link URIs (mmsys.cpl/control.exe…),
   GUID subgroup powercfg, **app id Steam dei titoli nuovi** (TheFinals 2073850 / R6 359550 — verificare),
   semantica undo drift-aware su powercfg/bcdedit.
3. **Restore-point + undo-all + panic**: provare dal vivo che recuperano davvero (non solo unit test).
4. **Audit KB finale**: ogni voce ha fonte verificata, evidence onesto, gate hardware corretto. Posso
   passare in rassegna le 120 voci e segnare quelle con fonte debole/da ricontrollare.

## TIER 1 — RELEASE ENGINEERING (da tool a prodotto) — IL GAP PIÙ GROSSO
Questo è ciò che manca davvero per "élite". Misto: backend ora, GUI dopo-design.
1. **Onboarding / primo avvio** (la hero flow): welcome → scan automatico → "ecco cosa ho trovato sul
   TUO PC" → un click. Oggi è un'app a navigazione; un umano nuovo non sa da dove iniziare.
   *(tocca GUI → DOPO il design; ma la LOGICA del first-run la preparo ora.)*
2. **Installer**: MSIX o setup firmato + shortcut + uninstall pulito. Oggi è una cartella `artifacts/app`.
   *(scaffolding ora; conflict-free.)*
3. **Code signing**: certificato così SmartScreen non marchia "editore sconosciuto" — CRITICO per un tool
   di tweaking che già "sembra sospetto". *(serve DECISIONE Léon: certificato = soldi/identità.)*
4. **Auto-update**: check versione + download firmato. *(scaffolding ora.)*
5. **Resilienza/crash**: handler globale eccezioni, errore amichevole, il journal non si perde MAI.
   *(App.xaml.cs lo tocca il design per i temi → coordino DOPO, oppure in un file separato.)*
6. **Help/"perché fidarsi"** in-app: flusso che mostra evidenza+reversibilità. *(GUI → dopo-design.)*

## TIER 2 — PROFONDITÀ su ciò che esiste (guardiano + loop misura)
1. **Coda V5** (conflict-free, backend/tray): Regression Sentinel come 2° check del tray (avvisa se gli
   FPS PEGGIORANO, non solo le derive); intervallo poll configurabile. *(Posso farla ORA.)*
2. **Time Machine "rewind" in GUI** (engine `wpep timeline` c'è già). *(tocca GUI → dopo-design.)*
3. **Loop "provalo" (V3) a prova di umano**: rendere il before/after UN CLICK su un gioco scelto, con
   verdetto chiaro. Il motore è oro; la UX di lanciare un benchmark dev'essere banale. *(GUI → dopo.)*

## TIER 3 — AI CO-PILOT (V6) — differenziatore, SERVE DECISIONE
Linguaggio naturale: "rendi Valorant più fluido" → Verdict spiega e propone (mai applica da solo).
- **DECISIONE LÉON**: cervello LLM →
  - **A) API Claude (cloud)**: qualità top, serve API key (costo/uso), dato esce dal PC.
  - **B) Modello locale (il tuo qwen2.5vl/Ollama)**: gratis e privato, serve Ollama attivo, qualità minore.
- Scope V6.0 = **advisor sola-lettura** (spiega + consiglia), zero auto-apply. Posso architettarlo
  dietro un'interfaccia così il backend (A o B) è intercambiabile.

## TIER 4 — COMMUNITY EVIDENCE (V7) — aspirazionale, SERVE SERVER
"Ha aiutato il 73% dei rig simili." Dati anonimi aggregati. Serve backend/infra → versione tarda.
Decisione + costo hosting. Non ora.

## CROSS-CUTTING (debito trasversale)
- **Test del layer App**: oggi la logica GUI (ViewModel) è poco testata. Estrarre la logica pura e testarla.
- **Accessibilità/DPI/tastiera**, performance d'avvio, localizzazione 100% IT (il design fa le stringhe).

---

## 3. COSA ATTACCO ORA (mentre il design lavora) — conflict-free
In ordine di valore, tutto fuori dai file del design:
1. **`wpep selftest --writes`** — esercita ogni write-path (registry/powercfg/bcdedit/dxuser/nvidia-drs)
   con write→verify→undo su target usa-e-getta. Chiude il buco "bcdedit mai provato". (Tier 0.1)
2. **Coda V5 backend**: Sentinel nel tray + intervallo configurabile. (Tier 2.1)
3. **Audit KB delle 120 voci**: fonti/gate/onestà, lista delle deboli. (Tier 0.4)
4. **Scaffolding release**: bozza installer (MSIX) + auto-update check, isolati in file nuovi. (Tier 1.2/1.4)
5. **Logica first-run** (solo backend/stato, la UI dopo). (Tier 1.1)

## 4. DECISIONI che servono a Léon (prepariamo le risposte)
- **Firma/installer**: vuoi un certificato (SmartScreen pulito) o resti unsigned per uso personale?
- **V6 LLM**: API Claude (qualità) o qwen/Ollama locale (privacy/gratis)?
- **V7**: ti interessa davvero la community/server, o lo lasciamo come sogno?

## 5. Ordine consigliato dopo il merge del design
V2-merge → **Tier 0 (trust, col tuo field-test)** → **Tier 1 (onboarding + installer + signing)** →
Tier 2 (rifiniture) → V6 (AI) → V7 (se vuoi). Il prodotto "élite vendibile" nasce a fine Tier 1.
