# Verdict — Brief di handoff per Claude Design

> Preparato 2026-06-22. Quando il funzionale è "perfetto", si manda l'app (questi file) a Claude
> Design per la magia visiva. Questo è il contesto completo perché la transizione sia pulita.

## 1. Cos'è Verdict
App desktop Windows (.NET 10 / C# / **WPF**, MVVM) per ottimizzare/tweakkare le prestazioni gaming.
Filosofia, non negoziabile:
- **FA, non INSEGNA.** L'app mostra ciò che applica con un click; la roba manuale/educativa è declassata.
- **Anti-placebo, onestà.** Ogni tweak ha evidenza; niente promesse di FPS finti.
- **Tutto reversibile.** Ogni modifica è journaled, con undo singolo + ripristino totale.
- **Legge lo stato reale** ("già attivo / da attivare"), niente "non rilevabile" dove sa leggere.
- **Anti-cheat safe.** Nessun driver kernel (NVAPI user-mode per il pannello NVIDIA, WMI per l'hardware).
- Voce: **italiano**, diretta, niente yes-man. (Alcune stringhe UI sono ancora in inglese: tradurle in
  questo pass — vedi `docs/NEXT_SESSION.md` finding #4.)

## 2. Identità estetica (la direzione)
Verdict = **villain / carbonio / cockpit da caccia**. Nero assoluto, viola scuro, look aggressivo ma
elegante, intimidatorio. C'è un **concept SVG** mostrato in chat (gauge di prontezza stile cockpit,
toggle viola premium, vitali di sistema) — usarlo come riferimento del *feel*.

## 3. ⚠️ La palette villain ESISTE GIÀ — non reinventarla, ESEGUILA meglio
Il problema NON sono i colori (già giusti), è l'**esecuzione flat/utilitaria**. La sorgente unica dei
colori è `src/WPEP.App/Themes/Theme.xaml`, un sistema a **token** (tutto `DynamicResource`, quindi i
temi si cambiano ridefinendo i brush):

| Token | Colore | Uso |
|---|---|---|
| `Bg` | `#08080C` | sfondo app (quasi nero) |
| `Surface` / `Surface2` | `#14141B` / `#1C1C26` | card / superfici (carbonio) |
| `Line` | `#272735` | bordi sottili |
| `Accent` | `#8B5CF6` | accento viola (SWAPPA coi preset tema) |
| `AccentDeep` | `#4A0080` | viola villain profondo |
| `Text` / `TextMuted` | `#E6E6EC` / `#8A8A96` | testo |
| `Ok`/`OkDim`/`Info`/`Warn`/`Danger`/`Neutral` | verdi/blu/ambra/rosso | SEMANTICI, non cambiano mai |

Type scale: `H1`(26) `H2`(16) `Body`(14) `Muted`(12.5) `Mono`(12.5). Font UI: Segoe UI Variable;
mono: Cascadia. Controlli base: `Card`, `PrimaryButton`, `GhostButton`, `NavButton`, `Badge`.

## 4. ⚠️ Requisito: TEMI PERSONALIZZABILI per-persona
Léon: "il villain mi gasa ma deve essere personalizzabile persona per persona". L'`Accent` già swappa
coi preset (c'è una base di preset incl. "Villain"). Obiettivo design: **temi/preset selezionabili**
(villain come default forte + altri), gestiti dalla pagina **Impostazioni** ridefinendo i token.

## 5. Cosa serve davvero (la magia)
NON i colori — l'**esecuzione premium**:
- Gerarchia, spaziatura, ritmo verticale da prodotto élite (ora è funzionale ma piatto).
- Componenti firma: **gauge di prontezza stile cockpit**, **toggle premium** soddisfacenti (come il
  concept), flusso hero **scan → attiva**, feedback before/after.
- Micro-interazioni/animazioni (entro WPF: transizioni, stati hover/press, accensione toggle).
- Sostituire le **glyph-emoji** (◆ 📈 ⚡ 🎭 …) con un set di icone coerente — sono il tell "amatoriale".
- Tradurre le stringhe inglesi rimaste (Settings/Changes/badge evidenza).

## 6. Le schermate da elevare (nav a sinistra)
Home/Welcome · **Verdict** (consigli, raggruppati per AZIONE: *Da attivare / Già a posto / Manuali / Da
evitare*) · **Scan** (build-sheet inventario componenti: Processore/GPU dedicata+integrata/RAM/Dischi
con VRAM e tipo NVMe, export PNG) · **Applica** (dialog anteprima dry-run) · **Modifiche** (journal undo)
· **Profili** · **Lab** (moduli a toggle, ognuno dice "appare in › pagina") · **Knowledge Base** ·
**Impostazioni** (temi) · **Report** (HTML condivisibile).

## 7. Stato funzionale (STABILE — la struttura non si muove sotto il design)
- Apply: dry-run → journal → write → verify-rileggendo → undo. Asincrono (UI non si blocca).
- Scan: inventario ricco + rilevamento stato reale dei tweak (batch NVAPI in una sessione).
- 28 tweak one-click (registry/powercfg/bcdedit/**nvidia-drs via NVAPI**/dxuser), gate hardware.
- Viste action-first, Lab leggibile, profili (Competitive/Single-player/Daily), engine bonificato da
  una review multi-agente (16 finding, i sostanziali fixati).

## 8. Vincoli tecnici per il design
- **WPF**: i temi sono `ResourceDictionary` di brush `DynamicResource` (vedi Theme.xaml) — ridefinire i
  token per nuovi preset. XAML in `MainWindow.xaml` (un grande file con i DataTemplate per pagina).
- **MVVM**: l'XAML binda ai ViewModel; il testo visibile è in larga parte literal nell'XAML (non
  localizzato) → tradurre in place.
- Niente driver kernel (anti-cheat). Niente dipendenze pesanti nuove senza motivo.
- Léon ha una GPU NVIDIA (RTX 5080) + CPU AMD: il pannello NVIDIA one-click è un differenziatore unico.

## 9. Delta dopo il brief originale (V4 + V5 — pagine in più da elevare)
Il funzionale è cresciuto; il design deve coprire anche queste, restando coerente:
- **Verdict → "Ottimizza per gioco"**: dropdown gioco + due liste (tweak di sistema · impostazioni
  in-game). Titoli supportati: Valorant, CS2, Apex, Overwatch 2, Fortnite, THE FINALS, R6 Siege.
- **Diagnostica → Network Duel**: tabella anchor (target · avg ms · jitter · loss · voto A–F con colore
  semantico). C'è una variante per-gioco (`wpep network <gioco>`).
- **Modifiche → Watchdog**: card con pallino-stato, lista alert, bottone "Controlla ora", bottone
  **"Avvia in background"** + **checkbox "Avvia all'avvio di Windows"** (V5).
- **Tray host (`WPEP.Tray`)**: agente WinForms separato (icona scudo + balloon). **FUORI SCOPE design**
  (è system-tray nativo, non WPF) — al massimo nota a Léon, non toccarlo.

## 10. Manifesto file (cosa toccare, cosa NO)
Repo: `C:\Users\leon0\Projects\WPEP`, progetto `src/WPEP.App`. Build:
`dotnet build WPEP.sln` (deve restare **0 errori, 0 warning** — `TreatWarningsAsErrors`). Test:
`dotnet test WPEP.sln` (deve restare **verde**, 291 test).

| File | Righe | Ruolo | Scope design |
|---|---|---|---|
| `Themes/Theme.xaml` | 159 | **Token colore + type scale + stili base** (Card/PrimaryButton/GhostButton/NavButton/Badge). Sorgente UNICA dei colori. | ⭐ PRIMARIO: qui vivono i temi/preset, le micro-interazioni degli stili, i nuovi controlli firma. |
| `MainWindow.xaml` | 1499 | Sidebar nav + **un DataTemplate per pagina** (Verdict/Scan/Measure/Diagnostics/Kb/Report/Changes/Profiles/Lab/Settings) + Welcome. | ⭐ PRIMARIO: layout, gerarchia, spaziatura, gauge cockpit, sostituzione emoji, traduzioni in place. |
| `Converters.cs` | 53 | `BoolToVis`, `TokenBrush` (string→Brush via DynamicResource), ecc. | ✅ OK aggiungere converter visivi. |
| `App.xaml` | 12 | Merge di `Theme.xaml`. | ✅ OK (es. caricare il preset tema scelto). |
| `MainWindow.xaml.cs` | 138 | Code-behind: navigazione + export PNG build-sheet. | ⚠️ Solo se serve un hook visivo; non cambiare la logica nav/export. |
| `ViewModels.cs` | 801 | MainViewModel + VM (Verdict/Scan/Diagnostics/Kb/Report/Settings) + record di display. Alcune **stringhe IT + divisori `──`** sono qui. | ⚠️ Puoi ritoccare TESTI/etichette visibili e tradurre, MA non cambiare nomi proprietà/binding né logica. |
| `*ViewModel.cs` / `MeasureWizard.cs` / `ApplyFlow.cs` (Scan/Lab/Ghost/Reaction/Profiles/Watchdog) | 98–438 | VM e flussi delle singole pagine; alcune stringhe/emoji qui. | ⚠️ Come sopra: testi/glyph sì, logica no. |
| `Infrastructure.cs` | 196 | `ViewModelBase`, `RelayCommand`, servizi. | ⛔ Non toccare. |
| `TrayAutostart.cs` | 49 | Registro HKCU per autostart tray. | ⛔ Non toccare. |

## 11. Inventario glyph/emoji da sostituire (il "tell amatoriale")
Sostituire con un set di icone coerente (vector path / icon font leggero, no emoji a colori). Posizioni esatte via grep.
- **In `MainWindow.xaml`** (emoji-icona): 🎯 `U+1F3AF` · 🎭 `U+1F3AD` · 📈 `U+1F4C8` · ⚡ `U+26A1` ·
  🛰 `U+1F6F0` · 🏛 `U+1F3DB` · ⏳ `U+23F3` · 🛡 `U+1F6E1`. (+ frecce `→` `U+2192` ×12 in testo: ok tenerle o stilarle.)
- **In `.cs`**: 🎭 (GhostTweakViewModel) da sostituire; `─` `U+2500` ×224 sono **divisori di sezione** negli
  header (es. "── Da attivare ──") → ridisegnarli come separatori veri; `⚠ U+26A0` ×10, `✓ U+2713`, `✗ U+2717`,
  `› U+203A` sono semantici (decidi se iconizzare). `≥ ≈` restano testo.

## 12. Guardrail (regole d'oro per non rompere il funzionale)
1. **Solo visivo.** Cambia XAML (layout/stili/template), `Theme.xaml`, converter, testi/icone. **NON**
   cambiare logica dei ViewModel, nomi di proprietà/`Binding`, comandi, firme, o il comportamento.
2. **Deve sempre compilare 0/0 e passare i test** (`dotnet build WPEP.sln` + `dotnet test WPEP.sln`).
   `TreatWarningsAsErrors` è attivo: niente warning (anche `using` inutili).
3. **DynamicResource per i colori** (mai hard-code di esadecimali nelle pagine): così i temi restano
   swappabili. Nuovi token → in `Theme.xaml`.
4. **Temi personalizzabili**: lascia un meccanismo per cambiare preset da **Impostazioni** (ridefinendo i
   token `Accent`/superfici). Villain come default forte.
5. **Italiano**, voce diretta. Traduci le stringhe inglesi rimaste (Settings/Changes/badge evidenza/Diagnostics).
6. **Niente dipendenze pesanti** nuove senza motivo (anti-cheat/peso). Icone preferibilmente vettoriali in-XAML.
7. Il **tray** (`WPEP.Tray`, WinForms) è fuori scopo: non va ridisegnato qui.

---

## AGGIORNAMENTO 2026-06-26 — NUOVA UI A INTERRUTTORI (richiesta di Léon)

**Cosa cambia:** la lista tweak della pagina **Verdict** (e le proposte del **Co-pilota**) NON usa più
coppie di bottoni "Come fare / Applica". Ora ogni tweak è un **INTERRUTTORE ON/OFF** (stile bxtool ma
più bello e onesto). Léon vuole che TU (Claude Design) renda questi interruttori **super belli, premium,
dettagliati** — è il punto estetico che ha esplicitamente delegato a te.

**Stati da rendere bellissimi** (la logica esiste già, tu fai SOLO il visivo):
- **OFF** (consigliato, non ancora attivo) — interruttore spento, invitante.
- **ON** (applicato da Verdict) — interruttore acceso color accento (viola/rosa Villain), micro-stato
  "applicato · annullabile".
- **ON già-attivo** (Windows ce l'aveva già) — acceso ma con sfumatura diversa (es. verde) + nota.
- **Disabilitato — serve admin** — spento con lucchetto, CTA "riavvia come admin".
- **Manuale/BIOS/placebo** — interruttore disabilitato + info + (fase 2) un **QR code** che apre la
  guida BIOS sul telefono. Rendi bello anche il blocco QR e il badge "placebo/manuale".
- **Busy/in-corso** — micro-animazione durante apply/verify ("sentito").
- In cima: bottone **"Accendi i consigliati"** (master). Rendilo un bell'elemento hero.

**Vincoli invariati:** solo visivo (XAML/Theme), niente logica; build 0/0 + suite verde; la palette
Villain (#4A0080, nero/carbonio) c'è già nei token. Gli interruttori sono `ToggleButton`/Button
templati: dai loro uno stile `x:Key` riutilizzabile in `Themes/Theme.xaml`.

---

## AGGIORNAMENTO 2026-07-01 — 2ª passata Design (dopo la sessione multi-brain + V7 + Hone)

**Cosa è cambiato tra la 1ª passata Design e questa 2ª:**

Tra il 2026-06-26 e il 2026-06-30 sono stati aggiunti 5 componenti UI nuovi che il brief
originale non copriva. La 2ª passata deve gestirli con lo stesso rigore estetico degli
interruttori (1ª passata):

### 1. Card "Rumore di sistema" (nuova, pagina Verdict, sopra la lista)
Fonte funzionale: `ViewModels.cs → VerdictViewModel.ShowNoiseCard`,
`NoiseHeadline/Body/Color/FactorsText`. Colore semantico dal band (Ok/Warn/Danger via
`TokenBrush`). Concept: **cockpit heads-up display** con score 0-100 come gauge
centrale, band label a lato, elenco fattori come "warning lights" in coda. È il primo
elemento che l'utente vede dopo lo scan — deve essere immediatamente leggibile.

### 2. Card "Pronto per Vanguard" (nuova, pagina Verdict, sotto Rumore)
Fonte funzionale: `VerdictViewModel.VanguardHeadline`, `SecureBootOnUi/OffUi`,
`TpmOnUi/OffUi`, `OpenSecureBootGuideCommand`, `OpenTpmGuideCommand`. Due righe di
status con simbolo ✓/⚠ + bottone "Istruzioni BIOS" (quando ⚠). Concept: **caccia
militare tactical display** con 2 gauge/status pill (verde/ambra), il bottone "Istruzioni
BIOS" è discreto ma ovvio.

### 3. Toggle "Vista tecnica / Vista bucket UX" (nuovo, sopra la lista Verdict)
Fonte funzionale: `VerdictViewModel.ShowByTechnical/ShowByBucket`, handler
`OnVerdictViewTechnical/OnVerdictViewBucket`. Attualmente `RadioButton` inline. Concept:
**segmented control premium** stile cockpit (bordo carbon fiber, glow sull'attivo).
Il default va reso ovvio (vista tecnica).

### 4. Card CTA "Gaming Session Mode" (nuova, nested nella card Rumore)
Fonte funzionale: `VerdictViewModel.SessionButtonLabel`, `SessionStatus`,
`ToggleSessionCommand`. Bottone primary + status text a lato. Concept: **pulsante rosso
missile launch** stile arcade (invitante quando OFF, "active" con pulse leggero
quando ON, "restore" quando ON con hover che mostra "farà cosa"). Zero micro-animazioni
distrattive — è azione seria.

### 5. Pagina Co-pilota — 4 RadioButton brain + PasswordBox per 3 cloud brain
Fonte funzionale: `CoPilotViewModel.IsOllama/IsClaude/IsGemini/IsOpenAi`,
`ClaudeApiKey/GeminiApiKey/OpenAiApiKey` + handler code-behind. Attualmente `WrapPanel`
con 4 RadioButton + 4 StackPanel condizionali. Concept: **brain selector premium** come
un bank di switch cockpit (4 toggle rigidi, uno solo può essere on). Ogni pannello brain
ha model TextBox + PasswordBox + check "✓ configurata" — rendere il "✓" visibile ma
non intrusivo (piccola pill verde a lato).

### Priorità della 2ª passata (in ordine)

1. **ToggleButton style refinement**: gli interruttori della lista Verdict (1ª passata)
   funzionano ma sono ancora "utilitari". La 2ª passata li deve rendere **premium
   dettagliati**: profondità (subtle inner shadow), micro-glow accento nell'ON, transizione
   liscia (Storyboard ~150ms), stato "già-attivo" distinguibile (accento diverso, es. Ok).
2. **Card style refinement**: le `Card` sono `Border` con `Background="Surface"` + border
   `Line`. Concept 2ª passata: **carbon fiber texture** subtle (linee diagonali molto
   sottili come Image o LinearGradientBrush), border piu` rifinito (2-color: Line ↑↑ +
   Line più scuro ↓↓ per pseudo-3D).
3. **Icone SVG inline** (Path Geometry) per sostituire glyph/emoji rimasti:
   - ✓ / ⚠ / ✗ nella card Vanguard (Path shield-check, warning-triangle, x-circle)
   - Bottone "Istruzioni BIOS" (Path chip-icon)
   - Vista bucket icons per ogni bucket (FPS/Network/QoL/Background)
   - Brain icons per RadioButton co-pilota (locale = home, cloud = cloud, brand-neutral)
4. **Icone Nav sidebar**: NavButton attualmente text-only. Aggiungere Path glyph a
   sinistra del testo per Verdict / Scan / Diagnostica / Report / Modifiche / Profili /
   Lab / Co-pilota / Impostazioni. Design coerente (line-icon, 1.5px stroke, 20px).

### Cosa NON deve cambiare (invarianti hard)

- **Token `OnAccent`** aggiunto 2026-06-29 (`#0F0F14`) è ora la fonte del testo scuro su
  accent chiaro. Se un tema light diventa "accent giallo", ridefinisce `OnAccent` a
  qualcosa di leggibile (per ora costante).
- **Regola d'oro KB** invariata: il design non tocca i contenuti dei tweak, solo la
  rappresentazione.
- **Anti-cheat safe**: niente overlay, niente componenti Win32 esotici. WPF standard.
- **`Directory.Build.props` con `TreatWarningsAsErrors=true`** valido anche per XAML:
  binding rotti sono fatal.

### Vincoli operativi

- Léon torna al PC col SDK il 2026-07-05. La 2ª passata Design **può essere fatta**
  prima, ma build/test rimangono da fare a casa. Ogni XAML rifinito viene validato
  visivamente solo il 2026-07-05+.
- Se la 2ª passata produce nuovi token semantici (es. per il gauge cockpit servono
  gradients), aggiungerli sotto la sezione `<!-- Extended semantic tokens -->` in
  `Theme.xaml` con commento chiaro sul purpose. NON riusare token esistenti per scopi
  diversi.
- I 10 preset di tema (`ThemePresets.All` in `Infrastructure.cs`) sono già definiti: la
  2ª passata deve funzionare con TUTTI e 10, non solo Villain. Test mentale: il gauge
  cockpit su un tema "Aurora" (chiaro) resta leggibile? Se no, ridefinire con
  `DynamicResource` invece che hardcoded.
