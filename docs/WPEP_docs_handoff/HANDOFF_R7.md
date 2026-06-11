# WPEP — Handoff per Claude Code (post R6)
*Preparato in chat il 2026-06-11. Da salvare nel repo come `docs/HANDOFF_R7.md`.
Contiene decisioni già prese: eseguire, non ridiscutere (salvo problema tecnico reale,
nel qual caso documentare in §7 "Domande aperte").*

---

## 0. Prompt di apertura (incollare a Claude Code)

> Leggi `docs/HANDOFF_R7.md`. Contiene le decisioni prese in fase di design.
> Ordine di lavoro: (1) feature noise-gate in `compare`, (2) comando `validate`
> per A/A e known-effect test, (3) R7 UI verdict-first come da spec §4.
> Regole spec §1/§6 invariate. Per ogni blocco: build verde, test, report breve
> (decisioni/assunzioni/rischi/alternative/domande).

---

## 1. Problema da risolvere prima di tutto: il noise gate

**Fatto misurato:** noise floor Fortnite live = ±28% mediana, ±129% 0.2% low.
Con questo rumore `compare` è cieco: nessun tweak realistico supera quella soglia.
Rischio attuale: l'utente fa un compare su scenario rumoroso, ottiene "nessun effetto",
e crede che il tweak sia inutile quando in realtà è la MISURA a essere inutile.
Questa è una violazione del principio di onestà, in direzione opposta al solito.

**Feature richiesta — noise gate in `compare` e `bench`:**
- `compare` deve SEMPRE confrontare l'effetto rilevabile minimo (MDE, minimum detectable
  effect, derivato dalla varianza dei run baseline) con il rumore dello scenario.
- Output a tre stati, non due:
  1. "Effetto rilevato: +X% [CI]"
  2. "Nessun effetto misurabile (sopra la soglia di rilevabilità di Y%)"
  3. **"Scenario troppo rumoroso: questo setup non può rilevare effetti sotto il Y%.
     Verdetto non emesso. Usa uno scenario ripetibile."**
- Lo stato 3 è obbligatorio quando il MDE supera una soglia ragionevole (proposta: 10%
  sulla mediana; tarabile). Meglio nessun verdetto che un verdetto falso.
- `bench` a fine raccolta deve già stampare la varianza intra-run e avvisare se lo
  scenario è rumoroso, PRIMA che l'utente perda tempo col post.

---

## 2. Comando `validate` — la pipeline si autocertifica

Nuovo comando: `wpep validate --dir <cartella run>`. Due modalità:

**A/A test:** due gruppi di run raccolti SENZA cambiare nulla (es. 5+5 sul benchmark
integrato di CP2077). Atteso: "nessun effetto". Se il tool dichiara un effetto su un
A/A test, la pipeline ha un falso positivo → bug o run troppo pochi.

**Known-effect test:** baseline vs post con una modifica a effetto GARANTITO e grande
(cambio di impostazione grafica, es. DLSS Quality vs nativo, o shadows ultra vs low).
Atteso: effetto rilevato, direzione giusta. Se non lo rileva → pipeline rotta o
scenario inutilizzabile.

`validate` è anche il tutorial perfetto per l'utente finale: "prima di fidarti dei
verdetti, fai questo". Va citato nel report HTML e nella UI.

---

## 3. Protocollo benchmark deciso (per Léon, da eseguire a casa)

**Validazione pipeline → Cyberpunk 2077, benchmark integrato** (già posseduto):
1. Pannello NVIDIA/gioco in configurazione stabile, PC a regime termico (10 min di carico prima).
2. 5 run benchmark integrato → `--label aa-baseline-1`
3. Altri 5 run identici → `--label aa-baseline-2` → `wpep validate` (A/A)
4. 5 run con DLSS/preset cambiato → known-effect test
5. Solo se 3 e 4 passano: la pipeline è certificata.
NOTA: usare l'install PULITA di CP2077, non quella moddata con CET (le mod alterano
frametime e invalidano la misura).

**Test tweak su Fortnite → scenario ripetibile:**
- Opzione A: replay con percorso camera fisso, stesso replay per tutti i run.
- Opzione B: mappa creativa privata, route fissa percorsa a piedi, 60s.
- Prima cosa da fare: misurare il noise floor del NUOVO scenario (5 run identici).
  Se il MDE resta sopra ~10%, cambiare scenario. Solo dopo, testare tweak.
- Primo tweak da testare (il più promettente tra quelli pendenti su questo rig):
  GameDVR off — gratis, evidenza decente, rollback banale.

---

## 4. R7 — Spec UI WPF "verdict-first"

**Identità:** "l'unico optimizer che ti dice quando smettere di ottimizzare."
La UI è il verdetto, non una dashboard di grafici.

**Avvio:** l'app lancia `analyze` + `advise` in automatico (sono no-admin e veloci).
Prima schermata = Verdetto:
- Header grande: "24/28 controlli a posto" (numero reale dallo snapshot)
- Tre card sotto: **Azioni consigliate (N)** / **Già ottimale (N)** / **Placebo evitati (N)**
- La card placebo NON è nascosta: è la feature identitaria. Click → spiegazione + fonte.

**Navigazione (sidebar sinistra, 5 voci):**
1. **Verdetto** (home, sopra)
2. **Misura** — wizard guidato: scegli processo → baseline N run → "ora applica UNA
   modifica a mano" → post N run → verdetto compare con noise gate. Il wizard
   incorpora la metodologia: l'utente non può sbagliare l'ordine.
3. **Diagnostica** — `diag` con richiesta elevazione on-demand; classifica driver,
   verdetto tipo "nessun colpevole DPC" se pulito.
4. **Knowledge Base** — 51 voci filtrabili per evidenza/categoria; badge colorati
   (forte=verde, plausibile=blu, controverso=giallo, placebo=grigio, rischioso=rosso).
5. **Report** — genera/apri HTML.

**Stile:** dark theme, sfondo ~#0F0F14, superfici #1A1A22, accento #8B5CF6,
accento scuro #4A0080, testo alto contrasto. Tipografia: Segoe UI Variable.
Niente animazioni pesanti, niente skeuomorfismi. Il tono dei testi è secco e onesto
("non serve a niente" si può scrivere).

**Architettura:** WPF MVVM, la UI chiama gli stessi servizi della CLI (nessuna logica
duplicata). La CLI resta com'è: utenti avanzati e automazione.

**Packaging (dopo la UI):** publish self-contained win-x64 single-file. Firma binario:
rimandata — decisione presa: niente certificato OV (costo ingiustificato per tool
gratuito); la risposta alla fiducia è l'open source (§5). Pubblicare hash SHA256
delle release.

---

## 5. Open source — decisione presa, preparazione

- Licenza: **MIT** (coerente con PresentMon e l'ecosistema).
- Repo GitHub pubblico quando R7 è stabile. Da preparare già ora:
  - `LICENSE`, `README.md` (pitch verdict-first, screenshot, filosofia onestà,
    sezione "cosa NON facciamo": no scritture, no input latency, no miracoli),
  - `CONTRIBUTING.md` minimo: ogni voce KB nuova richiede fonte primaria.
- Controllo pre-pubblicazione: nessun path personale, nessun dato di Léon nei
  file committati (snapshot di esempio anonimizzati).

## 6. Esplicitamente fuori scope (non farlo)
- Execution engine / scritture sul sistema (resta V1 read-only).
- Telemetria/cloud.
- Auto-update.
- Supporto multi-lingua (solo IT o solo EN per ora — scegliere EN se si va pubblico).

## 7. Domande aperte (per Léon, non bloccanti)
- Lingua UI: italiano (uso personale) o inglese (GitHub pubblico)? Consiglio: EN.
- Soglia noise gate: 10% di default va bene o tarare dopo i primi dati CP2077?
- Nome pubblico: "WPEP" è tecnico; valutare un nome più memorabile prima del repo pubblico.
