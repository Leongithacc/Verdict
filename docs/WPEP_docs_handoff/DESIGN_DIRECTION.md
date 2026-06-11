# WPEP — Design Direction (R7 UI)
*Deciso in chat il 2026-06-11. Salvare nel repo come `docs/DESIGN_DIRECTION.md`.
Quarto file del pacchetto handoff. Integra R7_COPY_AND_KB3.md (copy) e il mockup
`WPEP_UI_Mockup.html` (riferimento di layout/gerarchia, NON di qualità esecutiva).*

## Direzione estetica: "premium gaming × modern devtool"

Dal devtool (VS Code/JetBrains):
- Monospace per TUTTI i dati numerici, ID, verdetti, badge (Cascadia Code, già su Windows 11).
- Allineamenti rigorosi, colonne che si rispettano, niente decorazione gratuita.
- La riga "terminale" a fondo schermata (`$ wpep advise · 2.1s · 0 writes`) resta:
  è identità (la CLI sotto) e garanzia (0 writes).

Dal premium gaming (NVIDIA App):
- Superfici scure profonde, contrasto curato, gerarchia drammatica: il verdetto è GRANDE.
- Micro-transizioni fluide (150ms ease) su hover/selezione. Niente animazioni decorative.
- Sensazione "hardware di fascia alta": solido, denso di qualità, mai giocattoloso.

## Dove si gioca il "super professionale" (requisiti esecutivi, non negoziabili)

1. **Griglia di spaziatura 4px**: ogni margine/padding è multiplo di 4 (8/12/16/24/32).
   Mai valori arbitrari.
2. **Icone vere**: set coerente (Fluent System Icons o Lucide, stroke uniforme).
   MAI simboli unicode/emoji come icone (limite del mockup HTML: non replicarlo).
3. **Raggi coerenti**: una scala sola (es. 6px controlli, 10px card, 12px finestre).
4. **Stati completi** per ogni controllo interattivo: default/hover/pressed/focus/disabled.
   Focus visibile da tastiera ovunque.
5. **Tipografia disciplinata**: scala definita (11/12.5/14/16/20/26), pesi intenzionali,
   mai più di 2 pesi per schermata oltre al regular.
6. **Niente testo che si muove o cambia larghezza**: numeri in monospace tabulare,
   layout stabili durante gli aggiornamenti dati.
7. **Densità**: UN solo layout, eccellente. (Vedi decisione sotto.)

## Theming — DECISIONE: token system dal giorno 1

Architettura: tutti i colori vivono in UN dizionario risorse WPF (`Theme.xaml`),
referenziati ovunque via `DynamicResource`. Nessun colore hardcodato nei controlli. Mai.

Token minimi: `Bg, Surface, Surface2, Line, Accent, AccentDeep, Text, TextMuted,
Ok, OkDim, Info, Warn, Danger, Neutral`.

Selettore tema in Settings: **4-5 preset curati**, non un color picker libero
(i preset curati sono "élite", il picker arcobaleno è amatoriale):
- **Violet** (default, firma): #8B5CF6 / #4A0080
- **Stealth**: quasi monocromo, accento grigio-azzurro freddo
- **Crimson**: rosso scuro desaturato
- **Emerald**: verde tecnico
- (eventuale 5°: definire con dati reali davanti)
Vincolo: i colori SEMANTICI (Ok/Warn/Danger/badge evidenza) NON cambiano col tema —
il significato non si personalizza. Cambia solo l'accento.
Ogni preset va verificato per contrasto WCAG AA su testo e badge.

## Densità — DECISIONE: niente doppio layout in V1

Richiesta originale: toggle minimale/denso in Settings.
Decisione: **respinta per V1**. Motivo: un toggle vero = due versioni di ogni schermata
da progettare, testare e mantenere → raddoppio del lavoro UI e qualità mediocre su
entrambe. NVIDIA App e VS Code non ce l'hanno: hanno UN default eccellente.
Compromesso incluso in V1: opzione "Compact lists" che riduce solo padding verticale
delle righe lista (KB, advise) — banale, un token di spacing.
Densità completa come modalità → backlog V2, da rivalutare con feedback reali.

## Settings screen (nuova, da aggiungere alla nav)

Voci V1 (poche, vere):
- Theme: i preset di cui sopra
- Compact lists: on/off
- Default benchmark runs: 5 (3–10)
- Noise gate threshold: 10% default (avanzato)
- Language: EN (IT eventuale dopo)
- About: versione, licenza MIT, link GitHub, hash release

## Nota per Claude Code

Il mockup HTML è riferimento di LAYOUT e GERARCHIA (cosa va dove, cosa è grande).
La qualità esecutiva deve superarlo, seguendo i requisiti di questo documento.
WPF: check-grid = ItemsControl + UniformGrid; tema = ResourceDictionary swap a runtime.
