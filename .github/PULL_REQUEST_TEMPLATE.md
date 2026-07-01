## Cosa cambia questo PR

<!-- 1-3 frasi sul "cosa" del PR. -->

## Perché

<!-- Link a issue se esiste (#123), oppure il "perché" in chiaro. -->

## Tipo di modifica

- [ ] Bug fix (non breaking)
- [ ] Nuova feature (non breaking)
- [ ] Breaking change (rompe API/UI/CLI esistente)
- [ ] Solo documentazione
- [ ] Refactor / cleanup interno (zero impatto utente)

## Checklist autore

- [ ] `dotnet build WPEP.sln -c Release -m:1 --disable-build-servers -v q` → 0 warning / 0 errori
- [ ] `dotnet test WPEP.sln -c Release -m:1 --disable-build-servers -v q` → tutti i test verdi
- [ ] Se ho toccato la KB: la nuova entry ha una **fonte primaria** verificata (regola d'oro)
- [ ] Se ho toccato un comando CLI: ho aggiornato anche il `PrintUsage` in `Program.cs`
- [ ] Se ho toccato la GUI: ho aggiunto/aggiornato il `Raise(nameof(...))` per le property bind-ate
- [ ] Se ho aggiunto un **nuovo gioco**: ho toccato tutti i 6 punti di sync elencati in [CONTRIBUTING.md](../CONTRIBUTING.md) ("Adding a game")
- [ ] Se ho aggiunto un **nuovo brain co-pilota**: ho aggiornato ICoPilotBrain, DefaultXxxModel, CoPilotViewModel.BuildService, AppSettings, CLI subcommand, e [docs/BRAINS.md](../docs/BRAINS.md)
- [ ] CHANGELOG.md aggiornato sotto `[Unreleased]` se la modifica è user-visible

## Note per il review

<!-- Cose che vuoi che il reviewer guardi con attenzione, o decisioni di design da discutere. -->
