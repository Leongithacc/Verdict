# Privacy Policy — Verdict

> Ultimo aggiornamento: 2026-07-01.
>
> Questo documento descrive che dati Verdict tratta, quali dati escono dal tuo PC
> e in quali condizioni. È scritto in italiano semplice per essere leggibile; per
> gli aspetti legali GDPR vedi la sezione 7 in fondo.

## 1. Principio guida

**Verdict non spedisce nulla dal tuo PC senza il tuo consenso esplicito**. Ci sono
esattamente **tre** categorie di traffico di rete che Verdict può fare, e nessuna
di esse è attiva di default se non `update check` (che comunque richiede consenso
per scaricare) e le richieste al brain AI **che tu configuri**.

## 2. Cosa Verdict fa SEMPRE, senza rete

- **Scansione hardware**: WMI + registry + display API (`SystemAnalyzer`).
  Rimane tutto sul PC. Il risultato non lascia mai il disco tranne se
  esplicitamente esportato con `wpep report` o "Esporta report" nella GUI.
- **Benchmarking**: PresentMon ETW capture di frametime durante una sessione.
  I file `.csv` sono in una cartella `runs/` accanto all'exe. Puoi cancellarli
  quando vuoi.
- **Diagnostica DPC/ISR**: kernel ETW. Nessuna uscita di rete.
- **Ledger locale degli esiti**: ogni tweak applicato registra un
  `EvidenceRecord` in `%LOCALAPPDATA%\Verdict\data\evidence.json`. Struttura
  anonima (rig signature, tweak id, esito, delta misurato) ma comunque
  **sempre in locale** finché non attivi V7 community (vedi sez. 4).
- **Journal delle modifiche**: ogni apply crea file `changes-*.json` che
  descrivono cosa è stato scritto e come annullarlo. Restano sul PC. Puoi
  cancellarli — perdi solo la possibilità di undo su quelle modifiche.
- **AI co-pilot con Ollama**: se usi il brain di default, la richiesta va a
  `localhost:11434`. Zero rete esterna.

## 3. Cosa Verdict fa dietro il tuo consenso, con rete

### 3.1 Update check (default: OFF finché non premi il bottone)

- **Cosa manda**: HTTP GET verso `github.com/Leongithacc/Verdict/releases`.
- **Cosa contiene**: la richiesta. GitHub logga il tuo IP nel proprio access
  log come qualsiasi visita web.
- **Cosa Verdict fa col risultato**: mostra "nuova versione disponibile / sei
  aggiornato". **Non scarica mai** senza tuo click esplicito.
- **Come disattivarlo**: non premere il bottone "Controlla aggiornamenti".

### 3.2 AI co-pilot con brain cloud (default: OFF, Ollama è il default)

Quando cambi il brain in `Claude`, `Gemini` o `GPT` E inserisci una API key:

- **Cosa manda**: HTTPS POST verso:
  - Claude: `api.anthropic.com/v1/messages`
  - Gemini: `generativelanguage.googleapis.com/v1beta/models/.../generateContent`
  - GPT: `api.openai.com/v1/chat/completions`
- **Cosa contiene**: la tua domanda in linguaggio naturale + il catalogo Verdict
  compresso (id + nome + evidenza + rischio + stato attuale per ogni tweak).
  Nessun dato personale, nessun file, nessuna configurazione hardware oltre a
  quella già impressa nelle Recommendation.
- **API key**: cifrata a riposo con **DPAPI** (Windows Data Protection API)
  per l'utente Windows corrente. Se sposti `settings.json` su un altro utente/PC
  la chiave diventa illeggibile (comportamento voluto).
- **Chi vede la richiesta**: il vendor del brain (Anthropic / Google / OpenAI)
  secondo la sua privacy policy. **Verdict non tocca il traffico** — la
  richiesta va direttamente dal tuo PC al vendor.
- **Come disattivarlo**: torna al brain `Ollama` nella pagina Co-pilota.

### 3.3 V7 Community evidence (default: OFF)

Solo se attivi il flag "Condividi i miei esiti anonimi con la community Verdict"
nelle Impostazioni:

- **Endpoint**: `https://verdict-community.gz6jk62yk8.workers.dev` (Cloudflare
  Worker + D1). Codice sorgente pubblico:
  [Leongithacc/verdict-community](https://github.com/Leongithacc/verdict-community).
- **Cosa manda** (per ogni tweak applicato o esito misurato dal Ghost Tweak):
  - `rig_signature` — hash 8 caratteri Base32 derivato dalla config hardware
    (motherboard + CPU + cores + GPU + RAM + primo disco). Deterministico per
    lo stesso hardware, non reversibile all'inverso.
  - `rig_tier` — categoria del rig (MITICO / LEGGENDARIO / EPICO / RARO /
    COMUNE) derivata dall'euristica di RigDna.
  - `tweak_id` — id della voce KB.
  - `outcome` — `helped` / `no-effect` / `hurt` / `applied`.
  - `delta_percent` — la percentuale di miglioramento MISURATA dal Ghost
    Tweak (null se solo applicato, senza misura).
  - `captured_at_iso` — timestamp UTC dell'evento.
- **Cosa NON manda MAI**: username, IP salvato in DB, MAC, modello esatto
  motherboard/CPU/GPU, path file, cronologia precedente all'opt-in, contenuto
  del catalogo (già pubblico e versionato), altre applicazioni installate.

Cloudflare (il provider infrastruttura) può vedere il tuo IP a livello di
handshake HTTPS, come qualsiasi visita web. Il **Worker Verdict NON salva
l'IP** nel database D1 — vedi `src/index.ts` del Worker che non ha nessun
INSERT su una colonna IP.

- **Cosa ricevi in cambio**: quando visualizzi un tweak nella pagina Verdict,
  puoi vedere la percentuale aggregata "ha aiutato il X% dei rig simili". Solo
  quando ci sono almeno 10 sample per (tweak, tier), altrimenti la percentuale
  è nascosta (regola d'oro "niente FPS finti").
- **Quanto fidarti dei numeri**: gli esiti sono auto-riportati da client anonimi
  e non attestati — il server non può verificare che dietro una `rig_signature`
  ci sia hardware reale. Le percentuali community sono un indizio, non una
  prova: la prova sul TUO rig resta Measure.
- **Come disattivarlo**: togli il flag dalla pagina Impostazioni. Da CLI:
  `wpep community --disable`.

## 4. Retention

- **Ledger locale (`evidence.json`)**: mai cancellato automaticamente. Tu decidi.
- **Journal locale (`changes-*.json`)**: mai cancellato automaticamente. Tu decidi.
- **Server community**: **365 giorni rolling**. Job notturno alle 03:00 UTC:
  `DELETE FROM evidence WHERE received_at < datetime('now','-365 days')`.
  Cache aggregata `stats_cache`: ricostruita ogni notte, di fatto infinita ma
  derivata (nessuna PII).
  I dati community sono best-effort e **senza backup/DR** per la beta (sono anonimi
  e ricostruibili): stance accettata, dettagli in
  [V7_REMOTE_BACKEND_DESIGN.md §10.1](V7_REMOTE_BACKEND_DESIGN.md).

## 5. Come cancellare i tuoi dati

### 5.1 Dal tuo PC

Cancella la cartella `%LOCALAPPDATA%\Verdict\` (contiene evidence, journal,
settings, API key cifrate). Verdict è portable, quindi puoi anche cancellare
tutta la cartella dell'app: leave-no-trace by design.

### 5.2 Dal server community

Il `rig_signature` è deterministico dal tuo hardware, quindi se hai attivato
V7 community, riattivare Verdict dallo stesso PC produrrà lo stesso hash e
gli esiti restano associati.

Per richiedere la cancellazione **manuale** degli esiti dal server, apri una
[issue GitHub](https://github.com/Leongithacc/Verdict/issues/new) con il tuo
`rig_signature` (che puoi leggere in `evidence.json` sul tuo PC). Non chiediamo
mai nessun altro identificativo. Cancelliamo entro 30 giorni.

## 6. Sicurezza operativa

- **Codice open source**: puoi ispezionare tutto in
  [github.com/Leongithacc/Verdict](https://github.com/Leongithacc/Verdict).
- **Nessun servizio Windows installato**: Verdict è portable, non registra
  servizi di sistema.
- **Nessun kernel driver**: usiamo ETW (già presente in Windows), non
  LibreHardwareMonitor o simili che caricano WinRing0.
- **Anti-cheat safe by design**: Verdict non tocca il processo del gioco,
  non inietta, non hooka, non legge memoria di altri processi. Il co-pilota
  è read-only sul catalogo. La Gaming Session Mode abbassa solo la priority
  dei bloater noti (Discord, OneDrive), mai del gioco.

## 7. GDPR (per gli utenti UE)

### 7.1 Titolare del trattamento

Léon (Leongithacc su GitHub) — progetto personale, non commerciale, MIT license.
Contatto tramite [issue GitHub](https://github.com/Leongithacc/Verdict/issues).

### 7.2 Base giuridica

Consenso esplicito (Art. 6.1.a GDPR) — nessun dato lascia il PC finché non
attivi esplicitamente un feature che manda dati (update check, brain cloud,
community).

### 7.3 Natura dei dati

Il `rig_signature` è considerato dato **pseudonimizzato** (Art. 4.5 GDPR):
è un hash non reversibile a partire da una config hardware, non identifica
una persona fisica direttamente. Chi conoscesse **esattamente** la tua config
hardware potrebbe ricalcolare il code e cercarne gli esiti, ma non ha modo di
associare il code a un nome/indirizzo.

### 7.4 I tuoi diritti

- Accesso: puoi vedere tutti i tuoi esiti nel file `evidence.json` locale.
- Rettifica: applicando `wpep undo` puoi correggere gli esiti; i vecchi
  restano nell'append-only log locale ma il server aggregatore ricalcola le
  stats notturne.
- Cancellazione: vedi sez. 5.
- Portabilità: `evidence.json` è JSON standard, portabile ovunque.
- Opposizione: disattivi il flag `CommunityShareEnabled` e non manda più
  nulla; il vecchio già inviato resta per 365 giorni max.

### 7.5 Trasferimento dati fuori UE

Il Worker community gira su Cloudflare (infrastruttura globale). Cloudflare
è certificata DPF (Data Privacy Framework) per il trasferimento US-UE.
Per il brain AI cloud, il traffico va direttamente al vendor scelto (che ha
la sua policy: Anthropic, Google, OpenAI).

Se hai requisiti stretti di residenza dati UE, usa solo Ollama (locale) e
non attivare la community. Verdict funziona benissimo così.

## 8. Modifiche a questa policy

Le modifiche sono in git storia:
`git log docs/PRIVACY.md`. Se cambiamo qualcosa di sostanziale, viene
menzionato nel CHANGELOG.md sotto la release corrente.

## 9. Domande?

Apri una [issue GitHub](https://github.com/Leongithacc/Verdict/issues) o
mandaci una PR se pensi che questa policy possa essere più chiara.
