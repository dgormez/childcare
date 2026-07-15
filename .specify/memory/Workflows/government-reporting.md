# Workflow: Government Reporting & Compliance

> Added 2026-07-15 by the regulatory research pass. First features: 033–038, 041 (015 and 019
> also live here once started). Source contracts: `docs/integrations/opgroeien/` (README.md maps
> every file to its feature). Facts below were verified against official Opgroeien/FOD documents
> on 2026-07-15 — cite the archived files in specs, do not re-derive from memory.

## Purpose

A licensed KDV does not only serve families — it reports to authorities. This workflow covers
every legally mandated flow between the organisation and Opgroeien / Kind & Gezin, FOD
Financiën, and Zorginspectie, plus the in-house registers those bodies require the KDV to keep.
It is the compliance backbone that the operational workflows (attendance, daily care, billing)
feed.

## Actors

- **Director** — accountable for every submission and register; reviews before anything leaves
  the platform; receives deadline/error alerting.
- **Caregiver** — produces the underlying operational data (attendance, incidents, near-miss
  observations); never submits to authorities directly.
- **Parent** — confirms attendance records (written/electronic, frequency per location); signs
  attests (slaaphouding); receives fiscal attests.
- **System (backend)** — derives submissions from operational data, validates, submits, stores
  immutable audit copies, runs retention clocks.
- **Opgroeien / Kind & Gezin** — receives AARON (kinderopvangtoeslag), FO-SU-05 (IKT
  opvangprestaties), jaarregistraties, crisis meldingen; issues attests and licences.
- **FOD Financiën** — receives fiscal attest 281.86 data via Belcotax-on-web.
- **Zorginspectie** — inspects registers on site (attendance ≥12 months, risk analysis,
  financial reporting for subsidised organisators).

## Sub-flows

### 1. Monthly kinderopvangtoeslag submission (feature 033 — vrije-prijs KDVs)
- Data flow: attendance records (010) → per-child check-in/check-out timestamps → JSON payload
  (`KinderOpvangToeslag.json` contract) → POST /opvangprestaties (bearer token) → response +
  errors stored → director dashboard.
- Deadline: 7th of the month following the care month; unresolved errors past the 8th risk a
  fine — pre-deadline error surfacing is part of the flow, not a nicety.
- Child identity must match the Rijksregister (official name, birthdate, gender); opt-out
  children are omitted from the payload.
- Outputs: submission log (immutable payload copy), error worklist, deadline alerts.

### 2. Monthly IKT opvangprestaties (feature 019 item 4 — IKT/T2 locations, Phase 3)
- Data flow: attendance (010) aggregated per kindcode per month into FO-SU-05 duration buckets
  (min3u/min5u/min11u/min11uFlex × present/justified/unjustified) → XML → email (or announced
  webservice) → returned-with-errors handling.
- "Bestellen is betalen": reserved days count; 6u–20u window; flex-only night care; 90%
  name-matching against the attest inkomenstarief; a child can hold multiple kindcodes.

### 3. Annual jaarregistraties (feature 034)
- Six subsidy-tied XML forms (FO-RE-14/15/19/28/29/30) + the PDF-only medewerkers form
  (applies to ALL organisators; pre-filled by Opgroeien; platform provides a helper report,
  not the submission).

### 4. Fiscal attests (features 015 → 019 item 5)
- Per child per tax year: mandatory federal model 281.86, PDF to parents + digital filing via
  Belcotax-on-web (deadline: end of February following the income year). Amount = actually
  paid parental contributions from invoices (014).

### 5. Safety & compliance registers (features 035, 036, 037, 038, 041)
- Risk analysis across the 4 legal domains + (near-)incident logbook + actielijst checklists +
  slaaphouding attests (035).
- Crisis / grensoverschrijdend gedrag: register → official meldingsfiche pre-fill →
  klantenbeheerder; report-first-complete-later; verontrusting signal log with strictly
  restricted access (036).
- Attendance register compliance: record-at-the-moment audit, parent confirmation, ≥12-month
  retention, Zorginspectie export (037).
- Retention clocks and destruction with audit trail: 10y crisis/complaints, 5y child+staff,
  3y strafregister (destroy previous on new), ≥12m attendance (038).
- Effective-dated BKR rulesets — current regime vs the mandatory 2027 kindratio (041).

## Cross-platform impact

- **Backend**: dominant — derivation queries, validators, submission clients, schedulers,
  immutable audit storage, retention jobs.
- **Director web**: review-before-submit screens, error worklists, deadline dashboards,
  compliance registers, inspection exports.
- **Parent mobile**: attendance confirmation (037), attest visibility (015), signed documents.
- **Caregiver tablet**: only indirect (operational data entry already covered by other
  workflows); sleep-position flag display (035).

## Outputs

- Government submissions with immutable audit copies of exactly what was sent (constitution /
  brief non-negotiable #12).
- Compliance registers ready for on-site inspection.
- Deadline and error alerting for the director.

## Rules of thumb for features in this workflow

- Review-before-submit is the default UX; fully silent auto-submission only where the deadline
  mechanics demand it and the director has been given a veto window.
- Every submission path needs a "returned with errors" loop, not just a happy path.
- Open questions marked "do NOT invent" in the feature prompt blocks go to the product owner
  or software-ontwikkeling@kindengezin.be — never guessed.
