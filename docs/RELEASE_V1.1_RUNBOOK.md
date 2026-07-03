# Release runbook — v1.1 (e successive)

> Per la PRIMA release (`v1.0`) → vedi `RELEASE_GITHUB.md`. Quel runbook crea repo,
> attiva Pages e pubblica la primissima release.
>
> Per le release SUCCESSIVE — `v1.1`, `v1.2`, ecc. — usa questo.
> Scritto 2026-06-28 quando v1.0 è già live e c'è una pila di commit non spediti.

Tempo totale stimato: **20–30 minuti** (la maggior parte è il build).

---

## ⚡ Modalità rapida (release automatica via GitHub Actions)

Dal 2026-06-29 esiste `.github/workflows/release.yml` che fa build + test +
package + GitHub release **automaticamente** ogni volta che pusho un tag `v*`.
Se preferisci il flusso semplice, fai SOLO i passi 1–2–5 di sotto (bump version,
commit, tag+push) e salta i passi 3–4–6: ci pensa la pipeline.

```powershell
# Da locale, dopo aver bumpato AppVersion.cs + package-release.sh:
git add src/WPEP.Core/AppVersion.cs tools/package-release.sh
git commit -m "release: bump v1.1"
git tag -a v1.1 -m "Verdict v1.1"
git push origin main
git push origin v1.1   # ← questo trigger il workflow
```

Poi guardi su https://github.com/Leongithacc/Verdict/actions: in ~5 minuti la
release appare in https://github.com/Leongithacc/Verdict/releases con lo zip
allegato. Il workflow **verifica** che la versione nel tag combaci con
`AppVersion.Current` E con `VER` di `package-release.sh` — se sono fuori sync,
fallisce subito invece di pubblicare una release rotta.

I passi sotto restano validi per release **manuali** (es. se vuoi controllare
ogni step, o se le Actions sono temporaneamente fuori uso).

---

## 0. Prerequisiti (verifica una volta)

```powershell
# .NET 10 SDK installato:
dotnet --version
# Deve uscire qualcosa tipo "10.0.xxx". Se "not recognized", installa da
# https://dotnet.microsoft.com/download/dotnet/10.0

# Git Bash disponibile (per tools/package-release.sh che usa bash + bsdtar):
& 'C:\Program Files\Git\bin\bash.exe' --version

# GitHub CLI autenticato (per il tag + release):
gh auth status
# Se non logged in: gh auth login --web
```

Tutto OK? Procedi.

---

## 1. Sincronizza il repo locale

```powershell
Set-Location C:\Users\leon0\Projects\WPEP
git status                            # working tree deve essere clean
git pull --rebase origin main         # niente sorprese remote
git push origin main                  # spedisci eventuali commit locali ahead
```

Se hai commit locali pendenti (es. dal pacchetto handover di Claude del 2026-06-28),
li pushi qui PRIMA del bump versione. Verifica:

```powershell
git log --oneline origin/main..main
# Deve dire 'niente' (zero righe). Se mostra commit, ri-pusha.
```

---

## 2. Bump versione (2 file da tenere allineati)

**File A** — `src/WPEP.Core/AppVersion.cs`:
```csharp
public const string Current = "1.1";   // ← era "1.0"
```

**File B** — `tools/package-release.sh`, riga 7:
```bash
VER="1.1"   # keep in sync with src/WPEP.Core/AppVersion.cs  (AppVersion.Current)
```

I due **devono** combaciare. `AppVersion.Current` è la fonte di verità che la GUI,
la tray, la CLI e l'`UpdateCheck` leggono; il `VER` dello script è il nome dello zip
(`dist/Verdict-1.1.zip`) che diventerà l'asset della release GitHub.

```powershell
# Sanity check del bump:
Select-String -Path src/WPEP.Core/AppVersion.cs -Pattern 'Current = "1.1"'
Select-String -Path tools/package-release.sh -Pattern 'VER="1.1"'
# Entrambe devono ritornare la riga matchata.
```

---

## 3. Pre-flight build + test

Killare i processi che lockano le DLL (vincolo build .NET su Windows):

```powershell
taskkill /F /IM WPEP.exe /IM wpep-tray.exe /IM MSBuild.exe /IM VBCSCompiler.exe 2>$null
```

Build:

```powershell
dotnet build WPEP.sln -c Release -m:1 --disable-build-servers -v q
# Deve uscire: 0 Warning(s), 0 Error(s). 'TreatWarningsAsErrors=true' è ON
# repo-wide, quindi qualunque warning rompe la build.
```

Test piena:

```powershell
dotnet test WPEP.sln -c Release -m:1 --disable-build-servers -v q
# Atteso: ~360 passing (base 339 + 9 smoke CoPilot per Claude/Gemini/OpenAI
# + 5 smoke SessionMode + 6 smoke SystemSnapshot aggiunti tra il 2026-06-28 e
# il 2026-07-01). Il numero esatto varia; l'importante è 0 failing.
```

Smoke CLI:

```powershell
.\src\WPEP.Cli\bin\Release\net10.0-windows\win-x64\wpep.exe version
# Deve dire: Verdict v1.1
.\src\WPEP.Cli\bin\Release\net10.0-windows\win-x64\wpep.exe doctor
# Deve fare lo scan e finire pulito.
```

Smoke GUI (manuale, 30 secondi):

```powershell
dotnet run --project src/WPEP.App
# Apre la GUI. Click su 'Riscansiona' nella pagina Verdict. Verifica:
# - footer / titolo dice "Verdict v1.1"
# - card "Pronto per Vanguard" appare con stato corretto (Secure Boot + TPM 2.0)
# - card "Rumore di sistema" mostra il gauge cockpit con needle e Noise Score
# - toggle vista Bucket/Tecnica funziona nella pagina Verdict
# - pagina Co-pilota mostra il selettore 4-way (Ollama/Claude/Gemini/GPT)
# - pagina Impostazioni ha la checkbox "Condividi con community" (V7 opt-in)
# Chiudi la GUI.
```

Se qualcosa fallisce qui, **STOP**: fixa e riparti dal punto 3. Non taggare codice rotto.

### 3b. Visual QA del design pass cieco (il vero motivo per non taggare prima del 5/7)

I commit della 2ª passata Design sono stati scritti SENZA mai lanciare la GUI (SDK
non disponibile fino al 5/7). Compilano e i 399 test passano, ma i binding WPF
falliscono a runtime in SILENZIO (non crashano, semplicemente non renderizzano).

**Revisione statica già fatta (2026-07-03, Fable 5) — tutto ciò che si può verificare
a tavolino è OK:**
- `NoiseAngle` esiste sul VM (0°→180°) → target del binding del needle presente.
- Le chiavi colore `C.*` dei `DynamicResource` (gauge, gradient Card, MissileButton)
  sono definite staticamente in `Theme.xaml:7-25` → i gradienti risolvono già al load,
  non restano trasparenti. `ThemePresets.Apply` gira nel ctor di `SettingsViewModel`.
- `MissileButton`/`GhostButton` (`BasedOn PrimaryButton`) e le icone Path
  (`BasedOn IconBase`) hanno la base definita PRIMA nel dizionario → risolvono.
- I `RelativeSource AncestorType=RadioButton/ItemsControl` puntano ad antenati reali.

**Quindi resta solo da controllare a OCCHIO (nessuno di questi è un crash):**
1. **Gauge Noise Score** — il needle deve puntare all'altezza giusta (score 0 = tutto
   a sinistra, 100 = tutto a destra) e ruotare quando lo score cambia.
   ✅ **Già corretto** (commit 2026-07-03): il needle aveva SIA `RenderTransformOrigin`
   SIA `RotateTransform CenterX/CenterY` — in WPF si sommano e sfalsano il perno. Tolto
   il `RenderTransformOrigin`, resta il solo centro (70,80). Verifica solo che il perno
   sia alla base del needle (centro del semicerchio), non altrove.
2. **Card con gradient + drop-shadow** — le Card devono avere il gradiente viola
   (Surface→Surface2), non un fondo piatto o trasparente.
3. **Switch premium** (toggle) — l'animazione Storyboard del knob deve scorrere, non
   saltare; il glow deve apparire su ON.
4. **Icone Nav sidebar** — le 11 icone Path devono colorarsi con lo state
   (selezionata = accent, non selezionata = muted), non restare tutte grigie.
5. **MissileButton** (Session CTA) — deve avere il gradiente accent→rosso.
6. **Icone ✓ ⚠ ✕** nelle liste — devono essere le Path SVG, non i caratteri emoji.

Se 1-6 sono ok visivamente, il design pass è validato e puoi taggare. Se qualcosa
è solo brutto (non rotto), valuta se bloccare o rimandare a v1.1.1.

---

## 4. Package portatile

```powershell
# package-release.sh richiede Git Bash + bsdtar (tar.exe nativo di Windows 10+).
& 'C:\Program Files\Git\bin\bash.exe' tools/package-release.sh
# Output: dist/Verdict-1.1.zip
```

Verifica zip:

```powershell
Get-ChildItem dist/Verdict-1.1.zip | Select Name, Length
# Atteso: dimensione ~80-120 MB (varia con .NET runtime).
# Apri lo zip e verifica al volo che contenga:
#  - Verdict-1.1/app/WPEP.exe
#  - Verdict-1.1/app/wpep-tray.exe
#  - Verdict-1.1/Installa.cmd
```

---

## 5. Commit del bump + tag + push

```powershell
git add src/WPEP.Core/AppVersion.cs tools/package-release.sh
git commit -m "release: bump v1.1"

# Tag annotato:
git tag -a v1.1 -m "Verdict v1.1"

# Pusha sia commit che tag:
git push origin main
git push origin v1.1
```

**Importante**: il tag va pushato esplicitamente. Senza `git push origin v1.1`,
il tag resta solo locale e la release GitHub non può puntarci.

---

## 6. Pubblica la release su GitHub

Le release notes complete sono in [`docs/RELEASE_NOTES_v1.1.md`](RELEASE_NOTES_v1.1.md)
— usale come body della release invece di riscriverle a mano:

```powershell
gh release create v1.1 dist/Verdict-1.1.zip `
  -R Leongithacc/Verdict `
  -t "Verdict v1.1" `
  -F docs/RELEASE_NOTES_v1.1.md
```

`-F` legge il body dal file. Modifica prima `docs/RELEASE_NOTES_v1.1.md` se serve
aggiustare qualcosa (es. rimuovere il preambolo "> Testo destinato al body..." che è
meta-commento non necessario sulla pagina).

Per rigenerare il diff dei commit inclusi in questa release:

```powershell
git log v1.0..v1.1 --oneline
```

In alternativa, dalla UI: GitHub repo → Releases → Draft new release → tag `v1.1`,
trascina lo zip, scrivi le notes, Publish.

---

## 7. Smoke check post-deploy

```powershell
# 1) L'API GitHub vede la release?
gh release view v1.1 -R Leongithacc/Verdict
# Deve mostrare title, tag, asset 'Verdict-1.1.zip'.

# 2) L'app v1.0 attualmente installata rileva l'aggiornamento?
&"$env:LOCALAPPDATA\Verdict\WPEP.exe"
# Apri Impostazioni → Controlla aggiornamenti.
# Deve dire: "Disponibile: Verdict v1.1 — scarica."

# 3) (Solo se vuoi testare l'auto-update reale): clicca scarica, segui flusso,
#    poi 'verdict' in Win+R → titolo dice v1.1.
```

Se l'app NON rileva la nuova versione:
- Verifica che la release sia **published**, non draft (`gh release view`)
- Verifica che lo zip allegato finisca in `.zip` (UpdateChecker cerca quel suffisso)
- Aspetta 1-2 minuti (cache CDN di GitHub)

---

## 8. Rideploy GitHub Pages (auto, ma sappi che succede)

Se il commit di bump (o uno qualunque incluso) ha toccato `site/**`, il workflow
`pages.yml` parte automaticamente al push del main. Verifica:

```powershell
gh run list -R Leongithacc/Verdict --workflow=pages.yml --limit 1
# Deve mostrare l'ultimo run con status: completed, conclusion: success
```

Sito pubblico: https://leongithacc.github.io/Verdict/

---

## 9. Annuncio (opzionale)

Se vuoi avvisare utenti / amici della v1.1:
- Edit del README sulla repo per puntare a v1.1
- Post su social / Discord / forum gaming italiani
- Update di `docs/HANDOVER.md` se cambia qualcosa di strutturale

---

## 10. Rollback (se v1.1 ha un bug critico)

```powershell
# Marca la release come pre-release (nasconde dal "latest" usato dall'auto-update):
gh release edit v1.1 -R Leongithacc/Verdict --prerelease

# L'auto-update tornerà a vedere v1.0 come latest. Gli utenti di v1.1 restano
# su v1.1 (non c'è downgrade automatico), ma i nuovi installati prendono v1.0.

# Quando hai una v1.1.1 con il fix, ripubblicala come stable:
gh release edit v1.1 -R Leongithacc/Verdict --prerelease=false
# (oppure cancella v1.1: gh release delete v1.1 -R Leongithacc/Verdict --cleanup-tag)
```

---

## 11. Bumping cheatsheet (per le release future)

| File | Cosa cambiare | Esempio v1.1 → v1.2 |
|------|---------------|----------------------|
| `src/WPEP.Core/AppVersion.cs` | `public const string Current` | `"1.1"` → `"1.2"` |
| `tools/package-release.sh` | `VER="..."` riga 7 | `"1.1"` → `"1.2"` |
| Tag git | `git tag -a v1.X -m "Verdict v1.X"` | `v1.1` → `v1.2` |
| GitHub release | `gh release create v1.X dist/Verdict-1.X.zip` | idem |

Tutto il resto è derivato. Niente da bumpare in CHANGELOG (per ora non esiste) o
in `BiosGuide.cs` / KB (la versione non è scritta lì).

---

## 12. Quando NON usare questo runbook

- **Hotfix d'urgenza al sito GitHub Pages senza release app**: ti basta editare
  `site/*.html`, commit + push. Il workflow `pages.yml` ridepoya senza altro.
- **Solo doc/HANDOVER updates**: niente bump, niente release. Commit + push.
- **Refactor interni che non cambiano comportamento utente**: aspetta di accumulare
  abbastanza modifiche prima di farne una v1.x.
