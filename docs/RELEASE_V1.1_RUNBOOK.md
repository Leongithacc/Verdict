# Release runbook — v1.1 (e successive)

> Per la PRIMA release (`v1.0`) → vedi `RELEASE_GITHUB.md`. Quel runbook crea repo,
> attiva Pages e pubblica la primissima release.
>
> Per le release SUCCESSIVE — `v1.1`, `v1.2`, ecc. — usa questo.
> Scritto 2026-06-28 quando v1.0 è già live e c'è una pila di commit non spediti.

Tempo totale stimato: **20–30 minuti** (la maggior parte è il build).

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
# Atteso: 342 / 342 passing (era 339 + 3 smoke ClaudeBrain aggiunti il 2026-06-28).
# Se cambia, aggiorna questo numero qui per le release successive.
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
# - card "Pronto per Vanguard" appare con stato corretto (✓/⚠ Secure Boot + TPM)
# - pagina Co-pilota mostra il selettore Ollama/Claude
# Chiudi la GUI.
```

Se qualcosa fallisce qui, **STOP**: fixa e riparti dal punto 3. Non taggare codice rotto.

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

Una sola riga con `gh`:

```powershell
gh release create v1.1 dist/Verdict-1.1.zip `
  -R Leongithacc/Verdict `
  -t "Verdict v1.1" `
  -n "Cosa cambia in v1.1:`n- ClaudeBrain (co-pilota cloud opzionale)`n- 5 nuovi tweak BIOS guidati (Secure Boot, TPM, Above 4G, CSM, Virtualization)`n- Card 'Pronto per Vanguard' sulla pagina Verdict`n- 3 nuovi tweak (Intel undervolt, Win Update active hours, Focus Assist fullscreen)`n- Auto-update porta i precedenti da v1.0 a v1.1 automaticamente."
```

Riempi le release notes con quello che è cambiato davvero dal `v1.0..v1.1`. Per generare
il diff dei commit:

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
