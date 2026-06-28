# V7 — Backend community remoto: design

> Mini-spec per il `RemoteBackend` da implementare dietro l'interfaccia esistente
> `WPEP.Execution.ICommunityBackend`. Scritta 2026-06-28 (Claude Code) chiudendo il
> TODO #3 della sezione 4 di `HANDOVER.md`. Quando esiste un endpoint pubblico
> funzionante, basta cambiare `CommunityConfig.Endpoint` e la community remota si
> accende, senza toccare il resto del codice.

## 1. Obiettivo

Trasformare "ha aiutato il X% dei rig simili" da claim teorico a feature funzionante,
senza tradire le regole d'oro di Verdict:
- privacy-first (zero PII, niente account)
- opt-in esplicito (default OFF — `LocalOnlyBackend` resta default)
- nessun nuovo tweak inventato dal server (la KB resta locale e versionata)
- esiti onesti (helped / no-effect / hurt / applied) e niente promesse di FPS

## 2. Cosa NON è (anti-goal)

- NON un account / login / profilo utente
- NON un canale due-vie (server → client manda solo stats aggregati)
- NON personalizzazione "per te" (lo scopo è "rig simili", non identità)
- NON una sorgente di tweak nuovi (KB resta in `kb/tweaks.json`, versionata)
- NON sostituisce la KB: confermando/smentendo esiti misurati di entry esistenti

## 3. Stack scelto

**Cloudflare Workers + D1** (SQLite serverless).

Motivi (vs Supabase / self-host):
1. Stack già usato (`palla8-ai-bot`, `daily-brief-bot`) → zero curva d'apprendimento
2. Free tier abbondante: D1 5GB + Workers 100k req/giorno (sufficiente per migliaia
   di utenti pilot)
3. Latenza globale (edge), niente cold start per l'utente
4. Niente sysadmin (cert TLS, monitoring, backup gestiti da Cloudflare)
5. `wrangler deploy` = single command CI/CD

Alternative valutate:
- **Supabase** (Postgres + REST auto-generata): più potente ma stack nuovo per Léon.
  Riservato a futuro se D1 risulta limitante (improbabile per uso v1)
- **Self-host (Oracle Free / Hetzner)**: massimo controllo, massima manutenzione
  (rinnovi cert, monitoring, backup, patch sicurezza). No

## 4. Architettura

```
┌─────────────┐                  ┌────────────────────┐                ┌─────────┐
│ Verdict.exe │ ──── HTTPS ────▶ │ Cloudflare Workers │ ──── SQL ────▶ │   D1    │
│ (Windows)   │ ◀── JSON Stats ─ │ verdict-community  │ ◀── Results ── │ SQLite  │
└─────────────┘                  └────────────────────┘                └─────────┘
        │                                  │
        ▼                                  ▼
   evidence.json                  ogni notte: Cron Worker
   (sempre locale)                pre-aggrega stats per (tweak_id, rig_tier)
                                  → tabella `stats_cache` → query <50ms
```

Flusso:
1. Utente fa **opt-in** dalla pagina Impostazioni (checkbox, default OFF)
2. **Submit**: dopo ogni `apply --yes` (o batch periodico) il client invia i nuovi
   evidence al server. Idempotente per `(rig_signature, tweak_id, captured_at)`
3. **Query**: la card "Community e prove" della pagina Verdict chiama `GET /v1/stats`
   per ogni tweak visibile. Cache 1h sul CDN Cloudflare

## 5. API REST

URL base — `workers.dev` per pilot (gratis, zero setup), custom domain in v1.2+:

```
https://verdict-community.<account>.workers.dev
```

### POST /v1/evidence

Body:
```json
{
  "records": [
    {
      "rig_signature": "RIG-AB12-CD34",
      "rig_tier": "EPICO",
      "tweak_id": "xmp-expo-enable",
      "outcome": "helped",
      "delta_percent": 7.2,
      "captured_at_iso": "2026-06-28T20:14:00Z"
    }
  ]
}
```

Response: `{"accepted": 1, "duplicate": 0}` (HTTP 200).
Errori: 400 (schema invalido), 429 (rate limit), 5xx (server).

Headers:
- `User-Agent: Verdict/<version>` (no fingerprinting dettagliato)
- `Content-Type: application/json`
- NESSUN cookie, NESSUN auth header — il `rig_signature` è l'identificatore

Idempotency: deduplica server-side su PK `(rig_signature, tweak_id, captured_at_iso)`.
Re-submit dello stesso record → `duplicate++`, niente errore.

Rate limit: max 100 record per POST, max 1 POST/min per `rig_signature`.

### GET /v1/stats?tweak_id=...&rig_tier=...

Response:
```json
{
  "sample_size": 245,
  "helped_percent": 73,
  "no_effect_percent": 22,
  "hurt_percent": 5
}
```

Cache HTTP: `Cache-Control: public, max-age=3600` (1h CDN).
**Sample minimo: 10**. Sotto, risponde HTTP 200 con `sample_size: 0` — mai inventare
statistiche con campioni minuscoli (rispetta "non promettere FPS finti").

## 6. Schema D1 (SQLite)

```sql
-- raw evidence (append-only entro retention)
CREATE TABLE evidence (
  rig_signature  TEXT NOT NULL,
  rig_tier       TEXT NOT NULL,
  tweak_id       TEXT NOT NULL,
  outcome        TEXT NOT NULL CHECK (outcome IN ('helped','no-effect','hurt','applied')),
  delta_percent  REAL,
  captured_at    TEXT NOT NULL,    -- ISO 8601
  received_at    TEXT NOT NULL DEFAULT (datetime('now')),
  PRIMARY KEY (rig_signature, tweak_id, captured_at)
);
CREATE INDEX idx_evidence_tweak_tier ON evidence(tweak_id, rig_tier);
CREATE INDEX idx_evidence_received   ON evidence(received_at);

-- aggregate cache (rebuilt nightly by cron Worker)
CREATE TABLE stats_cache (
  tweak_id            TEXT NOT NULL,
  rig_tier            TEXT NOT NULL,
  sample_size         INTEGER NOT NULL,
  helped_percent      INTEGER NOT NULL,
  no_effect_percent   INTEGER NOT NULL,
  hurt_percent        INTEGER NOT NULL,
  computed_at         TEXT NOT NULL,
  PRIMARY KEY (tweak_id, rig_tier)
);
```

**NESSUNA colonna** IP, user-agent dettagliato, geo, session_id. Per design.

## 7. Privacy model

**Va in rete**:
- `rig_signature` (8 char base32, FNV-1a su hardware canonical — vedi `RigDna.Compute`)
- `rig_tier` (categoria: MITICO / LEGGENDARIO / EPICO / RARO / COMUNE)
- `tweak_id` (id dal catalogo KB)
- `outcome` (enum: helped / no-effect / hurt / applied)
- `delta_percent` (numero misurato dal Ghost Tweak; null per `applied`)
- `captured_at_iso` (timestamp UTC dell'evento)

**NON va in rete**: username, IP (Cloudflare lo riceve a livello HTTP ma il Worker
NON lo persiste su D1), MAC, modello esatto motherboard/CPU/GPU/disco (solo il
**tier aggregato**), path file, configurazione installata, cronologia precedente
all'opt-in.

**Linkability**: il `rig_signature` è deterministico per hardware. Chi *conoscesse*
già la config esatta di un utente (Mobo + CPU + GPU + RAM + primo disco) può
ricalcolare il code e cercarne gli esiti. Il dato esposto è solo "esiti gaming
tweak", non identità → rischio accettabile.

**GDPR**: il `rig_signature` è "pseudonimo" (Art. 4(5) GDPR). Una privacy notice
pubblica (questo file linkato dalla UI) + checkbox opt-in esplicito sono
sufficienti per un'esperienza volontaria privacy-first.

## 8. Opt-in UX (scelto: silent default OFF)

- Default app: `LocalOnlyBackend` → community OFF, niente rete
- Pagina **Impostazioni** → sezione "Community" → checkbox:
  *"Condividi i miei esiti anonimi con la community Verdict"*
  - Sotto: link *"Cosa viene inviato esattamente"* → apre questa sezione 7 nel browser
- Quando flippa ON: il VM istanzia `RemoteBackend(CommunityConfig.Endpoint)`
  invece di `LocalOnlyBackend`. Il prossimo `apply --yes` aggiunge la sync
- Quando flippa OFF: ritorna a LocalOnly. Gli evidence già inviati restano sul
  server (cancellarli sarebbe complesso e poco utile). Il client può inviare un
  soft delete `DELETE /v1/evidence/<rig_signature>` se Léon vorrà aggiungere
  quell'endpoint in futuro
- **Mai prompt invasivi**: chi vuole condividere lo cerca attivamente. Fedele al
  brand "privacy-first", a costo di adoption più bassa

## 9. Rate limiting e abuse

Cloudflare Workers ha rate limiting nativo. Regole proposte:
- `POST /v1/evidence`: 60 req/min per IP, 1 req/min per `rig_signature`
- `GET /v1/stats`: 600 req/min per IP (è cached, leggero)
- Body max 100 KB
- Validation Zod (TypeScript) su schema record:
  - `outcome` in enum
  - `delta_percent` in `[-100, +100]`
  - `rig_signature` regex `^RIG-[0-9A-HJKMNPQRSTVWXYZ]{4}-[0-9A-HJKMNPQRSTVWXYZ]{4}$`
  - `rig_tier` in enum MITICO/LEGGENDARIO/EPICO/RARO/COMUNE

**Anti-Sybil**: il limit per `rig_signature` evita 1000 POST dalla stessa origine
con rig casuali. Chi vuole davvero spammare può però variare `rig_signature` ogni
request. Mitigazioni:
- Sample minimo 10 + bilanciamento honest/dishonest 1:1 → manipolare le percentuali
  richiede sforzo non banale
- Future: Cloudflare Turnstile (PoW captcha invisibile) se si vede abuse reale

## 10. Retention

- `evidence`: **365 giorni rolling**. Job notturno:
  `DELETE FROM evidence WHERE received_at < date('now','-365 days')`
- `stats_cache`: ricostruito ogni notte, di fatto infinito ma derivato

Rationale: esiti vecchi di 1 anno hanno scarso valore (driver/Windows/hardware
sono cambiati). Limit aiuta storage D1 + privacy.

## 11. Deployment runbook (una tantum)

```bash
# 1. Scaffold Worker TypeScript
npm create cloudflare verdict-community --type=worker --ts
cd verdict-community

# 2. Crea D1 database
npx wrangler d1 create verdict-evidence
# annota database_id dall'output, mettilo in wrangler.toml:
#   [[d1_databases]]
#   binding = "DB"
#   database_name = "verdict-evidence"
#   database_id = "..."

# 3. Applica schema
npx wrangler d1 execute verdict-evidence --file=schema.sql

# 4. Implementa src/index.ts (POST evidence, GET stats, scheduled handler per cron)

# 5. Aggiungi cron in wrangler.toml:
#   [triggers]
#   crons = ["0 3 * * *"]   # aggrega stats ogni notte alle 3 UTC

# 6. Deploy
npx wrangler deploy
# → restituisce https://verdict-community.<account>.workers.dev

# 7. Aggiorna WPEP.Execution/Community.cs:
#    CommunityConfig.Endpoint = "https://verdict-community.<account>.workers.dev"

# 8. Bump AppVersion (src/WPEP.Core/AppVersion.cs), build, package, release v1.1.
```

## 12. RemoteBackend.cs (skeleton)

Da aggiungere in `src/WPEP.Execution/Community.cs` accanto a `LocalOnlyBackend`:

```csharp
using System.Net.Http;
using System.Text;
using System.Text.Json;

public sealed class RemoteBackend(string endpoint) : ICommunityBackend
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly string _endpoint = endpoint.TrimEnd('/');

    public string Name => $"Cloudflare Workers ({new Uri(_endpoint).Host})";
    public bool IsConfigured => _endpoint.Length > 0;

    public async Task SubmitAsync(IReadOnlyList<EvidenceRecord> records, CancellationToken ct = default)
    {
        if (records.Count == 0) return;
        var payload = JsonSerializer.Serialize(new { records });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        content.Headers.Add("User-Agent", $"Verdict/{WPEP.Core.AppVersion.Current}");
        using var resp = await Http.PostAsync($"{_endpoint}/v1/evidence", content, ct);
        // 200/202 = OK; 429 = back off; altro = mantieni in queue locale per retry
    }

    public async Task<CommunityStats?> QueryAsync(string tweakId, string rigTier, CancellationToken ct = default)
    {
        var url = $"{_endpoint}/v1/stats?tweak_id={Uri.EscapeDataString(tweakId)}"
                + $"&rig_tier={Uri.EscapeDataString(rigTier)}";
        using var resp = await Http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;
        var body = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var size = root.GetProperty("sample_size").GetInt32();
        if (size == 0) return null;
        return new CommunityStats(
            size,
            root.GetProperty("helped_percent").GetInt32(),
            root.GetProperty("no_effect_percent").GetInt32(),
            root.GetProperty("hurt_percent").GetInt32());
    }
}
```

**Wiring**: aggiungere a `AppSettings` la property `bool CommunityShareEnabled` (default
`false`). Il `MainViewModel` istanzia il backend così:

```csharp
ICommunityBackend backend = (Settings.CommunityShareEnabled && CommunityConfig.IsConfigured)
    ? new RemoteBackend(CommunityConfig.Endpoint)
    : new LocalOnlyBackend();
```

UI: nella pagina Impostazioni, sezione "Community", `CheckBox IsChecked="{Binding
Settings.CommunityShareEnabled}"` + descrizione + link "Cosa viene inviato".

## 13. Decisioni rimandate / aperti

- **Custom domain**: `workers.dev` ok per pilot; quando la community cresce, comprare
  `verdict.dev` o usare CNAME su un dominio Léon. Costo ~10€/anno
- **Pagina pubblica stats** (vetrina): URL pubblico che mostra top-N tweak con più
  sample. Niente PII. Da fare DOPO che il dataset ha almeno 1000 record (oggi: 0,
  prematuro)
- **PoW captcha** anti-spam: solo se si vede abuse reale. Cloudflare Turnstile è
  gratis e plug-and-play
- **Tier granulari**: oggi 5 tier sono sufficienti per `rig_tier`. Segmentazioni
  più fini (es. per GPU vendor) richiederebbero evolvere lo schema D1
- **Submit cadenza**: default proposto = immediato dopo ogni `apply --yes` (1 record
  per POST, semplice). Batch giornaliero sarebbe più efficiente lato rete ma
  complica il codice. Riservato a future ottimizzazione

## 14. Stima del lavoro per la v1.1

| Step | Tempo stimato |
|------|----------------|
| 1. Deploy Cloudflare Worker + D1 + schema | ~1 ora |
| 2. Worker `src/index.ts` (POST + GET + cron) | ~1 ora |
| 3. Sostituire `CommunityConfig.Endpoint` con l'URL workers.dev | 1 minuto |
| 4. Implementare `RemoteBackend.cs` (sez. 12) | ~30 minuti |
| 5. `CommunityShareEnabled` in AppSettings + checkbox UI Settings | ~30 minuti |
| 6. Wiring nel ViewModel + smoke test | ~30 minuti |
| 7. Build, test, package release v1.1 | ~30 minuti |

**Totale ~4 ore di lavoro focalizzato** — fattibile in un pomeriggio quando Léon
torna al PC con SDK e voglia.
