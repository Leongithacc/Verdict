# WPEP — Execution Engine (V2) — Spec della destinazione
*Deciso in chat il 2026-06-11. Salvare nel repo come `docs/EXECUTION_ENGINE_V2.md`.
Settimo file del pacchetto. NON si implementa ora: definisce la destinazione del
prodotto finito e i vincoli che la V1 deve già rispettare per renderla possibile.*

## 0. Visione confermata (decisione del proprietario del progetto)

Il prodotto FINITO scansiona il sistema, decide cosa serve, e **applica le modifiche
scelte dall'utente** — poi le ri-misura e le sa annullare. La V1 advisory è una fase,
non la destinazione. Ordine vincolante: prima la misura si dimostra affidabile
(pipeline certificata, A/A test, noise gate), poi il tool tocca i sistemi.

## 1. Gate di attivazione (quando V2 può iniziare)

L'Execution Engine si costruisce SOLO quando tutti questi sono veri:
1. Pipeline statistica certificata (validate A/A + known-effect passati su CP2077).
2. V1 usata su almeno 2 macchine reali diverse (desktop top + laptop debole).
3. Repo open source pubblico (il codice che scrive sul sistema DEVE essere leggibile:
   è l'unica risposta seria al problema "app AI-built non auditabile dall'utente").
4. Catalogo failure modes V1 gestito e stabile (EDGE_CASES_AND_TRUST).

## 2. Principi non negoziabili dell'esecuzione

1. **Niente pulsante "ottimizza tutto".** L'utente seleziona dalle raccomandazioni;
   il tool può proporre una selezione ("Apply all strong-evidence items"), mai
   applicare ciecamente l'intera KB. I tweak `risky` richiedono conferma singola
   con il testo del rischio; i `placebo` non sono applicabili affatto (che senso
   avrebbe?).
2. **Dry-run obbligatorio prima di ogni applicazione**: schermata che mostra le
   modifiche ESATTE (chiave registry → valore prima → valore dopo; comando; file)
   prima del consenso. Nessuna scrittura "a sorpresa".
3. **Transazione per-tweak**: snapshot del valore precedente PRIMA di ogni scrittura,
   salvato in un undo-journal locale (`data/journal/`). Ogni applicazione è
   reversibile singolarmente, in ordine inverso, anche a distanza di settimane.
4. **Restore point di sistema** creato prima di ogni sessione di applicazione
   (cintura + bretelle: journal granulare + rete di sicurezza Windows).
5. **Verify-after-write**: dopo ogni modifica, rilettura e conferma del nuovo stato.
   Scrittura fallita o stato incoerente → stop della sessione, journal aggiornato,
   report all'utente. Mai continuare su uno stato incerto.
6. **Misura come prova, non come opzione**: dopo l'applicazione il tool propone
   subito il flusso compare (baseline già in archivio). Il claim del prodotto resta
   "ti dimostro l'effetto", anche quando applica lui.
7. **Separazione delle modalità**: Advisor/Measure/Diagnostics restano read-only
   sempre. L'Execution è una modalità esplicita, visivamente distinta (la riga
   terminale passa da "0 writes" a "N writes · journaled"), con elevazione admin
   solo lì. La garanzia leave-no-trace si aggiorna onestamente: "non lascia tracce
   TRANNE le modifiche che TU hai approvato, tutte elencate e reversibili."
8. **Scope delle scritture limitato alla KB**: l'engine sa applicare solo voci della
   KB con campo `apply` definito (tipo di operazione, valori, verify, undo). Niente
   esecuzione arbitraria, niente script liberi. Ogni nuova capacità di scrittura
   passa per una nuova entry KB con fonte.

## 3. Estensione schema KB (da predisporre GIÀ in V1)

Aggiungere campi opzionali alle voci (null in V1, compilati in V2):
```jsonc
"apply": {
  "method": "registry|powercfg|bcdedit|service|gui-only",
  "operations": [ { "path": "...", "value_after": "...", "verify": "..." } ],
  "requires_reboot": false,
  "undo": "auto-journal|manual-steps",
  "gui_only_reason": null   // alcune cose si fanno SOLO a mano (es. impostazioni in-game)
}
```
Nota: molte voci resteranno `gui-only` per sempre (impostazioni dentro ai giochi,
BIOS, driver panel) — il tool finito sarà comunque ibrido applica+guida. Onestà
anche qui: non promettere automazione dove non può esistere.

## 4. Implicazioni architetturali per la V1 (da fare ORA)

- Lo schema KB include già il campo `apply` (nullable) — costo zero, evita migrazione.
- `SystemSnapshot` già salvato con ogni run (fatto, F10) — diventerà anche la base
  del confronto pre/post applicazione.
- La UI tiene lo spazio concettuale: la card di un tweak "Worth doing" ha il bottone
  "How to" in V1; in V2 lo stesso spazio ospita "Apply". Nessun redesign necessario.
- Il journal format si definisce in V2, ma la cartella `data/` portable è già pronta.

## 5. Fuori scope anche in V2
- Overlay in-game (decisione anti-cheat permanente).
- Scritture fuori KB / script arbitrari.
- "Profili aggressivi" senza evidenza — la grammatica del tool resta l'evidenza.
- Auto-apply senza interazione (modalità silenziosa/pianificata): mai.
