# WPEP — First Run, Failure Modes & Anti-Cheat Position
*Deciso in chat il 2026-06-11. Salvare nel repo come `docs/EDGE_CASES_AND_TRUST.md`.
Quinto file del pacchetto handoff. La qualità di un'app pubblica si vede quando le
cose vanno male: ogni failure mode qui elencato DEVE avere la gestione descritta.*

## 1. Posizione anti-cheat (decisione + testo ufficiale)

**Fatti architetturali:** WPEP non inietta codice, non hooka il gioco, non legge la
memoria del gioco, non disegna overlay. La cattura frametime legge eventi ETW del
kernel (canale passivo di Windows). EAC/BattlEye cercano injection, hook e manipolazione
della memoria di gioco — categorie a cui WPEP non appartiene per costruzione.

**DECISIONE DI SCOPE: nessun overlay in-game, mai in V1.** Un overlay richiede
injection (stile RTSS) ed è l'unica feature che sposterebbe WPEP in zona grigia
anti-cheat. Chi vuole l'overlay usa PresentMon ufficiale. WPEP resta fuori dal
processo del gioco per principio. Questo è anche un punto di marketing: scriverlo.

**Testo per README e schermata About (EN):**
> WPEP never touches your game. No code injection, no process hooks, no game memory
> access, no overlay. Frame data comes from Windows' own event tracing (ETW) — the
> same passive channel used by Intel PresentMon. We cannot offer formal guarantees
> on behalf of anti-cheat vendors, but WPEP belongs to no category anti-cheat
> systems target.

Onestà: NON scrivere "100% ban-safe" (garanzia che nessuno può dare). L'argomento
è l'architettura, non la promessa.

## 2. First-run experience (flusso deciso)

Primo avvio = momento della fiducia. Flusso in 3 schermate, poi subito il Verdict:

1. **Welcome** — una frase: "WPEP measures, diagnoses and recommends. It never
   modifies your system." + tagline. Bottone: `Scan my system`.
2. **PresentMon setup** (solo se assente) — "WPEP uses Intel PresentMon (open
   source, MIT) to capture frame data. Download now? (~10 MB, pinned version 2.4.1,
   SHA256 verified)" → consenso esplicito, MAI download silenzioso. Se l'utente
   rifiuta: l'app funziona lo stesso, sezione Measure mostra empty state con il
   bottone di download.
3. **Primo scan** — analyze+advise (no admin) parte subito, con progress reale
   (probe per probe, non barra finta). → Verdict screen.

Niente account, niente telemetria, niente EULA infinita. La elevazione admin NON
viene chiesta al primo avvio: solo quando l'utente avvia Diagnostics (on-demand,
con spiegazione PRIMA del prompt UAC: "Kernel tracing requires administrator.
Windows will ask for confirmation.").

## 3. Catalogo failure modes (ognuno con gestione obbligatoria)

Formato: condizione → comportamento richiesto. Copy in EN, tono: spiegare cosa è
successo e cosa fare, senza scuse vaghe (gli errori non si scusano, indirizzano).

**F1 · PresentMon assente/corrotto** → Measure mostra empty state con download
button. Hash mismatch dopo download → "Downloaded file failed verification.
Try again or download manually from GitHub (link)." MAI eseguire un binario non verificato.

**F2 · Elevazione negata (UAC cancel)** → niente errore drammatico: "Diagnostics
needs administrator to read kernel events. Everything else works without it."
Resto dell'app pienamente funzionante (degradazione elegante, già da spec).

**F3 · Processo gioco non trovato durante bench** → "No process named {name} found.
Is the game running?" + lista processi attivi con rendering per selezione rapida.

**F4 · Gioco chiuso a metà cattura** → run marcato INVALID, non conteggiato negli
N run, dato parziale scartato. "Run 3 aborted — the game closed. 2 of 5 valid runs."

**F5 · Run outlier** (es. shader compilation, alt-tab) → flag automatico (mediana
oltre 2×IQR dal gruppo) + proposta di esclusione. MAI esclusione silenziosa: l'utente
decide, il report dichiara cosa è stato escluso e perché.

**F6 · Sessione ETW già attiva / conflitto** (altro tool di tracing aperto) →
messaggio chiaro: "Another kernel trace session is running (often: LatencyMon,
WPR, or another capture tool). Close it and retry."

**F7 · Antivirus/SmartScreen blocca l'exe** → sezione dedicata nel README + pagina
"Why does Windows warn about WPEP?" (niente firma OV per scelta; open source +
hash pubblicati = la risposta). Aspettativa onesta: il warning al primo avvio CI SARÀ.

**F8 · Snapshot probe fallita** (WMI capriccioso, sensore non leggibile) → il probe
riporta "unknown", il Verdict conta solo i probe riusciti ("24/27 checks — 1 probe
unavailable"), MAI inventare valori, MAI crashare per un probe.

**F9 · Dati corrotti/parziali nelle cartelle run** → compare rifiuta con messaggio
specifico ("run_3.json is incomplete — re-run or remove it"), non verdetti su dati rotti.

**F10 · Risoluzione/refresh cambiati tra baseline e post** → rilevato dallo snapshot
allegato a ogni run → verdetto BLOCCATO: "Display mode changed between baseline and
post (1440p@240 → 4K@120). Comparison invalid." Stessa logica per cambio driver GPU.

Il principio unificante: **mai un verdetto su dati invalidi, mai un fallimento
silenzioso, mai un crash per una condizione prevedibile.** In un tool che vende
onestà metrologica, la gestione errori È il prodotto tanto quanto la statistica.

## 4. Nota implementativa per Claude Code

F10 implica: ogni run salvato include un mini-snapshot (display mode, driver GPU,
power plan) e compare lo valida prima di calcolare qualsiasi cosa. Probabilmente
il singolo requisito nuovo più importante di questo documento — aggiunge integrità
al cuore statistico già costruito.
