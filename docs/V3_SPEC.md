# Verdict V3 — Spec

*Dall'intervista cps con Léon (2026-06-18). V1 (misura/consiglia, Fable) e V2 (applica, Opus)
sono complete e validate sul campo. V3 = scanner hardware + batch/profili + idee nuove.*

## Principio fondante (confermato da Léon)
**Zero driver kernel.** Coerente con la regola del progetto (anti-cheat / Vanguard /
leave-no-trace). Inventario hardware via **WMI/CIM** (`System.Management`), GPU via
**nvidia-smi**. Niente WinRing0/LibreHardwareMonitor nostro. → niente VRM / RPM ventole /
temp-CPU live (richiederebbero un driver). Inventario statico completo: SÌ.

---

## 1. Hardware Scanner  (priorità 1)
Scopo triplice (tutti voluti): **sapere cosa hai** + **guidare i tweak** + **trovare problemi**.

### Cosa rileva (tutto via WMI, no driver)
- **Mobo**: modello, chipset, produttore — `Win32_BaseBoard`.
- **BIOS/UEFI**: versione, data — `Win32_BIOS` (per badge "BIOS vecchio?").
- **CPU**: modello, core/thread, cache, X3D — `Win32_Processor` (già parziale in SnapshotBuilder).
- **RAM**: per-banco capacità + velocità + (timings se esposti) + se gira a EXPO/XMP — `Win32_PhysicalMemory`.
- **GPU**: modello, VRAM, driver — già; + temp/clock via nvidia-smi.
- **Dischi**: modello, capacità, NVMe/SATA, **salute SMART**, temp — `Win32_DiskDrive` + `MSStorageDriver_FailurePredictStatus`.
- **USB / Audio / NIC / Monitor**: modelli, Hz/risoluzione/HDR/VRR — `Win32_PnPEntity`, `Win32_SoundDevice`, `Win32_VideoController`, EDID.

### Output (Léon vuole tutti e 4 questi tratti)
- **Build-sheet card**: stile **scheda tecnica pulita** MA col **tema villain/viola**; componenti ben disposti.
- **Badge/voti** sul setup: es. `RAM non a EXPO ⚠`, `NVMe salute ✓`, `driver aggiornato ✓`,
  `VRR attivo ✓`, `BIOS aggiornabile ⚠`. = la parte "trova problemi".
- **Export PNG**: bottone "Esporta immagine" → `RenderTargetBitmap` della card → file condivisibile (Discord ecc.).
- I componenti rilevati **sbloccano/filtrano i tweak** rilevanti (collega scanner → KB).

---

## 2. Batch Apply + Profili  (priorità 2) — modello "misto" (scelto da Léon)
- **Checkbox list**: tutti i tweak applicabili con spunta → "Applica selezionati (N)" in blocco,
  dietro l'unica schermata dry-run già esistente (journaled, undo per-riga). Resta possibile **1-a-1** (apply singolo c'è già).
- **Profili** = set salvabili di tweak:
  - **Predefiniti curati**: `Competitive` (latenza max), `Streaming`, `Daily`. + l'utente **crea/modifica i propri**.
  - Applica profilo = applica il suo set (dry-run + journal + undo). Cambio profilo = ripristina i precedenti e applica i nuovi.
  - Persistenza: `data/profiles/*.json`.

---

## 3. Idee nuove (Léon le vuole TUTTE)
- **Panic restore**: bottone grosso → annulla TUTTO il journaled in un colpo (undo di tutte le
  sessioni attive, drift-aware: salta ciò che hai cambiato a mano). Sicurezza totale.
- **Gaming session 1-click**: applica un profilo → lancia il gioco → alla chiusura del gioco,
  ripristina. **Precisazione di Léon**: per OGNI tweak del profilo si può scegliere se
  **torna normale dopo** o **resta** → flag per-tweak `revert-on-exit` (sì/no). Quindi alcuni
  tweak sono "solo mentre giochi", altri permanenti.
- **Misura prima/dopo auto**: dopo aver applicato un profilo, opzione di lanciare il bench
  (motore statistico V1 già esistente: Mann-Whitney + bootstrap + noise gate) baseline-vs-post →
  dice se ha migliorato **davvero**, con onestà.
- **Condividi profili**: export/import profilo come file `.json` → condividi i tuoi setup con amici.

---

## 4. Più tweak KB (priorità 3, continuo)
Continuare a espandere la KB con fonti primarie (regola d'oro).

---

## Ordine di build proposto (Léon: "decidi tu")
1. **Hardware Scanner** + build-sheet + export PNG (fondamenta: dà dati a tutto il resto).
2. **Batch/Profili** (checkbox + profili predefiniti/custom + persistenza).
3. **Idee**: Panic restore → Gaming session (con revert-on-exit per-tweak) → Misura prima/dopo → Condividi profili.
4. **Più tweak KB** (in parallelo/continuo).

## Note tecniche / rischi
- WMI puo essere lento: scansione in background con progress.
- SMART/temp dischi non sempre esposti senza admin → degradare con onestà ("non rilevabile").
- PNG export: RenderTargetBitmap richiede il controllo renderizzato (UI thread).
- Gaming session: rilevare la chiusura del gioco = watch del processo (già si rileva il gioco).
- Tutto passa per l'Execution Engine V2 esistente (dry-run/journal/undo/drift-aware/conflict-guard).
