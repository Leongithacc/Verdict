# Verdict — Idee premium/élite (brainstorm per Léon, da giudicare)

*Obiettivo di Léon: "roba potente che svolti la app, super premium élite". Lista ampia,
organizzata per tema. ⭐ = candidate "killer". Léon giudica, poi si costruisce.*

## A. Score & Gamification (la "faccia premium")
- ⭐ **Verdict Score** — UN numero 0–100 dello stato di ottimizzazione del PC, in grande in
  homepage. Calcolato da: tweak applicati vs ottimali, EXPO on/off, freschezza driver, power
  plan, impostazioni di latenza, thermals. Sale man mano che sistemi. Premium, immediato.
- ⭐ **Achievement / livelli** — sblocchi badge ("EXPO Enabled", "Zero Placebo", "Sub-10ms",
  "Fully Optimized", "Villain Mode") con progressione. Dopamina + premium.
- **Storico dello Score** — grafico del tuo punteggio nel tempo.
- **Percentile/leaderboard** (anonimo, opt-in) — "il tuo rig batte il 92% di build simili". Flex.
- ⭐ **Score ONESTO** — il punteggio RIFIUTA di premiare i placebo. "Non gonfiamo il numero".
  È il differenziatore del progetto trasformato in feature.

## B. Intelligenza per-gioco
- ⭐ **One-click "Ottimizza per [gioco]"** — scegli Valorant/CS2/Fortnite/Apex → applica i tweak
  di sistema + guida le impostazioni in-game e NVIDIA ottimali per QUEL titolo (KB già per-gioco).
- **Auto-rileva il gioco aperto** → suggerisce il profilo giusto.
- **Lettore config in-game** — legge i config di Valorant/CS2, confronta con l'ottimale, consiglia.

## C. Automazione & sicurezza ("funziona da solo")
- ⭐ **Watchdog (tray)** — monitor in background: ti avvisa se l'EXPO si spegne, driver vecchio,
  un tweak è saltato, salute NVMe cala, temp spike. Icona tray + notifiche.
- ⭐ **Gaming session 1-click** — applica profilo → lancia gioco → al chiudere ripristina i tweak
  scelti (revert-on-exit per-tweak). Già nello spec.
- **Profile scheduling** — applica "Competitive" all'avvio di Valorant, ripristina alla chiusura.
- **Snapshot con nome** — salva uno stato-tweak intero ("prima del LAN", "stock"), ripristinalo.
- **Check-up programmato** — scan settimanale + aggiornamento score + "ecco cosa è cambiato".

## D. La misura come esperienza premium
- ⭐ **Latency Lab** — trasforma il motore bench/statistico in un test before/after guidato e
  BELLISSIMO, con grafici. "Dimostra che il tweak ha funzionato." Data-viz premium.
- **Verdict Wrapped** — recap periodico: "questo mese hai guadagnato X% di 1% low, attivato EXPO,
  risolto 3 problemi". Card condivisibile.

## E. Hardware & upgrade
- ⭐ **Upgrade advisor** — da scan + i tuoi giochi: "la RAM a 5600 è il tuo collo di bottiglia —
  attiva EXPO (gratis); prossimo upgrade: nessuno necessario". Consulenza premium.
- **Health dashboard** — salute NVMe (SMART %), config RAM, thermals (fonti safe), driver, tutto
  in una vista premium.
- ⭐ **Walkthrough EXPO/BIOS** — rileva EXPO off → tooltip guidati (teach-mode) per attivarlo nel
  TUO BIOS ASUS specifico. Risolve davvero il problema, non solo lo segnala.
- **Freschezza driver** — controlla l'età dei driver NVIDIA/chipset, link 1-click all'update.

## F. Community & condivisione
- **Marketplace profili** — scarica i profili dei pro/community, votali.
- **Condividi build-sheet/score** — la card villain come "trading card" condivisibile (PNG c'è già).
- **Cloud sync** — profili/impostazioni/score ti seguono su più PC (tier premium).

## G. UX / polish premium
- **UI villain animata** — accenti animati sottili, build-sheet come card lucida, transizioni premium.
- **Wizard di onboarding** — primo avvio "ottimizziamo il tuo rig in 60 secondi".
- **Command palette (Ctrl+K)** — azioni rapide da power-user.
- **Temi come unlockable** — temi premium legati agli achievement/score.

## H. Trust & onestà (il differenziatore)
- **"Perché" deep-dive** — ogni tweak con fonte primaria, grado di evidenza, misurato-o-no: un
  browser della conoscenza premium. (I dati ci sono già.)

---

## Mia top-8 consigliata (per "svoltare" l'app, ordine di impatto/fattibilità)
1. ⭐ **Verdict Score** (+ achievement) — dà identità premium istantanea.
2. ⭐ **One-click "Ottimizza per [gioco]"** — utilità enorme, sfrutta la KB per-gioco.
3. ⭐ **Watchdog tray** — "funziona da solo", premium, ricorrente.
4. ⭐ **Gaming session 1-click** (revert-on-exit) — feature-firma.
5. ⭐ **Latency Lab** — trasforma la statistica in wow-factor.
6. ⭐ **Walkthrough EXPO/BIOS** — risolve il problema #1 trovato sul TUO PC.
7. ⭐ **Upgrade advisor** — consulenza premium dallo scan.
8. ⭐ **Panic restore** — ✅ già fatto (base di fiducia).

*Léon: segna quali ti accendono e in che ordine; le sviluppiamo.*

---

## 🆕 IDEE NUOVE / INEDITE (Léon: "le altre erano vecchie, proponimene di nuove")
Roba che NON si vede negli altri "ottimizzatori" — sfrutta il fatto che Verdict misura ed è onesto.

- ⭐⭐ **Ghost Tweak (A/B alla cieca su te stesso)** — Verdict applica un tweak SENZA dirti quale,
  tu giochi, lui misura col bench, poi RIVELA se ha aiutato davvero. Doppio-cieco su te stesso →
  uccide il placebo a livello psicologico. Nessun tool al mondo lo fa. È il progetto in una feature.
- ⭐⭐ **Time Machine / "Cos'è cambiato"** — Verdict tiene una baseline del tuo sistema e ti mostra
  una TIMELINE: "da ieri: driver NVIDIA aggiornato, EXPO si è spento, un tweak è saltato, temp +4°C".
  Più un rewind a un punto qualsiasi. Premium + sicurezza, sul journal/scan già esistenti.
- ⭐⭐ **Regression Sentinel** — dopo i tweak, ri-benchmarka da solo ogni tanto e ti AVVISA se le
  prestazioni sono PEGGIORATE (un Windows Update ha rotto qualcosa). Nessuno ti dice quando regredisci.
- ⭐ **Reaction Lab** — minigioco di reflex/aim integrato che misura la TUA latenza+reazione prima/dopo
  i tweak (umano + sistema insieme). Si lega al tuo progetto aim-coach. Divertente + utile + unico.
- ⭐ **GO / Rituale pre-partita** — UN bottone: applica profilo competitive + zittisce Discord/
  notifiche + Non Disturbare + lancia il gioco. "Entra nella zona" in un click. Premium streamlined.
- ⭐ **Evidence dalla community (onesta)** — dati anonimi aggregati: "questo tweak ha aiutato in modo
  MISURABILE il 73% degli utenti con rig simile". Trasforma il motore di onestà in un grado di
  evidenza CROWD-validato. Differenziatore enorme, network effect.
- **Risk Slider** — una manopola safe ↔ estremo; Verdict seleziona automaticamente il set di tweak
  adatto alla tua tolleranza al rischio. UX premium, niente liste infinite.
- **Rig DNA** — un'arte/firma generativa unica creata dal tuo hardware+config esatto (card da
  collezione). La build-sheet diventa un "trading card" figo da collezionare/condividere.
- **Optimization Debt** — come il "debito tecnico": le ottimizzazioni non fatte sono un "debito" da
  ripagare, con un numero che scende mentre sistemi. Framing che dà dipendenza buona.
- **Thermals-aware** — non ti consiglia un tweak di power se le temp sono già al limite (usa temp GPU).
  Consiglio intelligente e sicuro, non a tappeto.

*Le ⭐⭐ sono quelle che secondo me "svoltano" davvero e sono fattibili col motore che abbiamo.*

---

## 🔑 DECISIONE ARCHITETTURALE (Léon, 2026-06-18)
Léon ama TROPPE feature → non possono essere tutte sempre-attive per tutti (non clean).
→ **Pagina "Features" / "Lab" nelle Impostazioni**: ogni feature/modulo premium è un TOGGLE
(on/off). L'utente accende solo ciò che vuole. UI pulita + libreria enorme di feature opzionali.
I moduli pesanti (watchdog, sentinel, overlay) sono off di default. Da costruire come framework
di "feature flags" persistiti in settings.json. TUTTE le idee qui sotto diventano moduli toggle.

## 🆕🆕 IDEE NUOVE — ROUND 3 (Léon ne vuole ancora)
- ⭐⭐ **Placebo Museum** — galleria figa dei tweak-mito sfatati con l'evidenza. "Non ci sono
  cascato." Condivisibile/virale, è l'onestà del progetto resa contenuto.
- ⭐ **Explain my Stutter** — unisce diagnostica DPC/ISR + frame data e ti dice in italiano
  semplice QUALE driver causa lo stutter. Usa dati che abbiamo già.
- ⭐⭐ **Multi-monitor optimizer** — hai 3 monitor: sceglie il primary giusto, VRR per-display,
  spegne quelli inutili per l'input lag (si lega al toggle Samsung che hai già!). Su misura per te.
- ⭐ **Tweak Roulette / Challenge mode** — provi un tweak rischioso, misuri, tieni o annulli:
  un "roguelike" dell'ottimizzazione. Gamification divertente sul motore bench+undo.
- ⭐ **Network Duel** — test ping/jitter/bufferbloat verso i server DEI TUOI giochi, con voto.
- ⭐ **AI co-pilot** — linguaggio naturale: "rendi Valorant più fluido" → Verdict spiega e propone.
- **Trust mode** — mostra ESATTAMENTE cosa toccherà con un diff in stile security-review, per i
  paranoici. On-brand (fiducia totale).
- **Fresh-install score** — confronta col Windows pulito: "hai aggiunto 47 processi dall'installazione".
- **Hotkey profilo** — un tasto globale attiva/disattiva il profilo competitive al volo.
- **Boot impact** — cosa rallenta il boot, impatto delle app di avvio, con disabilitazioni safe.
- **Achievement su guadagni VERI** — "hai migliorato i tuoi 1% low dell'8% (misurato)". Gamification
  ONESTA, premia solo gain reali.
- **Scheduled clean slate** — panic-restore automatico prima dei Windows Update (che spesso rompono).


