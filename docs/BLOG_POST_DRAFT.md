# Draft blog post — Verdict v1.1 lancio

> Bozza per HN "Show HN" / DEV.to / Medium / r/pcgaming. Non pubblicare senza
> revisione. Data target di pubblicazione: dopo il tag v1.1 (post 2026-07-05).
> Rimuovere questo blocco quotato prima di pubblicare.

---

## Building an honest gaming optimizer

Windows gaming "tweak" tools tend to fall in one of two buckets:

1. Marketing-first apps that promise "+30% FPS" and ship 50 tweaks in a
   one-click preset.
2. Nostalgic collections of registry hacks from Windows 7 forums, most of which
   either do nothing on modern Windows or were placebos to begin with.

I wanted a third thing: **an optimizer that tells you when to stop**.

That's Verdict. Open source (MIT). Portable. It refuses to invent improvements,
refuses to apply tweaks without a vendor source, and refuses to emit a verdict
if the measurement noise is too high.

### The golden rule

Every tweak in the knowledge base needs a **primary vendor source** (Microsoft
Learn, NVIDIA, AMD, Intel, Riot, publisher docs). A YouTuber saying it worked
for them isn't a source. A Reddit thread with 500 upvotes isn't a source.

If I can't find a primary source, the tweak either doesn't go in — or, if it's a
well-known myth, it goes in as `evidence_level: placebo` with a source that
**debunks it**. That's the Placebo Museum: 14 tweaks that everyone quotes,
demonstrably shown to do nothing (or worse) on modern Windows.

### Statistical rigor is not optional

Every performance claim goes through a Mann-Whitney U test + bootstrap
confidence interval. The unit of observation is the run — never pooled frames.
Pooling frames makes any microscopic delta look "significant"; it's the oldest
statistical dishonesty in the benchmark world.

There's also a **noise gate**: if the baseline's minimum detectable effect
(MDE) exceeds a threshold, Verdict refuses to emit a verdict instead of
inventing one. Three possible outcomes: real effect, no measurable effect, no
verdict. The last one is not a failure — it's the tool being honest.

### System Noise Score

Inspired (honestly, credited in the docs) by Hone's marketing insight that
"background tweaks help more on noisy systems", Verdict computes an explicit
Noise Score (0-100) from documented factors: startup apps count, indexing
state, SysMain, Game DVR, transparency effects.

When your Noise Score is low, Verdict tells you explicitly:

> "The background tweaks in the list below probably won't produce measurable FPS
> gains on this system. Don't apply them automatically."

This is the opposite of "apply everything, guaranteed boost". It's the tool
actively working against the placebo response.

### 4-brain AI copilot

Natural language: "make Valorant smoother". The response is grounded on the
verified catalog — **invented tweak IDs are dropped at the code level**, not
just "please don't hallucinate" in the prompt.

Four brains, swappable:
- **Ollama** (local, default, 100% offline)
- **Anthropic Claude**
- **Google Gemini**
- **OpenAI GPT**

Cloud API keys are your keys, stored DPAPI-encrypted locally, never sent to any
server of mine. Ollama is the default because privacy > cloud quality on
principle.

### Anti-cheat safe by design

No overlay. No injection. No kernel driver. Frame data comes from Windows ETW —
the same passive channel Intel PresentMon uses. Verdict simply doesn't belong
to a category anti-cheat systems target.

I can't get vendor guarantees from Vanguard/EAC/BE. What I can do is be
architecturally in a place where they don't need to care.

### Community evidence, privacy-first

There's a Cloudflare Worker + D1 backend (open source, MIT) that receives
**opt-in** anonymized outcomes: rig signature hash (8 chars, FNV-1a of hardware
canonical), tier, tweak id, outcome, delta%, timestamp. No PII, no IP, no user
agent, no geo. Default OFF in the client.

The public leaderboard shows top tweaks with **minimum 10 samples per row**.
Below the threshold, no percentage is shown — respecting the "no fake numbers"
rule.

### What Verdict is not

- Not a game trainer.
- Not an OSD (no in-game overlay by policy).
- Not a GPU overclocker (MSI Afterburner exists).
- Not a "clean my registry" tool (CCleaner et al exist).
- Not an anti-cheat bypass.

### Why open source

Because the credibility asset needs to be verifiable. If I ship a closed-source
tool that claims to only apply source-backed tweaks, you have to trust me. Open
source, you can grep the code.

### Try it

- GitHub: [github.com/Leongithacc/Verdict](https://github.com/Leongithacc/Verdict)
- Backend Worker: [github.com/Leongithacc/verdict-community](https://github.com/Leongithacc/verdict-community)
- BIOS guide (mobile-friendly): [leongithacc.github.io/Verdict](https://leongithacc.github.io/Verdict/)

Portable ZIP release, no installer needed. Windows 10/11.

### Feedback welcome

If you find a tweak I got wrong, or a source that's stale, or a competitor I
should evaluate — open an issue with the fact and the source. That's the whole
protocol.

---

**Word count**: ~600. Tighten to 400 for Show HN, expand to 900 for DEV.to.

## Target notes per canale

### Show HN (Hacker News)
- Titolo suggerito: `Show HN: Verdict – an honest Windows gaming optimizer with a "no verdict" mode`
- Post text: max 400 parole. Tagliare "System Noise Score" e "4-brain copilot",
  focus su golden rule + statistical rigor + noise gate.
- Prima frase: hook forte tipo *"Every Windows gaming optimizer promises +30%
  FPS. Verdict is the first that will actively refuse to give you a number."*

### DEV.to
- Titolo: `I built an honest gaming optimizer — here's how the statistical noise gate works`
- Formato: markdown standard, code snippet delle formule MW+bootstrap.
- Tag: `gaming`, `dotnet`, `csharp`, `wpf`, `statistics`, `opensource`

### Reddit r/pcmasterrace
- Titolo: `Made a gaming optimizer that tells you when NOT to optimize [OC, open source]`
- Focus community-friendly, meno statistica, più "onestà brand".
- Includere screenshot Verdict page (da fare al ritorno al PC).

### Medium
- Titolo: `The optimizer that tells you when to stop optimizing`
- Long-form ~1500 parole con tutti i dettagli tecnici.

## Da fare quando torni al PC
- [ ] Screenshot Verdict page (con Vanguard card + Noise Score gauge)
- [ ] Screenshot Copilot page (con 4-brain selector)
- [ ] Screenshot Changes page (journal + undo)
- [ ] Video demo 60 secondi (script separato)
- [ ] Revisione manuale di questo draft, poi pubblicazione
