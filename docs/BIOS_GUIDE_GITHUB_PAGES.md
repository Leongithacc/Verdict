# Pubblicare le guide BIOS su GitHub Pages (per il QR del telefono)

L'app mostra un **QR** sui tweak manuali-BIOS (XMP/EXPO, Resizable BAR, aggiornamento BIOS, PBO).
Lo scansioni col telefono e si apre la guida **passo-passo per la tua marca di scheda madre**, in
italiano o inglese — raggiungibile dal telefono anche col PC nel BIOS (perché è una pagina online).

> Tutto il contenuto è già pronto nel repo, nella cartella `site/` (`index.html` + `bios.html`).
> Devi solo accendere GitHub Pages **una volta**.

## Setup (una volta sola, ~2 minuti)
1. Assicurati che la repo **`leon007bmx/Verdict`** esista e sia **Public** (serve già per l'auto-update —
   vedi `docs/RELEASE_GITHUB.md`).
2. Carica il codice se non l'hai già fatto:
   ```bash
   cd /c/Users/leon0/Projects/WPEP
   git push        # su 'main'
   ```
3. Sul sito GitHub: repo **Verdict** → **Settings** → **Pages**.
4. Alla voce **Build and deployment → Source**, scegli **GitHub Actions**.
   (Non serve scegliere cartelle: il workflow `.github/workflows/pages.yml` pubblica `site/` da solo.)
5. Fatto. Al primo push il workflow gira; dopo ~1 minuto il sito è online a:
   **https://leon007bmx.github.io/Verdict/**

## Verifica
- Apri sul telefono: `https://leon007bmx.github.io/Verdict/bios.html?t=xmp-expo-enable&v=asus`
  → deve mostrare i passi ASUS per attivare EXPO/XMP, con switch lingua IT/EN e selettore marca.
- Nell'app: i tweak manuali-BIOS mostrano il QR che punta a questo indirizzo, già con la TUA marca
  rilevata (ASUS) e la lingua.

## Aggiornare/aggiungere una guida in futuro
- Tutto il testo vive in `site/bios.html` (oggetto `T` in fondo, ben commentato). Modifica/aggiungi,
  fai `git push`: il workflow ripubblica da solo. Niente da toccare nell'app finché gli `id` combaciano
  con quelli della Knowledge Base.

## Note
- I percorsi dei menu sono **verificati per marca** (ASUS/MSI/Gigabyte/ASRock) e riportano l'onesto
  "può variare per modello/versione". Niente passi inventati per scheda esatta — in BIOS si va sul sicuro.
- Se un domani cambi host/utente, l'URL base è in **un solo punto** lato app
  (`WPEP.Core/Bios/BiosGuide.cs`, costante `SiteBaseUrl`) e in `pages.yml`/questo runbook.
