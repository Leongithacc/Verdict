# WPEP — Portability, Low-End Hardware & Managed Devices
*Deciso in chat il 2026-06-11. Salvare nel repo come `docs/PORTABILITY.md`.
Sesto file del pacchetto handoff.*

## 1. DECISIONE: WPEP è portable. "Leave no trace" è una garanzia di prodotto.

Requisiti vincolanti:
- **Zero installer.** Distribuzione: un singolo zip → una cartella → `WPEP.exe`
  (self-contained win-x64, nessun .NET richiesto sul sistema).
- **Zero servizi Windows, zero task pianificati, zero chiavi registry scritte,
  zero file fuori dalla propria cartella.** Tutti i dati (snapshot, run, report,
  settings.json, log) vivono in sottocartelle accanto all'exe (`data/`, `runs/`,
  `reports/`, `logs/`).
- **PresentMon: usare la CONSOLE APP, mai la variante Service** (quella installa
  un servizio Windows e romperebbe la promessa). Il binario PresentMon scaricato
  sta nella cartella `tools/` di WPEP.
- **Disinstallazione = cancellare la cartella.** Documentarlo letteralmente.
- Le sessioni ETW sono transitorie per natura (muoiono con il processo), ma il
  codice deve garantire la chiusura pulita della sessione anche su crash/kill
  (try/finally + nome sessione fisso con cleanup di sessioni orfane all'avvio).

Copy per README/About (EN):
> Portable by design. One folder, no installer, no services, no registry writes.
> Delete the folder and WPEP was never here. Read-only while running, gone when removed.

Nota di coerenza: questa è l'estensione naturale del principio read-only —
"non modifichiamo il tuo sistema, e non ci lasciamo nemmeno dentro."

## 2. Hardware low-end / laptop vecchi (nuovo caso d'uso supportato)

Su hardware debole il verdetto cambia natura: il problema dominante di solito non
sono i tweak, è il throttling e il carico di fondo. Probe da aggiungere/pesare:

- **Thermal throttling probe** (nuovo, prioritario): durante un bench o uno stress
  breve, leggere clock effettivi e temperatura CPU/GPU; se i clock crollano sotto
  base clock con temperatura al limite → verdetto esplicito: "Your CPU is thermally
  throttling. No software tweak fixes this — clean the fans / repaste / check
  power settings."
  Su laptop: rilevare anche il profilo alimentazione (battery vs AC) — benchmark
  su batteria = invalido, va bloccato come F10.
- **Background load probe**: CPU% media a riposo e top processi; su macchine vecchie
  il colpevole è spesso un agent/antivirus/aggiornamenti, non Windows in sé.
- **Storage health**: un HDD meccanico o SSD pieno/degradato domina l'esperienza
  su macchine vecchie; probe SMART di base + tipo disco già nello snapshot.
- Advisor: su hardware sotto soglia (da definire), riordinare le priorità del
  verdetto (throttling/background/storage PRIMA dei tweak di configurazione).
- Requisito di compatibilità: l'app deve girare decentemente anche su CPU vecchie
  e GPU integrate (UI senza effetti pesanti — già in linea con DESIGN_DIRECTION).

## 3. Dispositivi aziendali gestiti — avvertenza obbligatoria

Contesto: far girare un exe non firmato con tracing kernel su un dispositivo
gestito dall'IT aziendale può violare le policy d'uso, far scattare alert EDR
(Defender for Endpoint ecc.) e creare problemi reali alla persona, non solo al PC.
WPEP non deve permettere che succeda per ignoranza.

DECISIONE — feature "managed device notice":
- All'avvio, probe leggera (read-only, come tutto): la macchina è domain-joined /
  Entra-joined / con MDM enrollment? (rilevabile via API/WMI standard).
- Se sì → banner non bloccante ma chiaro:
  > "This looks like a company-managed device. Running third-party diagnostic
  > tools may violate your organization's IT policy. Get IT approval first."
- Non bloccare (la decisione resta all'utente adulto), ma la notice è sempre
  visibile nel report generato su macchine gestite (l'onestà vale anche qui).

Questa feature trasforma un rischio in un altro segnale di serietà del tool.

## 4. Implicazioni per il packaging (aggiorna HANDOFF §4-packaging)

- Il publish self-contained single-file era già deciso; questo documento aggiunge
  il vincolo "tutti i dati accanto all'exe" (niente %APPDATA% di default).
  Eventuale modalità %LOCALAPPDATA% solo come opzione futura, non default.
- Lo zip di release contiene: WPEP.exe, README breve, LICENSE, hash SHA256
  pubblicato accanto al download.
