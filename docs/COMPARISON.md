# Verdict vs il resto — comparazione onesta

Documento pubblico che confronta Verdict con gli altri strumenti nella categoria
"gaming optimizer for Windows". Aggiornato 2026-07-01.

> **Filosofia del documento**: non è marketing. Nessun competitor è "cattivo".
> Ognuno serve un caso d'uso legittimo. Il valore di Verdict non è "essere
> migliore in tutto" ma "essere il più onesto — sa quando dire no".

Vedi anche gli approfondimenti dedicati:
- [VS_HONE.md](VS_HONE.md) — Hone, il competitor più grosso
- [VS_RAPTECHPC.md](VS_RAPTECHPC.md) — RapTechPC, insufficient evidence

## Chi confrontiamo

| Nome | Categoria | Distribuzione | Prezzo | Sorgente |
|---|---|---|---|---|
| **Verdict** (questo progetto) | Optimizer + measurement | GitHub Releases | Free | MIT |
| **Hone** | Optimizer preset-based | Epic Games Store | Free + Premium | Closed |
| **RapTechPC** | Optimizer tool | TikTok social presence | Free (?) | Non verificato |
| **Wemod** | Game trainer / mod | Wemod client | Free + Pro | Closed |
| **Iolo System Mechanic** | System optimizer generalista | Web download | Subscription (~$50/y) | Closed |
| **Advanced SystemCare (IObit)** | System optimizer generalista | Web download | Free + Pro | Closed |
| **RivaTuner Statistics Server** | FPS limiter + OSD | Guru3D | Free | Closed |
| **MSI Afterburner** | GPU tuning + OSD | MSI | Free | Closed |
| **Windows 11 Game Mode nativo** | Feature OS | Built-in Windows | Incluso | N/A |
| **Windows Game Bar** | Overlay + capture | Built-in Windows | Incluso | N/A |

## Matrice feature onesta

Legenda: ✓ = fatto in modo verificabile · ~ = parziale · ✗ = no per design · ? = non chiaro/verificabile

| Feature | Verdict | Hone | RapTechPC | Wemod | Iolo | IObit | RTSS | MSI AB | Win11 Game Mode |
|---|---|---|---|---|---|---|---|---|---|
| Fonti primarie obbligatorie per ogni tweak | ✓ (regola d'oro codificata) | ✗ | ? | ✗ | ✗ | ✗ | N/A | N/A | N/A |
| Misurazione pre/post con statistica rigorosa | ✓ (Mann-Whitney + bootstrap) | ✗ | ✗ | ✗ | ✗ | ✗ | ~ (FPS log) | ~ (bench) | ✗ |
| Rifiuta di emettere verdetto se rumore alto | ✓ (noise gate) | ✗ | ✗ | N/A | ✗ | ✗ | ✗ | ✗ | ✗ |
| Reversibilità per-tweak (journal + undo) | ✓ | ~ (built-in per tweak) | ? | N/A | ~ (System Restore) | ~ | N/A | ~ | N/A |
| Zero overlay / injection in-game | ✓ (ETW passivo) | ✓ (dichiarato) | ? | ✗ (mod nel processo) | ✓ | ✓ | ✗ (overlay) | ✗ (overlay opz) | ✓ |
| Zero kernel driver | ✓ | ✓ | ? | ~ | ~ (driver aux) | ~ | ~ (opz) | ✗ | ✓ |
| Portable (leave-no-trace) | ✓ | ✗ (installer) | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | N/A |
| Sorgente aperto | ✓ (MIT) | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| Gratuito completo | ✓ | ~ (limits free) | ~ | ~ (limits free) | ✗ | ~ | ✓ | ✓ | ✓ |
| Detection anti-cheat safe | ✓ (per design) | ✓ (dichiarato) | ? | ✗ (mod = kick) | ~ | ~ | ~ | ~ | ✓ |
| AI co-pilot con grounding sul catalogo | ✓ (4 brain swappable) | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| Community evidence opt-in con backend privacy-first | ✓ (Worker + D1) | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| BIOS guide QR per marca | ✓ (6 vendor) | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| DPC/ISR diagnostics per driver | ✓ (ETW) | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| Placebo Museum (mostra cosa NON funziona) | ✓ (14 voci) | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| Rimozione di funzioni essenziali di Windows | ✗ (per policy) | ✗ | ? | ✗ | ✓ | ✓ | ✗ | ✗ | ✗ |
| Modifica in-game (trainer / godmode / etc) | ✗ (per policy) | ✗ | ✗ | ✓ (feature) | ✗ | ✗ | ✗ | ✗ | ✗ |
| FPS limiter | ~ (via NVAPI) | ~ | ? | ✗ | ✗ | ✗ | ✓ (feature) | ✓ | ✗ |
| OSD in-game (FPS, temp, ecc) | ✗ (per policy) | ✗ (dichiarato) | ? | ✗ | ✗ | ✗ | ✓ | ✓ | ~ (Game Bar) |
| Auto Game Priority in Windows | ~ (advise) | ✓ | ? | N/A | ~ | ~ | N/A | N/A | ✓ (nativo) |
| Marketing "+X% FPS garantito" | ✗ (per policy) | ✓ (15-30%) | ✓ (varie) | N/A | ✓ | ✓ | N/A | N/A | N/A |

## Quando scegliere quale

### Scegli **Verdict** se
- Vuoi capire *perché* un tweak funziona (e quando non funziona)
- Non ti fidi delle promesse "+X% FPS garantiti"
- Vuoi misurare con rigore statistico che il tuo tweak abbia effetto vero
- Ti serve BIOS QR guide gratis, verificate per la tua marca
- Vuoi un AI co-pilot locale (privacy) o cloud (qualità), a tua scelta
- Sei sviluppatore o power user: CLI-friendly, scriptabile
- Vuoi portable + open source

### Scegli **Hone** se
- Vuoi un'interfaccia curatissima con branding gaming
- Preferisci preset one-click senza voler capire il perché
- Sei disposto a pagare per una curated experience
- Vuoi profili per gioco già configurati da un vendor esports-connected

### Scegli **RivaTuner + MSI Afterburner** se
- Vuoi overclock GPU + monitor OSD in-game
- Vuoi FPS limiter di precisione
- Sei OK con overlay (non giochi anti-cheat competitive)

### Scegli **Wemod** se
- Vuoi cheat/trainer offline per giochi single-player
- **NB**: Wemod è un game trainer, non un "optimizer" — categorie diverse

### Scegli **Iolo / IObit** se
- Vuoi un tool commerciale con supporto telefonico
- Sei OK a pagare abbonamento annuale
- Ti fidi del brand
- **Avvertenza**: entrambi in passato hanno avuto controversie su rimozione
  aggressiva di componenti Windows / registry deep-clean. Verifica reviews recenti.

### Usa **Windows 11 Game Mode + Game Bar nativi** se
- Non vuoi installare niente di terze parti
- Ti basta il "just works" MSFT
- Non ti interessa il "perché" dei tweak
- **Nota**: coprono ~30% di quello che copre Verdict — sono complementari,
  non sostitutivi.

## Cosa Verdict NON è

Per chiarezza:
- **Non è un game trainer** → per quello vedi Wemod
- **Non è un OSD in-game** → per quello vedi RTSS/MSI AB
- **Non è un tuning tool GPU** → per quello vedi MSI Afterburner o AMD Adrenalin
- **Non è un cleaning tool aggressivo** → per quello vedi CCleaner (con la sua
  storia controversa) o Iolo/IObit
- **Non è un anti-cheat bypass** → è per design compatibile con anti-cheat,
  non li aggira

## La domanda che conta davvero

Non è "quale optimizer ha più feature?". È: **"il tuo optimizer sa dirti quando
NON servirà a nulla farlo?"**

Verdict risponde sì. Al Noise Score basso, alla misura sotto la soglia MDE, al
placebo museum. Gli altri, quasi mai. Questo è l'asset competitivo che Verdict
sceglie di coltivare nel tempo: **la credibilità onesta**.

---

*Documento aggiornato in occasione della release v1.1. Se noti errori sui
competitor (ho fatto del mio meglio con la ricerca pubblica disponibile), aprimi
issue con la fonte corretta.*
