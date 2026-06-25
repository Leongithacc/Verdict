# Pubblicare Verdict su GitHub Releases (per attivare l'auto-update)

L'app è **già cablata** sull'host `github.com/leon007bmx/Verdict`
(`src/WPEP.Core/Update/UpdateCheck.cs` → `UpdateConfig`). Manca solo creare la repo
e pubblicare la release: poi `Controlla aggiornamenti` (in Impostazioni) e
`wpep update-check` diventano attivi **senza toccare altro codice**.

> Verifica al volo dello stato attuale: `wpep update-check`
> Adesso risponde: *"Nessuna release pubblicata ancora (o repo non trovata)"* → corretto.

---

## 1. Crea la repo (1 minuto)
- Vai su **github.com/new**
- **Owner:** `leon007bmx` · **Repository name:** `Verdict`
- **Public** ⚠️ *importante:* l'app legge le release **senza login**. Una repo **privata**
  darebbe 404 e l'update non funzionerebbe. (Public = solo le release sono visibili;
  non sei obbligato a caricarci il codice sorgente.)
- Spunta **"Add a README"** (così esiste un primo commit a cui agganciare la release)
- **Create repository**

## 2. (Opzionale) Carica il codice
Serve **solo** se vuoi anche il sorgente online. Per il *solo* auto-update **NON è necessario**:
basta la repo + la release con lo zip. Se lo vuoi:
```bash
cd /c/Users/leon0/Projects/WPEP
git remote add origin https://github.com/leon007bmx/Verdict.git
git push -u origin main      # (o 'master' se il tuo branch si chiama così)
```

## 3. Genera lo zip da allegare
```bash
cd /c/Users/leon0/Projects/WPEP
bash tools/package-release.sh
# → crea  dist/Verdict-1.0.zip
```

## 4. Crea la Release (2 minuti, dal sito)
- Repo → **Releases** → **"Draft a new release"**
- **Choose a tag:** scrivi `v1.0` → *"Create new tag: v1.0 on publish"* (target: `main`)
- **Release title:** `Verdict v1.0`
- **Trascina** `dist/Verdict-1.0.zip` nella zona *"Attach binaries"* (l'app cerca un
  asset che finisce in `.zip` → sarà il link del pulsante **Scarica**)
- **Publish release**

## 5. Verifica
```bash
wpep update-check
# Atteso ora: "Sei aggiornato (v1.0)."   ← latest = v1.0 = la tua versione
```
E nella GUI: **Impostazioni → Aggiornamenti → Controlla aggiornamenti** → "Sei aggiornato".

---

## Le prossime versioni (come funziona da qui in poi)
1. Alza la versione in **un solo posto:** `src/WPEP.Core/AppVersion.cs` → `Current = "1.1"`
   (aggiorna anche `VER` in `tools/package-release.sh`).
2. `bash tools/package-release.sh` → `dist/Verdict-1.1.zip`
3. Nuova Release con tag `v1.1` + lo zip allegato.
4. Chi ha la v1.0 aprirà Impostazioni → **"Disponibile v1.1"** + pulsante **Scarica**.

### Note tecniche
- Il confronto versioni è **numerico** (`VersionCompare`): `v1.10` > `v1.9`, tollera la `v`
  iniziale e i suffissi tipo `-beta`.
- **Consent-first:** Verdict controlla e dice *dove* scaricare. **Non scarica né installa
  da solo** — apre la pagina/zip nel browser solo se premi *Scarica*.
- Cambiare host in futuro = cambiare `GitHubOwner`/`GitHubRepo` in `UpdateConfig`. Zero altro.
