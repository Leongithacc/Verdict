# AI Co-pilot — guida ai 4 cervelli

> Verdict ha 4 cervelli AI swappable dietro l'interfaccia `ICoPilotBrain`
> (`src/WPEP.Advisor/CoPilot/`). Nessuno dei quattro può inventare tweak:
> gli id che non esistono nel catalogo KB vengono scartati **nel codice**
> (`CoPilotGrounding.ParseReply`), non solo "chiesti gentilmente" nel prompt.
> Questa è la "regola d'oro" del co-pilota. Vedi anche
> [docs/HANDOVER.md sez. 3](HANDOVER.md) per l'architettura completa.

## Confronto rapido

| Brain | Locale | Costo | Privacy | Qualità output | Latenza |
|---|---|---|---|---|---|
| **Ollama** (default) | Sì | Zero | Massima (offline) | Buona con `qwen2.5vl:32b` | ~5-15s |
| **Claude** (Anthropic) | No | ~$0.003/query (Sonnet) | TLS, key DPAPI a riposo | Eccellente | ~2-5s |
| **Gemini** (Google) | No | ~$0.001/query (2.5 Pro) | TLS, key DPAPI a riposo | Molto buona | ~2-4s |
| **GPT** (OpenAI) | No | ~$0.005/query (GPT-5) | TLS, key DPAPI a riposo | Eccellente | ~3-6s |

Costi indicativi per una richiesta media co-pilota (catalogo Verdict + domanda
utente ~2000 token in, risposta ~500 token out). Le percentuali cambiano;
controlla sempre il pricing ufficiale.

## Quando scegliere quale

### Ollama (default) — usa quando

- **Privacy totale**: nessun dato lascia il PC, mai. Ottimo per PC di lavoro, PC
  di gaming con anti-cheat, PC condivisi.
- **Free forever**: nessuna API key, nessun costo.
- **Sperimentare senza pensieri**: puoi chiedere qualunque cosa senza pagare.

Prerequisito: [ollama.com](https://ollama.com) installato + un modello pullato.
Léon usa `qwen2.5vl:32b` sulla RTX 5080 (settabile nella pagina Co-pilota).

Limiti: qualità del ragionamento dipende dal modello pullato. Modelli piccoli
(`qwen2.5:7b`) sono veloci ma meno accurati sui tweak complessi. `qwen2.5vl:32b`
è il compromesso di Léon.

### Claude (Anthropic) — usa quando

- **Ragionamento su tradeoff**: domande tipo "quale tweak conviene per un Ryzen
  7800X3D con RTX 5080 su Warzone?" — Claude ragiona meglio sui vincoli
  hardware-specifici.
- **Spiegazioni lunghe e coerenti**: quando vuoi capire *perché* un tweak
  funziona, non solo *quale* applicare.
- **Default consigliato per cloud**: modello `claude-sonnet-4-6` è il migliore
  compromesso qualità/costo per il caso d'uso Verdict.

API key: [console.anthropic.com](https://console.anthropic.com) → Settings → API Keys.
Env var: `ANTHROPIC_API_KEY` (CLI) o inseriscila nella pagina Co-pilota (GUI,
cifrata DPAPI a riposo).

Alternative modello: `claude-sonnet-5` (top-tier bilanciato, uscito dopo il
default 4.6), `claude-opus-4-8` (più costoso, qualità marginale superiore per
ragionamento complesso), `claude-haiku-4-5-20251001` (più economico, buono per
query semplici).

### Gemini (Google) — usa quando

- **Prezzo minimo tra i cloud**: `gemini-2.5-pro` è il più economico dei 3
  cloud brain.
- **Contesti lunghi**: Gemini gestisce molto bene 1M+ token in input; utile se
  in futuro Verdict aggiungerà passaggio di intere sessioni benchmark come
  contesto.

API key: [aistudio.google.com](https://aistudio.google.com) → Get API key.
Env var: `GEMINI_API_KEY`.

Alternative modello: `gemini-2.5-flash` (più economico, buono per query rapide).

Nota: Gemini API richiede accettazione dei termini Google (residenza dati EU
disponibile ma potrebbe richiedere piano Vertex AI a pagamento).

### GPT (OpenAI) — usa quando

- **Familiarità**: se hai già API key OpenAI da altri progetti.
- **Ecosistema tools**: se vuoi integrare eventuali function calling futuri di
  Verdict con lo stack OpenAI Assistants.

API key: [platform.openai.com](https://platform.openai.com) → API keys.
Env var: `OPENAI_API_KEY`.

Alternative modello: `gpt-4o-2024-08-06` (più economico, buono), `gpt-4o-mini`
(molto economico, adeguato per query semplici).

Nota: `gpt-5` è il default nella config Verdict; se OpenAI non l'ha ancora
rilasciato, cambia in `gpt-4o-2024-08-06` dalla pagina Co-pilota.

## Esempi di prompt effettivi

Il co-pilota accetta domande in linguaggio naturale. Il grounding aggiunge il
catalogo Verdict compresso in background, e la risposta cita SOLO tweak
esistenti (gli inventati vengono scartati).

Buone domande:

- "Rendi Valorant più fluido su questo PC"
- "Ho stutter random dopo 30 minuti di CS2. Cosa controllo?"
- "Quali tweak da applicare per Warzone su Ryzen 7800X3D?"
- "Il mio setup è già ottimizzato o c'è ancora qualcosa da fare?"
- "Voglio ridurre input lag. Non toccare cose rischiose."

Domande che ricevono risposte cortesi ma non actionable:

- "Overclokka la mia CPU al massimo" (Verdict non fa overclock automatico,
  suggerirà PBO / Curve Optimizer come guida manuale)
- "Guadagno 50 FPS?" (nessun tweak promette percentuali garantite)

## Come switchare da CLI

```powershell
# Ollama (default, no key)
wpep copilot "rendi Valorant piu fluido"

# Claude (env var):
$env:ANTHROPIC_API_KEY = 'sk-ant-...'
wpep copilot "rendi Valorant piu fluido" --brain claude

# Gemini (--api-key inline):
wpep copilot "..." --brain gemini --api-key '...'

# GPT:
wpep copilot "..." --brain openai --api-key 'sk-...'
```

## Come switchare da GUI

Pagina Co-pilota → riga RadioButton "Cervello: Ollama · Claude · Gemini · GPT".
Quando selezioni un brain cloud, appare il pannello con:
- Nome del modello (editabile per puntare a varianti diverse)
- PasswordBox per l'API key (nascosta, cifrata DPAPI a riposo)
- Indicatore "✓ configurata" quando la chiave è salvata

Il co-pilota ricorda l'ultimo brain scelto tra un avvio e l'altro (persistito in
`AppSettings.CoPilotBrain`).

## Faccio quello che voglio con le API key?

Sì: sono le TUE chiavi, salvate SOLO sul tuo PC (cifrate DPAPI per l'utente
Windows corrente). Verdict non le manda mai a nessun server proprio. Se sposti
il file `%LOCALAPPDATA%\Verdict\data\settings.json` su un altro utente/PC,
DPAPI non riesce a decifrarle e vengono trattate come "non configurate"
(comportamento voluto).

## Cost management

Vuoi tenere sotto controllo la spesa cloud? 3 opzioni:

1. **Usa Ollama** come default e passa a un cloud solo quando la risposta locale
   non ti convince. Verdict ricorda il brain, ma cambiarlo è istantaneo.
2. **Rate-limit lato provider**: tutti i provider cloud hanno spending cap nel
   loro dashboard. Impostalo a $5-10/mese e non c'è modo di andare oltre.
3. **Modelli economici**: `claude-haiku-4-5-20251001`, `gemini-2.5-flash`,
   `gpt-4o-mini` costano molto meno dei modelli flagship. Sono adeguati per il
   caso d'uso Verdict che è text-in text-out breve.

Verdict non tracka la tua spesa cloud — è la tua chiave, il tuo dashboard,
la tua fattura.
