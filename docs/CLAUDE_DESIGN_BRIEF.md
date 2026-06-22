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
