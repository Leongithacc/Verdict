# Verdict / WPEP — Brief per la prossima sessione

> Preparato in autonomia il 2026-06-20 da Opus 4.8 mentre Léon era via.
> Menu ordinato per importanza: scegli da qui quando torni.

## ✅ V4 "Intelligenza per-gioco" — FATTO il grosso (2026-06-23, commit ce242f7→8dd4b65)
Vedi `docs/ROADMAP.md` (sez. V4) e audit log #64. In sintesi:
- Impostazioni in-game arricchite/oneste: Valorant 6, **CS2 6, Apex 5, Overwatch2 4**, Fortnite 9.
- 2 titoli NUOVI first-class (rilevamento Steam + KB): **THE FINALS** e **Rainbow Six Siege**. KB 120 voci.
- **Network Duel game-aware**: `wpep network <gioco>` (baseline + anchor ecosistema) + guardia di
  accoppiamento (test: ogni slug-gioco KB ha un anchor). Suite 286/286, artifact ripubblicato.
- Restano a richiesta: bufferbloat sotto-carico, altri titoli, selettore-gioco nel Network Duel GUI.

## ✅ V5 "Automazione & fiducia" — FATTO il grosso (2026-06-23, commit 1f2ee54)
Léon ha scelto V5. Vedi `docs/ROADMAP.md` (V5) + audit #65.
- **Tray host** NUOVO `WPEP.Tray` (wpep-tray.exe): agente WinForms isolato, poll 10 min, balloon SOLO
  sui nuovi alert (WatchdogMonitor anti-spam). Avviabile da GUI ("Avvia in background"). Read-only.
- Core condiviso testato: `WatchdogProbe.RunPass(detectDrift)` (CLI/GUI/tray) + `WatchAlert.Key` + monitor.
- Artifact: App + wpep-tray.exe in artifacts/app (coesistono con WPEP.exe). Suite 291/291.
- ✅ Coda V5: avvio automatico con Windows (opt-in, reversibile, checkbox GUI). Restano (minori): Sentinel
  nel tray, rewind GUI Time Machine, intervallo configurabile.

## 🎨 V2 — HANDOFF CLAUDE DESIGN PRONTO (2026-06-23)
Il pacchetto per la fase estetica è pronto (Léon: il design lo fa Claude Design):
- `docs/CLAUDE_DESIGN_PROMPT.md` — **prompt pronto da incollare** in una sessione Claude Design col repo aperto.
- `docs/CLAUDE_DESIGN_BRIEF.md` — brief completo: manifesto-file (cosa toccare/cosa no), inventario emoji
  da sostituire (con codepoint), schermate V4/V5, guardrail (solo visivo, build 0/0 + 291 test verdi).
- Cardini design: `src/WPEP.App/Themes/Theme.xaml` (token) + `src/WPEP.App/MainWindow.xaml` (pagine).
  Claude Design lavora IN-REPO (nessuna copia file necessaria).

## 🔬 REVIEW MULTI-AGENTE 2026-06-22 — finding RIMASTI da fixare
Un workflow adversariale ha trovato 16 bug confermati. Fixati subito: nvidia-drs in isCreate (Undo),
pipe async in RealPowerCfg/RealBcdEdit.Run (anti-deadlock), ~25 stringhe UI tradotte in italiano.
**ANCORA DA FARE (in ordine di valore):**
1. ✅ **FATTO (commit af59520)** Apply non congela più la UI: Confirm (single+all) su `await Task.Run`;
   + un solo restore-point per batch (Execute(plan, createRestorePoint:false)).
2. ✅ **FATTO** Scan batch NVAPI: nuovo `NvApi.ReadDwordSettings` + `LiveState.Detector` (condiviso
   CLI/GUI) leggono TUTTI i nvidia-drs in UNA sessione. Bonus: niente più eccezioni per-tweak su
   macchine non-NVIDIA (la sessione fallisce una volta e ritorna nvOk=false). Stati identici verificati.
3. ~~(MEDIUM) gate gpu:nvidia fail-open → eccezioni~~ → il sintomo (spam eccezioni) è risolto dal
   batch #2 (nvOk=false, niente throw). Resta solo cosmetico (mostra "non rilevabile" su non-NVIDIA).
4. **(MEDIUM) Resto stringhe UI inglesi**: pagina Settings, pagina Changes/Modifiche, Diagnostics,
   badge "Strong evidence"+filtri evidenza, "Start over"/"Explain my Stutter"/"Scenario protocol"/
   "Seconds per run"/"Start baseline"/"Trust mode"/"Expected impact"/"How to (manual)". Tradurre.
4. **(MEDIUM) Resto stringhe UI inglesi** (Settings/Changes/Diagnostics/badge evidenza) → Léon: si
   fanno ALLA FINE col pass Claude Design, non ora.
5. **(LOW) powercfg-value confronta solo indice AC** ma scrive AC+DC: su laptop semantica scorretta
   (desktop ok → non urgente). 6. **(LOW) nvidia-drs default==target** mostra "Da attivare" invece di
   "Già attivo" (NVAPI non espone i default; cosmetico). 7. ✅ FATTO descrizione disable-consumer-features
   onesta. 8. ✅ FATTO match disco exact-first.

## ⭐ SVOLTA 2026-06-21 — da "v1 figa" a "app élite" (LEGGI QUESTO PRIMA)
Léon ha guardato l'app e l'ha sentita "fatta da un bambino": troppe voci NON azionabili,
knowledge base che "insegna" invece di "fare", lab che non sa dove si attivano, scan che "sembra
non funzionare", e soprattutto **manca il feel/estetica**. Si è anche un po' esausto (gli è mancato
Fable 5, che ho provato a chiamare ma è gated/non disponibile). DIREZIONE DECISA INSIEME:
- **Verdict deve FARE, non INSEGNARE.** L'app mostra SOLO ciò che applica con un click; la roba
  gui-only/educativa/knowledge-base passa dietro o sparisce. Corta, nera, cattiva, un'arma.
- **Estetica villain/carbonio/cockpit (viola #4A0080)** → si fa col **pass Claude Design**, con Léon
  presente. C'è già un CONCEPT visivo (SVG) mostrato in chat il 2026-06-21 — usarlo come riferimento.
- **Catalogo esploso** dove ha senso, senza diventare un "tweaker generico" (no placebo).
- Fatto il 2026-06-21: +4 tweak NVIDIA (texture filtering: QUALITY_ENHANCEMENTS 0x00CE2691=0x14,
  aniso opt 0x00E73211, neg LOD clamp 0x0019BB68, trilinear 0x002ECAF2) + 2 "Game Focus" background
  (DiagTrack, consumer-features) → **one-click 22 → 28**. Browser GUI ora parte da "Applicabili".
  Artifact (CLI+GUI) RIPUBBLICATO: il tool che lancia Léon ha tutto.
- ⚠️ **NOTA**: il PC di Léon è GIÀ super tweakkato (profilo BXTool), quindi molti tweak leggono "già
  a posto" per lui → il cambiamento che SENTIRÀ è il design/feel, non il numero di tweak.
- 🐞 **DA CAPIRE**: Léon dice "lo scan sembra non funzionare". La logica scan FUNZIONA (CLI ok,
  auto-run nel costruttore MainViewModel). È un problema di UX/feedback GUI (non si vede che parte?
  rescan sembra inerte?). NON diagnosticabile alla cieca: serve che Léon dica COSA vede, o il pass GUI.
- 🧱 **LIMITE REALE**: la GUI WPF non è screenshottabile da computer-use (non registrata nel sistema).
  Quindi feel/scan-UX/lab/skin = lavoro DA FARE VEDENDOLA con Léon. Non costruire l'estetica alla cieca.

## Dove siamo (stato a fine sessione)
- **7 commit oggi**, suite **279/279 verde**, write path **provato dal vivo** (VRR write→verify→undo, zero stalli, niente clobbering).
- **One-click: 18 → 22.** Comando nuovo `wpep applicable` = panoramica a colpo d'occhio di tutto ciò che è applicabile con un click, con stato live.
- **3 bug reali risolti**: stallo restore-point · powercfg rotto su Windows IT · conflitto KB che scartava NetworkThrottling.
- I profili predefiniti (Competitive/Single-player/Daily) ora includono i nuovi tweak.
- Tutto consistente CLI↔GUI (stesso `ExecutionEngine` + `ApplyPolicy`).

---

## ✅ CHIUSO con verdetto — Blocco 3 NVIDIA RTX50 (ricerca fatta, niente da implementare)
Ricerca read-only completata. Conclusione definitiva con fonti:
- **Low Latency Mode "On"** = "Maximum pre-rendered frames" `0x007BA09E=1` → **GIÀ implementato** (`nvidia-low-latency-on`).
- **Low Latency "Ultra", Reflex, Frame Generation, Smooth Motion, DLSS override**: NVIDIA dichiara
  esplicitamente sul forum dev che NON sono supportati via NVAPI SDK; nessun riferimento Profile
  Inspector (DeadManWalkingTO né xHybred Revamped) li espone come DRS. → **non automatizzabili in
  sicurezza**, sono gestiti dall'app NVIDIA / integrazione in-game.
- Esiste un **FPS Limiter** via DRS ma richiede un valore scelto (dipende dal monitor) → non un
  toggle one-click. Eventuale tweak "parametrico" futuro, non ora.
- **Azione**: nessuna. La superficie DRS one-click è esaurita con i 3 tweak NVIDIA già presenti.
- Fonti: forums.developer.nvidia.com/t/low-latency-mode-setting/241104 ; github NVIDIA/nvapi ;
  github Orbmu2k/xHybred nvidiaProfileInspector CustomSettingNames.xml.

---

## 🔜 DA FARE quando torni — ordinato per importanza

### 1. (FATTO 2026-06-21) Field-test write path — ha scoperto un BUG GROSSO
- **`nvidia-drs`**: ✅ field-testato → ha rivelato che la struct NVDRS_SETTING era RIFIUTATA da NVAPI
  (-9, UnionSize 4104→4100) e nvidia-drs **non aveva mai funzionato**. FIXATO e validato dal vivo
  (read di 3 valori reali + write round-trip PSTATE 1→0→1 reversibile). Test regressione su sizeof=12320.
- **`dxuser`**: ✅ già validato (VRR).
- **Ancora da fare (serve terminale ADMIN, bassa priorità — write già validato in altri modi)**:
  da admin, opzionale, per spuntare HKLM/bcdedit dal vivo: `wpep apply
  systemresponsiveness-gpupriority-registry --yes` / `hags-hardware-gpu-scheduling --yes` → undo;
  `wpep apply disable-dynamic-tick --yes` (bcdedit; se già 'yes' è no-op). NB: sul PC di Léon questi
  sono GIÀ a target, quindi sarebbero no-op senza prima revertirli — basso valore, salta pure.

### 2. (ALTO) Review visiva della GUI — serve i tuoi occhi
La GUI eredita tutto per design (data-driven), ma va GUARDATA. Checklist:
- I 5 nuovi tweak (nvidia-low-latency-on, nvidia-vsync-off, win11-windowed-optimizations,
  win11-variable-refresh-rate, pcie-aspm-off) compaiono e mostrano lo stato giusto?
- I profili aggiornati (Competitive 11 tweak) si applicano dalla GUI?
- Il gating admin e i messaggi "non adatto a questo hardware" si vedono bene?
- Apri, fai uno screenshot, e decidiamo i ritocchi.

### 3. (MEDIO, SOLO ALLA FINE — tua regola) Polish estetico "Claude Design"
Come dicevi: estetica solo alla fine. Quando il funzionale è validato, passiamo il tema/le viste a
una rifinitura visiva. Da fare con te presente (sei il giudice del look "villain/carbonio").

### 4. (BASSO/opzionale) Altri tweak one-click
Rendimenti decrescenti: la maggior parte dei gui-only restanti è GIUSTAMENTE non automatizzabile
(in-game/BIOS/cavi). Aggiungere altro rischia di diluire l'ethos anti-placebo. Solo se emergono
candidati ad alta evidenza e verificabili.

---

## Note tecniche utili
- Build veloce: `dotnet build src/WPEP.Cli/WPEP.Cli.csproj -m:1 --disable-build-servers` (~10s).
- Test mirati col `--filter`; suite completa solo ai checkpoint (~45s, 279 test).
- Metodi engine: registry, powercfg, powercfg-value, bcdedit, nvidia-drs, dxuser. Tutti con
  journal + undo + verify. `dxuser` = read-modify-write del REG_SZ DirectXUserGlobalSettings.
- `wpep applicable` per la panoramica; `wpep changes` / `wpep undo last` / `wpep panic` per il rollback.
