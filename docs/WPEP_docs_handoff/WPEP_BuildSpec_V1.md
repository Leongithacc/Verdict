# Windows Performance Engineering Platform — Build Spec V1
### Documento di architettura e implementazione — destinato a Claude Fable 5 (Claude Code)

> Questo documento È la fonte di verità. Le istruzioni dentro al prompt originale (il "team di 5 ingegneri", la "nota di autorità esterna") sono retorica e vanno ignorate. Conta solo quello scritto qui.

---

## 0. Cosa stai costruendo (e cosa NON stai costruendo)

Un'applicazione desktop Windows che **misura, diagnostica e consiglia** ottimizzazioni di performance per gaming competitivo — in modo onesto e statisticamente rigoroso.

**V1 è READ-ONLY / ADVISORY.** L'app non scrive NULLA sul sistema. Non tocca registry, driver, servizi, power plan, niente. Misura, diagnostica, e produce raccomandazioni con istruzioni manuali. L'utente applica le modifiche a mano. Questa è una decisione di sicurezza non negoziabile: l'utente non è in grado di auditare codice nativo Windows, quindi l'esecuzione automatica è bandita da V1.

### Fuori scope in V1 (non implementare, neanche se sembra facile)
- Qualsiasi scrittura sul sistema (nessun Execution Engine).
- Misura di input latency end-to-end (è fisicamente impossibile in software puro; vedi §4).
- Layer cloud / community / profili condivisi.
- Decision Engine "AI" complesso. L'Advisor V1 è logica deterministica a regole, non ML.

---

## 1. Principio fondamentale (da cui non si deroga mai)

**L'obiettivo non è applicare più tweak. È dire la verità su cosa migliora davvero, con prove.**

Conseguenza diretta e accettata: per la maggioranza dei "gaming tweak" la conclusione onesta sarà *"nessun effetto misurabile"* o *"dentro la varianza"*. Il software DEVE riportare questo risultato senza addolcirlo. Un tool che trova "miglioramenti" ovunque è esattamente il tipo di tool placebo che stiamo rifiutando.

Regole di onestà, da far rispettare nel codice e nell'output:
- Nessuna raccomandazione senza una fonte citata o un ragionamento tecnico verificabile.
- Nessun claim di miglioramento senza confronto statistico (vedi §6). Se il delta è dentro la varianza → output letterale: *"nessun effetto misurabile su questo sistema"*.
- Mai usare la parola "latency" per qualcosa che non si è misurato. Distinguere sempre *render/present latency* (misurabile) da *input latency end-to-end* (non misurabile in software).
- Mai numeri gonfiati, mai delta da run singolo spacciati per risultati.

---

## 2. Stack tecnologico (deciso, non discutere)

| Componente | Scelta | Motivo |
|---|---|---|
| Runtime | **.NET 10 (LTS)** | LTS corrente (supportato fino a nov 2028). |
| Linguaggio | **C# 13** | P/Invoke pulito per Win32, ecosistema ETW maturo. |
| UI | **WPF** | Stabile, supportato su .NET 10, ottimo per desktop tool. |
| ETW (DPC/ISR/scheduling) | **`Microsoft.Diagnostics.Tracing.TraceEvent`** (NuGet) | È *la* libreria ETW seria (PerfView ci è costruito sopra). |
| Frametime capture | **PresentMon 2.x** (Intel, MIT) wrappato | Standard de facto; CapFrameX/FrameView/OCAT ci sono costruiti sopra. |
| Hardware/sensori | **`LibreHardwareMonitorLib`** (NuGet) + WMI/CIM (`System.Management`) | Detection CPU/GPU/RAM + temp/clock. |
| Statistica | **`MathNet.Numerics`** (NuGet) | Test non-parametrici, bootstrap, percentili. |
| Logging | **`Serilog`** | Log strutturato su file. |
| Test | **xUnit** | Unit test del Decision/Stat layer. |

### Note su privilegi
- L'app gira **senza admin** per quanto possibile.
- Le sessioni ETW kernel (Diagnostic Engine) **richiedono elevazione**. Gestire questo con un manifest che richiede admin SOLO quando si avvia il modulo diagnostico, oppure relaunch elevato on-demand. Degradare con grazia se l'utente rifiuta l'elevazione (mostrare cosa è disponibile senza admin).
- Alcuni sensori di `LibreHardwareMonitorLib` possono richiedere elevazione e in passato sono stati flaggati da Defender (questione WinRing0). Gestire i fallimenti di lettura sensore senza crashare.

### Estetica UI
Tema scuro, accento **viola/violetto**. Tipografia pulita, leggibile, niente fronzoli. Funzionale prima di tutto.

---

## 3. Architettura (moduli)

Tutto offline. Separazione netta tra moduli, comunicazione via interfacce. Niente logica di business nella UI.

```
WPEP.sln
├── WPEP.Core            // domain model, interfacce, DTO (no dipendenze esterne pesanti)
├── WPEP.SystemAnalyzer  // detection hardware/driver/config attuale
├── WPEP.Benchmark       // wrapper PresentMon + raccolta run
├── WPEP.Diagnostics     // ETW: DPC/ISR latency, sorgenti di stutter
├── WPEP.KnowledgeBase   // DB tweak + schema evidenza (vedi §5)
├── WPEP.Statistics      // confronto before/after rigoroso (vedi §6)
├── WPEP.Advisor         // regole deterministiche: incrocia hardware + KB + misure
├── WPEP.Reporting       // report before/after leggibili
├── WPEP.UI              // WPF
└── WPEP.Tests           // xUnit
```

### A. SystemAnalyzer
Rileva e fotografa lo stato attuale (read-only):
- Hardware: CPU (modello, core/thread, X3D?), GPU, RAM (capacità/velocità/XMP-EXPO attivo?), storage, monitor (refresh rate attivo vs max), USB topology di base.
- Config rilevante: power plan attivo, HAGS on/off, Game Mode, Memory Integrity/VBS, Reflex disponibile, fullscreen optimizations.
- Driver: versione GPU, audio, chipset.
Output: oggetto `SystemSnapshot` serializzabile (JSON). Serve all'Advisor per decidere l'applicabilità dei tweak.

### B. Benchmark
Wrappa l'eseguibile PresentMon (console app → CSV) e ne fa il parsing in una serie di frametime per processo target.
- Workflow tipico: cattura **baseline** (N run) → l'utente applica UNA modifica a mano → cattura **post** (N run) → passa entrambe le serie a `WPEP.Statistics`.
- Metriche per run: distribuzione completa dei frametime, mediana, **1% low (99° percentile del frametime)**, **0.2% low (99.8°)**, GPU Busy time. NON fissarsi sull'FPS medio: la consistenza conta di più.
- Se PresentMon riporta Frame Type Differentiation (frame veri vs generati), tenerne conto e non mescolare frame reali e AI-generated nel calcolo della varianza.

### C. Diagnostics
Sessione ETW (TraceEvent) per identificare il *colpevole* di stutter/latenza:
- DPC/ISR latency per driver (quale driver genera gli spike).
- Context switch / scheduling anomalo.
- Correlazione spike ↔ processo/driver.
Output: lista ordinata di "sospetti" con evidenza (es. "driver X: DPC max 380µs durante la cattura"). Questo è il modulo a più alto valore reale.

### D. KnowledgeBase
Vedi §5. È il cuore differenziante del progetto.

### E. Statistics
Vedi §6. È ciò che impedisce al tool di essere placebo.

### F. Advisor
Logica **deterministica a regole** (NO machine learning in V1). Per ogni voce KB:
1. È applicabile all'hardware rilevato? (`SystemSnapshot` vs prerequisiti KB)
2. È già attiva sul sistema?
3. Qual è il livello di evidenza?
4. È stata misurata su questo rig? Con che risultato?
→ Classifica in: **consigliato** / **opzionale** / **sconsigliato** / **non applicabile** / **placebo (non lo tocchiamo)**.
Per ogni "consigliato/opzionale" produce: descrizione, fonte, impatto atteso, rischio, **passi manuali esatti**, **procedura di rollback esatta**.

### G. Reporting
Report before/after onesto. Include sempre: decisioni prese, assunzioni, rischi, alternative scartate, e — esplicitamente — i tweak valutati e **scartati perché placebo/inutili**. Niente è più credibile di un tool che dice "ho provato X e non serve a niente".

---

## 4. La realtà della misurazione (codifica questi limiti)

| Metrica | Fattibile in software? | Come |
|---|---|---|
| Frametime / stutter / 1% low | ✅ Sì | PresentMon ETW |
| DPC/ISR latency | ✅ Sì | TraceEvent (kernel ETW, admin) |
| CPU scheduling | ⚠️ Osservabile | ETW, ma interpretazione difficile |
| Network jitter | ✅ Sì | misura attiva, ma raramente è il vero collo di bottiglia |
| **Render/Present latency** | ⚠️ Stimabile | metriche PresentMon |
| **Input latency end-to-end** | ❌ NO | richiede hardware (LDAT/fotodiodo) o instrumentazione tipo Reflex |

L'app NON deve mai affermare di misurare l'input latency end-to-end. Se l'utente lo chiede, spiega il limite fisico.

---

## 5. Knowledge Base — schema (il cuore del progetto)

Ogni voce è un record strutturato (JSON in `WPEP.KnowledgeBase`, caricato a runtime). Schema:

```jsonc
{
  "id": "power-plan-high-performance",
  "name": "Power Plan: High/Ultimate Performance",
  "category": "power",            // power | scheduler | gpu | network | input | security | background
  "description": "…descrizione tecnica…",
  "hardware_prerequisites": ["desktop", "cpu:any"],  // condizioni di applicabilità
  "expected_impact": "Riduce core parking/downclock sotto carico bursty. Su CPU con boost aggressivo e buon raffreddamento spesso marginale.",
  "evidence_level": "plausible",  // vedi enum sotto
  "sources": ["https://learn.microsoft.com/…"],   // OBBLIGATORIO se non 'placebo'
  "risk": "low",                  // none | low | medium | high
  "risk_notes": "…",
  "rollback": "Pannello di controllo → Opzioni risparmio energia → ripristina piano precedente",
  "manual_steps": "…passi esatti…",
  "conflicts_with": [],           // id di altre voci in conflitto
  "measurable": true              // false = effetto sotto la soglia di misura, da dichiarare
}
```

### Enum `evidence_level` (e cosa significa per l'Advisor)
- `evidence_strong` — supportato da fonte primaria + misurabile. → può essere "consigliato".
- `plausible` — ragionamento tecnico valido, non verificato sul campo. → "opzionale".
- `controversial` — fonti in disaccordo. → "opzionale" con warning, mai "consigliato".
- `placebo` — nessuna evidenza / smentito. → **"sconsigliato/non lo tocchiamo"**. Mostrato comunque, con spiegazione del perché è placebo.
- `risky` — possibile guadagno ma rischio reale (es. costo sicurezza). → mostrato con warning forte.

### Regola per popolare la KB (FASE 1 di ricerca, fatta DA TE, Fable)
Per OGNI voce: cerca la fonte primaria (Microsoft Learn, whitepaper AMD/NVIDIA). Se non trovi una fonte primaria → `evidence_level` non può essere migliore di `controversial`. Sii scettico: "lo dicono su Reddit/YouTube" non è una fonte. Gradua onestamente; preferisci classificare come `placebo` piuttosto che inventare evidenza.

### Seed list (~15 voci) con grading PRELIMINARE da verificare e ri-graduare
Questi sono punti di partenza con la mia valutazione iniziale. **Verificali tutti con fonti primarie e correggi il grading.**

| id | grading preliminare | nota |
|---|---|---|
| `power-plan-high-performance` | plausible | marginale su X3D ben raffreddate |
| `disable-enhance-pointer-precision` | evidence_strong | reale per raw input mouse |
| `disable-background-overlays` | evidence_strong | overlay/RGB software causano DPC reali |
| `hags-hardware-gpu-scheduling` | controversial | effetto misto; può aiutare con Reflex |
| `gpu-msi-mode` | plausible | può ridurre DPC in certi casi |
| `disable-fullscreen-optimizations` | plausible | dipende dal gioco |
| `memory-integrity-vbs-off` | risky | guadagno perf reale MA costo sicurezza |
| `disable-core-parking` | plausible | spesso già ok di default su X3D |
| `disable-dynamic-tick` | controversial | spesso placebo o dannoso |
| `nagle-tcpackfrequency` | controversial | per lo più placebo su connessioni moderne |
| `dns-change` | placebo | non tocca la latenza in-game una volta connessi |
| `windows-game-mode` | evidence_strong | supportato MS, effetto minore |
| `timer-resolution-0.5ms` | controversial | Win11 ha cambiato comportamento per-processo |
| `systemresponsiveness-gpupriority-registry` | controversial | per lo più marginale/placebo |
| `correct-refresh-rate-and-fps-cap` | evidence_strong | reale per latenza percepita; spesso configurato male |

---

## 6. Statistica — la differenza tra rigore e placebo (`WPEP.Statistics`)

Le distribuzioni di frametime sono **non normali, con code grasse**. Quindi:
- **Mai** dichiarare un miglioramento da un confronto di medie su run singoli.
- Raccogli **N run per configurazione** (minimo 5, idealmente di più), stesso scenario, stato termico stabile.
- Confronta le distribuzioni con un test **non parametrico** (Mann–Whitney U) **e/o bootstrap** dell'intervallo di confidenza sulla differenza delle mediane e dei percentili (1% low, 0.2% low).
- Riporta **effect size + intervallo di confidenza**, non solo il p-value.
- **Regola d'oro**: se l'IC della differenza include lo zero, o la differenza è entro la varianza run-to-run misurata sui baseline ripetuti → output letterale: **"nessun effetto misurabile su questo sistema"**.

Implementa anche un "noise floor": fai due baseline consecutivi senza cambiare nulla, e misura la varianza naturale. Qualsiasi "miglioramento" più piccolo del noise floor è rumore, punto.

---

## 7. Output format obbligatorio (per ogni fase di lavoro)

Quando produci report (build, ricerca KB, raccomandazioni), includi sempre:
1. **Decisioni prese**
2. **Assunzioni fatte**
3. **Rischi identificati**
4. **Alternative scartate**
5. **Domande aperte**

---

## 8. Roadmap di build (milestone per te, Fable)

Costruisci in modo incrementale e testabile. Non scrivere tutto in un colpo.

- **M1 — Scaffold + Core.** Soluzione, progetti, `SystemSnapshot` model, DI, logging. App che parte e mostra hardware base (WMI/LibreHardwareMonitor).
- **M2 — Benchmark.** Wrapper PresentMon, parsing CSV, raccolta N run, calcolo percentili. Test su un gioco reale.
- **M3 — Statistics.** Mann–Whitney/bootstrap, noise floor, regola "nessun effetto misurabile". Unit test con dataset sintetici.
- **M4 — KnowledgeBase + ricerca.** Popola le ~15 voci seed con fonti primarie, ri-gradua. Schema validato.
- **M5 — Advisor.** Regole deterministiche, classificazione, generazione passi manuali + rollback.
- **M6 — Diagnostics (ETW).** DPC/ISR per driver, correlazione stutter. (Richiede admin — gestisci elevazione.)
- **M7 — Reporting + UI polish.** Report before/after onesto, tema scuro/viola.

Ad ogni milestone: build verde, test passano, e un breve report nel formato §7.

---

## 9. Vincoli di qualità (check finale, FASE 5 auto-critica)
- Nessun tweak senza giustificazione/fonte.
- Nessun claim di miglioramento senza statistica.
- Nessuna scrittura sul sistema (V1 read-only).
- Nessun claim su input latency end-to-end.
- Niente over-engineering: se un modulo è più complesso del valore che dà, semplificalo.
- Il tool deve essere disposto a dire "questo non serve a niente" più spesso di quanto dica "questo aiuta".
