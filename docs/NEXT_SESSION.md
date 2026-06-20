# Verdict / WPEP — Brief per la prossima sessione

> Preparato in autonomia il 2026-06-20 da Opus 4.8 mentre Léon era via.
> Menu ordinato per importanza: scegli da qui quando torni.

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

### 1. (ALTO) Field-test dei restanti write path REALI — serve te al PC
Il ciclo write+verify+undo è provato su `dxuser` (VRR). Restano da validare dal vivo, ognuno con
`--yes` e poi `wpep undo last`:
- **`wpep apply nvidia-low-latency-on --yes`** (metodo `nvidia-drs`, scrittura DRS reale) → poi undo.
  È l'unico metodo che scrive davvero sul driver mai field-testato in WRITE (finora solo dry-run).
- **`wpep apply nvidia-prefer-max-performance --yes`** → undo. Idem.
- Da un **terminale amministratore**: `wpep apply systemresponsiveness-gpupriority-registry --yes`,
  `wpep apply hags-hardware-gpu-scheduling --yes` (HKLM, servono admin), poi undo. Valida il write
  HKLM dal vivo + il gating admin.
- **`wpep apply disable-dynamic-tick --yes`** da admin → valida l'UNICO write path mai provato:
  RealBcdEdit Set/Delete (bcdedit). NB: se è già 'yes' non scrive — controlla prima `wpep applicable`.
Obiettivo: spuntare ogni metodo (registry/powercfg-value/nvidia-drs/dxuser/bcdedit) come "WRITE
provato dal vivo", non solo coi fake.

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
