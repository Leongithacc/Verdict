# Verdict vs RapTechPC — nota di ricerca

> Ricerca 2026-07-01 tramite Perplexity. Vedi anche [VS_HONE.md](VS_HONE.md)
> per l'analisi del competitor principale della categoria.
>
> **Sintesi in una riga**: le uniche feature *plausibilmente* attribuibili a
> RapTechPC dai contenuti social sono già interamente coperte da Verdict.
> Nessuna azione da fare, doc archiviato per completezza.

## 1. Cosa abbiamo cercato

- Repository ufficiale GitHub o pagina prodotto canonica di **RapTechPC's
  Free Optimizer Tool** (o "RapTechPC Optimizer" / "RapTechPC Free Tool").
- Account social del progetto (`@raptechpcs` su TikTok, YouTube, Twitter/X)
  e il tipo di contenuti tecnici che pubblica.
- Reviews indipendenti su r/pcgaming, r/pcmasterrace, r/buildapc,
  YouTube tech channels.

## 2. Cosa abbiamo trovato

**Poco che sia tecnicamente verificabile.** Perplexity ha trovato:

- Presenza social su TikTok (`@raptechpcs`) con contenuti orientati a
  "gaming performance profiles", "FPS tracker", "1000 people trusted"
  — tutto marketing/social-proof, nessuna documentazione tecnica.
- Descrizione informale come "open-source Windows 11 and 10 tweaking tool"
  ma **nessun link a repository GitHub verificato**.
- Nessuna review indipendente con protocollo di misura pubblicato.
- Nessun changelog / release note / issue tracker.

**Conseguenza pratica**: non possiamo dire con confidenza cosa RapTechPC
faccia realmente a livello di codice. Tutto quello che segue è **inferenza
prudente** basata sui claim social.

## 3. Feature ipotizzate → stato in Verdict

| Feature RapTechPC (ipotizzata) | Copertura Verdict |
|---|---|
| Gaming performance profiles | ✓ **Già coperto** — `windows-game-mode` KB entry (evidence_strong) + entry power (`ryzen-balanced-power-plan`, `intel-p-core-priority`) + `nvidia-prefer-max-performance` |
| FPS tracker | ✓ **Già coperto** — Ghost Tweak + Measure con PresentMon (ETW) + Mann–Whitney + bootstrap CI + noise gate |
| Windows optimizer generico | ✓ **Già coperto** — 130 entries KB con fonti primarie verificate |
| Registry tweaks non documentati | ✗ **Scartati per policy** — regola d'oro KB: no fonte, no tweak |
| Services / debloat aggressivo | ⚠ **Coperto conservativamente** — `sysmain-superfetch-disable` (documentato), `disable-background-overlays`. Verdict NON fa debloat aggressivo per policy |
| Power plan / Game Mode | ✓ **Già coperto** — 10+ entries |
| Network booster / ping claims | ✗ **Scartati per policy** — `nagle-tcpackfrequency` esiste ma classificato correttamente; niente "network booster" senza baseline |
| Driver-level / kernel tweaks | ✗ **Scartati per policy** — niente kernel driver (regola anti-cheat safe) |
| In-game settings profiles | ✓ **Già coperto** — `OptimizeForGame` per CS2/Valorant/Apex/OW2/Fortnite/TheFinals/R6Siege |
| FPS overlay / injection | ✗ **Scartati per policy** — Verdict usa ETW passivo, mai overlay |

## 4. Cosa portiamo dentro Verdict

**Niente**. Zero feature nuove giustificate dall'analisi.

Il valore del progetto RapTechPC, per come emerge dal profilo social, è
principalmente **marketing e community-building**. Verdict ha già le stesse
capabilities tecniche (spesso con implementazione più rigorosa: Mann–Whitney
vs media aritmetica, source-verification obbligatoria vs claim social).

## 5. Cosa NON portiamo dentro (regole confermate)

- Nessun claim di FPS boost universale senza protocollo misurato.
- Nessun tweak "hidden scheduler settings" senza fonte primaria Microsoft.
- Nessun "network booster" senza baseline jitter/loss reale.
- Nessun servizio essenziale spento per default (rischio stabilità > guadagno).
- Nessuna monetizzazione con feature Premium ad alto rischio (BIOS,
  network tuning) come fa Hone.

## 6. Se RapTechPC pubblicasse un repo, cosa cambierebbe

Se domani `github.com/raptechpc/optimizer` diventasse pubblico con codice
sorgente ispezionabile, potremmo:

- Vedere quali chiavi registry tocca esattamente → verificare se sono
  già in KB o se ne mancano di documentate (Microsoft/AMD/NVIDIA).
- Vedere quali servizi disabilita → confrontare con la nostra scelta
  conservativa e valutare se allargare.
- Vedere il loro FPS tracker → confrontare con PresentMon/ETW (probabilmente
  meno rigoroso di Ghost Tweak, ma la UX potrebbe insegnare qualcosa).

Fino ad allora, questo doc resta archiviato. Se emerge un URL affidabile
del repo, aprire una issue in Verdict con titolo `[research] RapTechPC repo
found` e allegare il link — rifaccio la passata.

## 7. Il posizionamento reso più chiaro

Questa ricerca conferma la tesi di [VS_HONE.md](VS_HONE.md): la categoria
"gaming optimizer for Windows" è dominata da progetti con **forte marketing
e debole documentazione tecnica**. Verdict prende deliberatamente la strada
opposta.

Cita questo doc quando un utente dice "ma RapTechPC promette +30 FPS":
_"Abbiamo cercato. Non abbiamo trovato la documentazione tecnica per validare
questa promessa. Se hai un link, aprimi una issue — la valuto seriamente."_
