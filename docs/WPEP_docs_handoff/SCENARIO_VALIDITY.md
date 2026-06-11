# WPEP — Addendum ricerca: validità degli scenari di benchmark
*Ricerca fatta in chat il 2026-06-11. Salvare nel repo come `docs/SCENARIO_VALIDITY.md`.
Integra e CORREGGE la sezione §3 di HANDOFF_R7.md.*

## Scoperta chiave: i replay di Fortnite NON replicano il carico live

Fonte primaria: documentazione ufficiale Unreal Engine (Replay System / DemoNetDriver).

Fatti documentati dall'engine:
1. La riproduzione di un replay ricostruisce il gameplay dai dati di replica tramite
   DemoNetDriver — non è la stessa pipeline di una partita live.
2. In replay il gioco usa un Player Controller diverso (ReplaySpectatorPlayerController);
   rilevanza, culling e prioritizzazione degli attori sono calcolati diversamente:
   in replay "ci si aspetta di vedere tutto", in live molti attori lontani vengono
   esclusi per efficienza e anti-cheat.
3. Il salvataggio dei checkpoint può essere ammortizzato su più frame
   (demo.CheckpointSaveMaxMSPerFrame), con attori campionati da frame diversi.
4. In replay NON c'è netcode attivo verso il server né input processing del giocatore.

URL: docs.unrealengine.com → Replay System / DemoNetDriver and Streamers
(valido per UE4.27 e UE5.x; Fortnite gira su UE5).

## Conseguenza: matrice scenario → categoria di tweak

| Categoria tweak da testare | Replay Fortnite | Creative route fissa | CP2077 benchmark integrato |
|---|---|---|---|
| Impostazioni grafiche/GPU (DLSS, ombre, ecc.) | ✅ ok (relativo) | ✅ ok | ✅ ideale |
| Tweak CPU/scheduling (priorità, parking, ecc.) | ⚠️ dubbio (carico CPU diverso dal live) | ✅ preferito | ✅ ok |
| Tweak rete (throttling, Nagle, ecc.) | ❌ INVALIDO (niente netcode attivo) | ⚠️ parziale (creative ha netcode ma traffico ≠ partita vera) | ❌ n/a (single player) |
| Tweak input (polling, raw input) | ❌ INVALIDO (niente input del giocatore) | ✅ ok | ⚠️ parziale |
| Overlay/background apps | ✅ ok | ✅ ok | ✅ ok |

Regole derivate:
- **Mai usare replay per verdetti su tweak di rete o input.** Il tool/protocollo deve
  rifiutarsi o marcare il verdetto come "scenario non valido per questa categoria".
- I numeri assoluti misurati in replay NON sono confrontabili con quelli live
  (solo confronti relativi within-scenario).
- Per tweak di rete: onestamente, quasi nessuno è misurabile in modo controllato
  lato client (il server e il percorso variano partita per partita). La KB lo dice
  già (grading controversial/placebo): coerente. Non promettere misure di rete.

## Conferma metodologica (AMD GPUOpen, Unreal Engine Performance Guide)

AMD documenta che i run di gioco contengono elementi casuali (es. generatori random
che decidono quanti effetti/particelle spawnano) che da soli alterano i tempi tra
run identici. Senza scenario controllato non si distingue l'effetto di una modifica
dal caso. → Conferma indipendente del nostro approccio noise-floor + MDE.

## Aggiornamento al protocollo §3 di HANDOFF_R7.md

1. CP2077 benchmark integrato (install PULITA) = ambiente di CERTIFICAZIONE della
   pipeline (A/A + known-effect). Invariato.
2. Per Fortnite, scenario di default = **mappa creativa privata, route fissa, 60-90s**
   (non il replay): è l'unico scenario con input vero + rendering live-like.
   Il replay resta utile SOLO per confronti di impostazioni grafiche.
3. Idea per la UI/wizard Misura (R7): quando l'utente sceglie un tweak dalla KB,
   il wizard suggerisce lo scenario adatto in base alla categoria (tabella sopra)
   e BLOCCA le combinazioni invalide (es. tweak rete + replay).
   → aggiungere campo `valid_scenarios` allo schema KB, o derivarlo dalla categoria.

## Domanda aperta nuova
- La voce KB sui tweak di rete dovrebbe dichiarare esplicitamente "non misurabile
  in modo controllato lato client" come parte del testo? (Proposta: sì.)
