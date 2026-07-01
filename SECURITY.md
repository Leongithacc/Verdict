# Security Policy

## Versioni supportate

Solo l'ultima release pubblicata su
[GitHub Releases](https://github.com/Leongithacc/Verdict/releases) è supportata
per fix di sicurezza. Le v1.0.x ricevono fix solo per critici (RCE, data
corruption, escalation di privilegi non intenzionale).

## Modello di minaccia (in due righe)

Verdict scrive nel registro / powercfg / bcdedit / nvidia-drs / dxuser. Ogni
scrittura è **consent-first per-cambio**, con dry-run visibile prima. Il journal
locale è la fonte di verità per l'undo.

## Superficie non-negoziabile

Le seguenti restano OFF-limits per design:
- Nessun kernel driver, nessun servizio elevato che gira permanente.
- Nessun overlay / injection in-game. Frame data solo via ETW.
- Nessuna telemetria in uscita di default. V7 community è opt-in.
- Nessun binario di terze parti scaricato a runtime. Tutti i tool esterni
  (PresentMon, ecc.) sono pinnati per versione + SHA256.

## Come segnalare una vulnerabilità

**Non** aprire una issue GitHub pubblica per report di sicurezza.

Contatta l'autore via GitHub: [@Leongithacc](https://github.com/Leongithacc)
— apri una draft discussion privata o segnala via email pubblica sul profilo.

Includi nel report:
- Versione Verdict (`wpep version`)
- Descrizione del bug in 3-5 frasi
- Passi minimi per riprodurre
- Se applicabile: impatto (RCE / privilege escalation / data corruption / DoS)
- (Bonus): patch proposta

## Cosa aspettarti

- **Prima risposta**: entro 7 giorni. È un progetto solo/hobby, non SLA.
- **Discussione**: coordinata privata su GitHub.
- **Fix**: la patch viene sviluppata in un branch privato, poi merge + release.
- **Disclosure**: dopo la release fixata, aggiungiamo un `SECURITY_ADVISORY.md`
  con dettagli tecnici e credit al reporter (se vuole).

## Cosa NON è un problema di sicurezza

- "Il tweak X non funziona su Windows Y" → apri una issue normale.
- "L'update-check chiama github.com" → è documentato in
  [PRIVACY.md](docs/PRIVACY.md) e opt-out via impostazione.
- "V7 community manda dati" → è opt-in default OFF, spuntabile in Impostazioni.
- "Il co-pilota cloud invia dati ad Anthropic/Google/OpenAI" → sì, con la TUA
  API key, tu stesso hai accettato i loro TOS. Ollama locale resta default.

## Community backend (verdict-community)

Il repo del backend Worker ha la sua policy: vedi
[Leongithacc/verdict-community/SECURITY.md](https://github.com/Leongithacc/verdict-community).
Report di sicurezza sul backend (rate-limit bypass, injection SQL D1, ecc.)
vanno lì.
