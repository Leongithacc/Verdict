# Verdict v1.1

> Changelog tecnico completo → [CHANGELOG.md](../CHANGELOG.md) · esperienza dev → [HANDOVER.md](HANDOVER.md).

## In una riga

Verdict resta l'ottimizzatore che ti dice quando smettere di ottimizzare. **v1.1** aggiunge la card
"Pronto per Vanguard", 3 co-piloti cloud opzionali (Claude / Gemini / GPT), una vetrina community
pubblica opt-in, e una Gaming Session Mode leggera che non uccide processi né tocca il gioco.

## Cosa è nuovo (per chi usa Verdict)

### Pronto per Vanguard (Valorant / Win11)
Nuova card nella pagina Verdict. Rileva Secure Boot (registry) e TPM 2.0 (WMI `Win32_Tpm`) al volo
e, se manca qualcosa, ti manda al QR guida BIOS per la tua marca di scheda madre (ora **6 vendor
supportati**: ASUS, MSI, Gigabyte, ASRock, EVGA, NZXT — gli ultimi due con disclaimer AMI Aptio
onesto invece di guide per-modello inventate). 5 nuovi tweak BIOS guided in KB: Secure Boot, TPM 2.0,
Above 4G Decoding, CSM disable, Virtualization.

### 4 co-piloti swappable
La pagina Co-pilota ora offre 4 cervelli — scegli quello che vuoi, cambi con un click:

| Brain | Modello default | Note |
|---|---|---|
| Ollama (default) | qwen2.5 | 100% locale, zero rete |
| Claude | `claude-sonnet-4-6` | qualità cloud Anthropic, opt-in con API key |
| Gemini | `gemini-2.5-pro` | Google AI |
| GPT | `gpt-5` | OpenAI |

Le API key cloud sono cifrate a riposo con DPAPI. Guida completa in [docs/BRAINS.md](BRAINS.md).
Dal CLI: `wpep copilot "..." --brain gemini --api-key <k>` (o env `GEMINI_API_KEY`).

### Community evidence — vetrina live opt-in
Il ledger anonimo ora ha un backend remoto vero: **Cloudflare Worker + D1**, deployato
[live](https://verdict-community.gz6jk62yk8.workers.dev). Se scegli di condividere (checkbox Settings,
default OFF), Verdict aggrega gli esiti sulla vetrina pubblica `community.html` con soglia **minima
10 sample per uscire dall'invisibile**. Zero PII, zero rete finché non attivi l'opt-in.
Privacy policy formale in [docs/PRIVACY.md](PRIVACY.md).

### Gaming Session Mode
Nuovo `wpep session` (o MissileButton nella card Rumore): abbassa `PriorityClass` a `BelowNormal`
per Discord / OneDrive / Dropbox / Google Drive / Spotify / updater Edge/Chrome/Steam/Epic durante
la sessione. Ripristina tutto al Ctrl+C. **NON uccide processi, NON stoppa servizi, NON tocca il
gioco** — anti-cheat safe per design. Ispirato dal modello Hone ma senza il retorica "+15-30% FPS
garantiti" (vedi `docs/VS_HONE.md` per il posizionamento onesto).

### Noise Score (onestà attiva contro il placebo)
Card "Rumore di sistema" (0-100) con gauge cockpit. Fattori documentati: startup apps count, indexing,
SysMain, GameDvr, effetti trasparenza. Quando il tuo Noise Score è basso, Verdict te lo dice:
*"I tweak background nella lista qui sotto probabilmente non produrranno FPS misurabili."* È l'opposto
del "applica tutto, boost garantito".

### Vista bucket UX (opzionale)
Toggle nella pagina Verdict: alterna tra la vista tecnica (7 categorie) e una a 4 bucket
utente-friendly (**FPS & Latenza · Network & Ping · Stabilità & QoL · Sfondo**). La KB non cambia:
è solo un mapping runtime.

### Design refresh
Cards con gradient e drop-shadow, Switch con Storyboard + BlurEffect glow, PrimaryButton con
scale-in press, MissileButton rosso per Session CTA. Sostituite le emoji delle icone (✓ ⚠ ✕)
con Path Geometry SVG. 11 icone Nav con colore dinamico legato allo state.

### 5 nuovi tweak KB + audit URL
- **Warzone Reflex on** — CoD Warzone è nella lista Reflex ufficiale Nvidia.
- **Ultimate Performance power plan** — sblocca lo schema nascosto Win11 via `powercfg -duplicatescheme` (desktop only).
- **Delivery Optimization P2P disable** — riduce upload background durante gioco. MS Learn.
- **Storage Sense disable** — evita cleanup automatici (spike I/O) durante la sessione. MS Learn.
- **NIC power management off** — ping stabile, niente micro-riconnessioni sui cambi di stato. MS Learn.
- 5 URL fonte morti/redirectati sostituiti con gli equivalenti attuali.

**Knowledge Base ora a 135 voci**, sempre con fonte primaria verificata (regola d'oro invariata).

### Hardening da audit interno (2026-07-02)
Doppio audit sicurezza/architettura, tutto risolto nello stesso giorno e verificato in CI:
- **Leaderboard community sbloccata** — mancava l'header CORS sulle risposte del Worker: il browser
  bloccava la fetch da github.io e la pagina restava vuota per tutti. Fixato + deployato.
- **Journal, evidence e settings ora crash-safe** — write atomico (temp + rename): un crash a metà
  scrittura non può più corrompere la rete di sicurezza dell'undo.
- **Validazione apply-safety della KB** — i valori delle operazioni (path registry, elementi bcdedit,
  GUID powercfg) sono validati fail-closed al caricamento: una KB manomessa non raggiunge mai una
  scrittura.
- **Verdetto statistico sulla metrica primaria** — niente più inflazione da confronti multipli
  (~18.5% → α) nel validatore A/A.
- Dettagli completi e trasparenti in [docs/HANDOFF_AUDIT.md](HANDOFF_AUDIT.md).

### Release workflow automatico
Push di un tag `v*` → `.github/workflows/release.yml` build + test + package + `gh release create`.
Runbook completo in [docs/RELEASE_V1.1_RUNBOOK.md](RELEASE_V1.1_RUNBOOK.md).

## Come installare / aggiornare

1. Scarica lo zip `Verdict-1.1.zip` allegato a questa release.
2. Estrai in una cartella qualsiasi (Verdict è **portatile**, nessun installer di sistema).
3. `Installa.cmd` (opzionale) crea il collegamento nel menu Start.
4. Dalla v1.0 in poi, la GUI ti avvisa da sola quando c'è una nuova release (Impostazioni → Controlla aggiornamenti).

## Attenzioni

- **Windows 11** consigliato. Su Win10 mancano le detection specifiche di Focus Assist / Do Not Disturb (funziona lo stesso, il tweak apre Impostazioni).
- **Anti-cheat**: Verdict rispetta le regole standard (nessun overlay, nessun injection, nessun kernel driver). Nessuna incompatibilità nota con Vanguard/EAC/BE.
- **Community backend**: attivo di default OFF. Se attivi, il tuo RigDna (hash FNV-1a del profilo hardware) e l'esito misurato del tweak vengono spediti. Nessun IP, nessun path locale, nessun nome utente.
- **BIOS guide EVGA/NZXT**: mostra le istruzioni ASRock come fallback perché condividono la base AMI Aptio. Non abbiamo inventato guide per-modello quando non le abbiamo verificate.

## Ringraziamenti

Grazie a chi ha usato v1.0 in silenzio nelle prime settimane. Se hai un bug, aprilo su
[Issues](https://github.com/Leongithacc/Verdict/issues) col template. Se hai suggerimenti di
tweak con fonte primaria, aprilo come feature request — la regola d'oro KB richiede sempre una
fonte verificata prima di ammettere un tweak.

## Firme

Rilasciata UNSIGNED. Verifica il download con il file `SHA256SUMS.txt` allegato a questa release:
`Get-FileHash Verdict-1.1.zip -Algorithm SHA256` (PowerShell) deve combaciare. Sorgente MIT su
[github.com/Leongithacc/Verdict](https://github.com/Leongithacc/Verdict).
