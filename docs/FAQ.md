# FAQ

## Generale

### Cos'è Verdict?

Un'app Windows di ottimizzazione gaming **onesta**: misura, rifiuta di
inventare miglioramenti, applica solo tweak con fonte verificata.
Vedi [README.md](../README.md) per i dettagli.

### È gratis?

Sì, 100%. MIT license, sorgente aperto, niente subscription, niente
"premium tier". Se vuoi supportare il progetto vedi
[`.github/FUNDING.yml`](../.github/FUNDING.yml) (se presente).

### Su quale Windows funziona?

Consigliato Windows 11. Windows 10 supportato per la maggior parte delle
feature (alcune UI specifiche di Win11 non appaiono, es. detection Focus
Assist automatico in fullscreen).

### È rischioso?

Ogni scrittura è **journaled + reversibile**. Prima di ogni modifica vedi
un dry-run esatto (before → after). Un "Ripristina tutto" globale rimette
tutti i valori come li ha trovati. Nessun tweak è applicato senza il tuo
consenso esplicito per-cambio.

## Anti-cheat

### È compatibile con Vanguard / EAC / BattlEye?

Sì per design. Verdict:
- Non usa overlay in-game
- Non fa injection nel processo del gioco
- Non installa driver kernel
- Legge frame data solo via ETW (canale passivo Windows, stesso di Intel
  PresentMon)

Non abbiamo garanzie dai vendor anti-cheat: Verdict semplicemente NON
rientra nelle categorie che gli anti-cheat colpiscono.

### Il co-pilota manda dati?

Solo se scegli un brain cloud (Claude / Gemini / GPT) E fornisci **la tua**
API key. Ollama (default) è 100% locale, zero rete.

### La community "V7" manda dati?

Solo se attivi l'opt-in (default OFF) in Impostazioni → "Condividi con
community". Vedi [PRIVACY.md](PRIVACY.md) per cosa manda esattamente
(spoiler: nessuna PII).

## Uso pratico

### Come inizio?

1. Scarica lo zip dell'ultima [release](https://github.com/Leongithacc/Verdict/releases)
2. Estrai in una cartella qualunque
3. Doppio-click su `WPEP.exe` (o `Installa.cmd` per creare il collegamento Start)
4. Pagina "Verdict": clicca **Riscansiona**
5. Guarda cosa dice il verdetto, applica quello che ti convince

### Il verdetto dice "già ottimale" per tutto

Bel PC + Windows recente = non c'è tanto da fare. Verdict è onesto:
non inventerà tweak solo per farti sentire "ottimizzato". Se il Noise
Score è basso, i tweak background probabilmente non produrranno FPS
misurabili — te lo dice esplicitamente.

### Come misuro se un tweak ha davvero funzionato?

Usa **Ghost Tweak** (pagina Misura in Verdict, oppure `wpep bench` +
`wpep compare` da CLI). Fa 5 run baseline + 5 run post con
Mann-Whitney + bootstrap CI. Se il rumore di baseline è troppo alto,
Verdict rifiuta di emettere un verdetto invece di inventarne uno.

### Un tweak non ha "una fonte" che mi convince

Aprilo come [issue](https://github.com/Leongithacc/Verdict/issues) — la
regola d'oro è pubblica: senza fonte primaria non entra nella KB. Se hai
una fonte vendor migliore, proponi PR.

## Cose che spesso si chiedono

### "Ma non c'è il tweak X che ho letto su Reddit / YouTube?"

Se non è nella KB, uno dei due motivi:
1. **Nessuna fonte primaria**: il "tweak" è folklore, forse funzionava su
   Windows 7, oggi non fa nulla o rompe cose. Se hai una fonte vendor
   (Microsoft / NVIDIA / AMD / ...), aprimi issue.
2. **Placebo dimostrato**: alcuni tweak popolari sono in KB come
   `placebo` — cioè li mostro esplicitamente per **dirti che NON funzionano**
   e con quale fonte. Vedi `wpep placebo-museum`.

### "Perché non un'app UWP moderna / Fluent?"

WPF perché:
- Deve funzionare offline
- Deve essere portable (zip + doppio click)
- Deve accedere a registry / powercfg / ETW senza permessi UWP
- Deve girare identica su Win10 e Win11

### "Perché unsigned?"

Certificati code-signing costano centinaia di euro l'anno per un progetto
solo/hobby. Uno self-signed non toglie l'avviso SmartScreen, quindi il
vantaggio pratico è zero. Se decido di distribuire su Microsoft Store un
giorno, quello richiede un accordo separato.

### "Come contribuisco?"

Vedi [CONTRIBUTING.md](../CONTRIBUTING.md). Regola d'oro: **no fonte, no
merge** per la KB.

## Contatti

- Bug / feature: [Issues](https://github.com/Leongithacc/Verdict/issues)
- Discussion: [Discussions](https://github.com/Leongithacc/Verdict/discussions)
  (se abilitate)
- Sicurezza: vedi [SECURITY.md](../SECURITY.md)
