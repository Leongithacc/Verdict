# Verdict vs Platinum+ Optimizer — nota di ricerca

> Ricerca 2026-07-04 (fonti verificate live). Vedi anche [VS_HONE.md](VS_HONE.md)
> e [VS_RAPTECHPC.md](VS_RAPTECHPC.md) per gli altri competitor della categoria.
>
> **Sintesi in una riga**: Platinum+ Optimizer è uno script batch/PowerShell
> elevato distribuito senza README, licenza né documentazione delle modifiche,
> e diverse sue versioni `.bat` risultano marcate *"Malicious activity"* dal
> verdetto automatico del sandbox pubblico ANY.RUN. Non ne verifichiamo il
> comportamento come "sicuro"; per la regola d'oro di Verdict questo basta a
> **non** trattarlo come fonte da cui importare tweak.
>
> Questo doc non afferma che Platinum "è un malware": afferma cosa è
> **osservabile pubblicamente** e cosa **non siamo riusciti a verificare**.

## 1. Cosa abbiamo cercato

- Un repository / sito canonico del progetto e la sua documentazione.
- La natura tecnica dello strumento (che cosa esegue, con quali privilegi,
  quali aree di sistema tocca).
- Analisi indipendenti riproducibili (sandbox pubblici, non opinioni).

## 2. Cosa abbiamo trovato — separato per grado di verificabilità

### 2a. Osservabile e verificabile

**Repository GitHub** — [`github.com/Aledect/Platinum-Optimizer`](https://github.com/Aledect/Platinum-Optimizer)
(verificato 2026-07-04). Si descrive:
*"Advanced Windows optimization tool for performance, debloat, and privacy.
Free to use. Compatible with Windows 10/11."* Topics dichiarati includono
`fps-boost`, `gaming-optimization`, `latency-reduction`, `telemetry-removal`,
`system-tweaks`. Composizione: **PowerShell 94%**, HTML 6%; file principali
`install.ps1` e `index.html`; distribuzione anche via
`platinum.optimizer.workers.dev`. **Assenti nella pagina del repo al momento
della verifica**: un README con documentazione, un file LICENSE, release note,
e qualunque documentazione di *quali* modifiche di sistema applica.

**Report sandbox pubblico** — ANY.RUN, analisi di `Platinum+ Optimizer v3.1.bat`
([report](https://any.run/report/08c019d8d0bea8e26b000df3ee7fc0a8e032e39c04bf61da4ed2cdb08b097102/69fa9955-552b-4b4f-90db-b150a8c588b3),
verificato 2026-07-04). Comportamenti **osservati** (fatti, non interpretazioni):

- Catena di `cmd.exe` → `PowerShell.EXE`, con `TIMEOUT.EXE` a scandire i passi.
- Modifiche registry a parametri TCP/IP: `TcpAckFrequency`, `TCPNoDelay`,
  `TcpDelAckTicks`; `SystemRestorePointCreationFrequency` impostato a 0.
- `SC.EXE` invocato 67+ volte per riconfigurare servizi Windows; servizi
  **disabilitati** tra cui `WdiServiceHost` (Diagnostics) ed **`EFS`**
  (Encrypting File System).
- `NETSH.EXE` su firewall e TCP: `int tcp set security mpp=disabled`, **RSS
  disabilitato**, parametri UDP globali modificati; `IPCONFIG` per flush DNS.
- `PLUGScheduler.exe` che scrive log in `C:\ProgramData\PLUG\Logs\`.
- Indicatori di **rilevamento ambiente virtuale** (controlli registry) e
  trigger di riavvio/shutdown del sistema.

Molte di queste sono esattamente le "magie di rete" che la Knowledge Base di
Verdict classifica come **controverse o placebo** dietro fonte primaria:
`TcpAckFrequency`/Nagle è in KB come *controversial* (inerte per i giochi UDP),
disabilitare RSS non ha fonte primaria a favore del gaming (vedi
[KB_RESEARCH.md](KB_RESEARCH.md), candidato *skipped*), e disattivare `EFS` è una
modifica ad area di sicurezza, non un tweak di performance.

### 2b. Percezione (non è evidenza tecnica)

- Esistono **tutorial video di terzi** ("How to Use Platinum+ Optimizer V4.0")
  e una presenza social/community: marketing e passaparola, non documentazione.
- Il verdetto **`"Malicious activity"`** che ANY.RUN assegna a più versioni `.bat`
  di Platinum è una **classificazione automatica del sandbox**, non una prova di
  intento doloso: va letto come "questo binario esibisce pattern che il sistema
  automatico marca come rischiosi", non come una condanna verificata.

### 2c. Non verificabile

- **Identità dell'autore / manutentore** (i file circolano con handle tipo
  `@STEFANO83223`, `@Aledect`): non attribuibile con certezza.
- **Business model** e roadmap: nessuna fonte canonica.
- **Feature list completa e comportamento esatto** della build corrente: senza
  README/LICENSE e con lo script che cambia versione dopo versione, non è
  possibile fissare cosa faccia "Platinum" in generale — solo cosa fa *una
  specifica build* osservata in sandbox.

## 3. Cosa portiamo dentro Verdict

**Niente.** Zero tweak importati. Ogni modifica osservata nei sample o è già in
KB con il suo grado di evidenza onesto (spesso *controversial*/*placebo*), o è
esclusa per policy (aree di sicurezza come EFS, servizi diagnostici). Non
esiste un tweak "nuovo e con fonte primaria" che questo strumento sblocchi.

Per la **regola d'oro** (nessuna fonte primaria verificata = nessuna voce),
Platinum non è nemmeno una *fonte* candidabile: uno script elevato non
documentato non attesta *perché* un tweak funzioni.

## 4. Il contrasto di design (si scrive da solo)

| Aspetto | Platinum+ Optimizer (da ciò che è osservabile) | Verdict |
|---|---|---|
| Forma | Script `.bat`/PowerShell elevato, versionato a mano | App .NET con engine di scrittura tracciato |
| Trasparenza modifiche | Nessuna doc di cosa tocca; si scopre solo in sandbox | Ogni tweak ha descrizione, fonte primaria, passi manuali |
| Fonte primaria per tweak | Non presente | Obbligatoria (regola d'oro codificata nel validator) |
| Dry-run prima di scrivere | Non osservato | Sì (piano esatto mostrato prima) |
| Journal + undo per-modifica | Non presente | Sì (journal-before-write, verify-after, rollback globale) |
| Aree di sicurezza (EFS, servizi) | Toccate | Escluse per policy |
| Misura pre/post con statistica | No | Mann-Whitney + bootstrap + noise gate |
| Verdetto sandbox pubblico | Più versioni marcate *"Malicious activity"* (ANY.RUN) | N/A (nessuno script elevato opaco distribuito) |
| Licenza / sorgente ispezionabile | LICENSE assente al controllo | MIT, sorgente completo |

## 5. Se emergesse documentazione affidabile

Se Platinum pubblicasse un README con l'elenco esatto delle chiavi/servizi
toccati **e** le fonti primarie per ciascuno, si potrebbe confrontare voce per
voce con la KB. Fino ad allora questo doc resta archiviato: la posizione di
Verdict non è "Platinum è cattivo", è *"non abbiamo potuto verificarlo come
sicuro, e non importiamo da fonti non verificabili"*.

Cita questo doc quando un utente chiede "ma Platinum+ non dà più FPS?":
_"Abbiamo guardato cosa fa davvero in sandbox: tocca gli stessi parametri di
rete che la nostra KB classifica come controversi/placebo, disattiva servizi e
aree di sicurezza, e gira come script elevato senza journal né undo. Non
abbiamo trovato la documentazione per validarlo. Se ce l'hai, aprimi una issue."_

---

*Fonti verificate live il 2026-07-04: repository GitHub del progetto e report
pubblico ANY.RUN citati sopra. Verdict non ha alcuna affiliazione con Platinum+
Optimizer o i suoi autori. Le classificazioni ANY.RUN sono verdetti automatici
del sandbox, riportati come tali.*
