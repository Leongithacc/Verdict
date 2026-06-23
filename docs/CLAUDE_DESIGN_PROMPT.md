# Prompt per Claude Design — pass estetico di "Verdict"

> Da incollare in una sessione di Claude Design **con il repo `C:\Users\leon0\Projects\WPEP` aperto**.
> Il testo qui sotto (tra le righe ───) è il prompt; sopra/sotto sono note per Léon.

───────────────────────────────────────────────────────────────────────────

Sei **Claude Design**. Devi fare un **pass estetico premium** su un'app desktop Windows **WPF (MVVM)**
già FUNZIONANTE chiamata **Verdict** (engine "WPEP"): un tool gaming che applica tweak di performance,
misura before/after ed è onesto/anti-placebo. Il codice e la logica sono finiti e vanno **lasciati
intatti**: il tuo lavoro è **solo l'esecuzione visiva**.

**Leggi prima questi due file** (sono il tuo brief completo):
- `docs/CLAUDE_DESIGN_BRIEF.md` — cos'è l'app, l'identità estetica, il manifesto-file (cosa toccare e
  cosa no), l'inventario delle emoji da sostituire, le schermate da elevare e i guardrail.
- `src/WPEP.App/Themes/Theme.xaml` — la sorgente UNICA dei colori: un sistema a **token** `DynamicResource`.

## Identità (la direzione)
**Villain / carbonio / cockpit da caccia.** Nero assoluto, viola scuro (`#4A0080` / `#8B5CF6`), look
aggressivo ma elegante, intimidatorio. ⚠️ **La palette esiste già** in `Theme.xaml` ed è giusta — il
problema NON sono i colori, è l'**esecuzione flat/utilitaria**. Devi alzarne il livello, non reinventarla.

## Cosa voglio davvero (la magia, non i colori)
1. **Gerarchia, spaziatura, ritmo verticale** da prodotto élite (ora è funzionale ma piatto).
2. **Componenti firma**: una **gauge di prontezza stile cockpit**, **toggle premium** soddisfacenti,
   il flusso hero **scan → attiva**, feedback before/after.
3. **Micro-interazioni** entro WPF: transizioni, stati hover/press, accensione dei toggle.
4. **Sostituire le emoji-glyph** (🎯 📈 ⚡ 🎭 🛡 🛰 🏛 ⏳ …) con un **set di icone coerente** (vettoriali
   in-XAML, niente emoji a colori): sono il "tell amatoriale".
5. **Temi personalizzabili**: lascia un meccanismo per scegliere un preset da **Impostazioni**
   (ridefinendo i token), con **Villain come default**.
6. **Traduci in italiano** le stringhe inglesi rimaste (Settings, Changes/Modifiche, Diagnostics,
   badge evidenza).

## Schermate da elevare (nav a sinistra)
Home/Welcome · **Verdict** (consigli raggruppati per AZIONE + "Ottimizza per gioco") · **Scan**
(build-sheet componenti, export PNG) · **Applica** (dialog anteprima) · **Modifiche** (journal undo +
Watchdog) · **Profili** · **Lab** (moduli a toggle) · **Knowledge Base** · **Diagnostica** (incl.
Network Duel) · **Impostazioni** (temi) · **Report**.

## Regole d'oro — NON rompere il funzionale (vincolanti)
- **Solo visivo.** Tocca XAML (layout/stili/template), `Theme.xaml`, converter, testi/icone. **NON**
  cambiare logica dei ViewModel, nomi di proprietà/`Binding`, comandi o comportamenti.
- **Deve sempre compilare e passare i test**: `dotnet build WPEP.sln` → **0 errori, 0 warning**
  (`TreatWarningsAsErrors` attivo!); `dotnet test WPEP.sln` → **verde (291 test)**. Verifica alla fine.
- **Colori solo via `DynamicResource`** (mai esadecimali hard-coded nelle pagine): i temi devono restare
  swappabili. Nuovi token in `Theme.xaml`.
- **Niente dipendenze pesanti** nuove (anti-cheat / peso). Icone preferibilmente vettoriali in-XAML.
- Il **tray** (`WPEP.Tray`, WinForms) è **fuori scopo**: non ridisegnarlo.

## Modo di lavorare
Procedi a tappe e fammi vedere i progressi: prima i **token/temi + stili base** in `Theme.xaml`, poi
le **schermate una a una** (parti da Verdict e Scan, le più viste). Chiedimi un parere sul look quando
una schermata-chiave è pronta. Alla fine: build 0/0 + test verdi + un riassunto di cosa hai cambiato.

L'obiettivo: da "sembra fatta da un bambino" a **un'arma élite** che fa venire voglia di aprirla.

───────────────────────────────────────────────────────────────────────────

## Note per Léon (non incollare)
- Apri Claude Design **dentro la cartella del progetto** (`C:\Users\leon0\Projects\WPEP`) così ha i file.
- Tutto il dettaglio (manifesto-file, glyph con codepoint, guardrail) è in `docs/CLAUDE_DESIGN_BRIEF.md`:
  il prompt lo richiama, non serve incollarlo.
- Se Claude Design propone un look, **sei tu il giudice**: è il pezzo dove serve il tuo occhio.
