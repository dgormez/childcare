# Opgroeien Integration & Regulatory Reference Documents

> Source documents for the ChildCare backlog's government-integration and compliance features.
> Collected & verified 2026-07-15. Intended repo location: `docs/integrations/opgroeien/`.
> Each file below maps to one or more backlog features — see BACKLOG.md for the full context.

## ikt-attendance/ — feature 019 (item 4)

| File | What it is | Source |
|---|---|---|
| `FO-SU-05.xsd` | XML schema v2.3 for the monthly attendance report (opvangprestaties) of IKT/T2 locations. Root `<IKG>`; buckets min3u/min5u/min11u/min11uFlex × present/justified/unjustified. Submitted by email to ko.formulieren@opgroeien.be. | opgroeien.be Organisator → Software (registratie-aanwezigheden.zip) |
| `registratie-aanwezigheden-kinderopvang-beschrijving.pdf` | Official functional description (04/2025): counting rules ("bestellen is betalen"), 6u–20u window, flex/night-care rules, 90% name-matching, multi-kindcode children, submission mechanics. ⚠ Broken text layer — read visually; the extracted rules live in BACKLOG 019 item 4. | idem |

Web-only (not stored here): IKT SOAP cookbook `inkomenstarief-opvangprestaties-kinderopvang-webservices.pdf` and `aanwezigheidsregistratie-kinderopvang-webservice.pdf` — https://www.opgroeien.be/aanbod/kinderopvang/voorzieningen/administratie/organisator#toc-software. WSDL: test `https://tstarws.kindengezin.be/kinderopvang-ikgwebservices/service/ikg.wsdl`, prod `https://arws.kindengezin.be/...`.

## kinderopvangtoeslag-aaron/ — feature 033

| File | What it is | Source |
|---|---|---|
| `KinderOpvangToeslag.json` | **The field-level API contract** (Swagger 2.0, host tstgpappr.kindengezin.be): POST /opvangprestaties, bearer token; locatieId + periode + per-child checkIn/checkOut timestamps. | Swagger UI test env (token via software-ontwikkeling@kindengezin.be) |
| `aaron-sector-handleiding.pdf` | Organisator manual (39 p.): deadline 7th of following month, fines, RRN/bisnummer validation, 2.5y toeslag expiry, opt-out periods, offline mode, location lifecycle. | opgroeien.be (intern) |
| `aaron-handleiding-ouders.pdf` | Parent manual: QR/numeric codes, parent read access, ≤5 contacts per child, Rijksregister-exact identity. | kindengezin.be |

## jaarregistraties/ — feature 034

| File | What it is |
|---|---|
| `FO-RE-14.xsd` | Prestaties inclusieve opvang (per-child DerdeDag/HalveDag/VolleDag) |
| `FO-RE-15.xsd` | Flexibele openingstijden (per-location counts) |
| `FO-RE-19.xsd` | Voorrangsgroepen / IKT (capacity, priority-group detail, KindPlaatsRatio) |
| `FO-RE-28.xsd` | Dringende opvangplaatsen |
| `FO-RE-29.xsd` | Ruimere openingsmomenten |
| `FO-RE-30.xsd` | Kwetsbare gezinnen |
| `jaarregistraties-kinderopvang-brochure.pdf` | Brochure (dec 2024): all six XML forms are subsidy-tied; the **medewerkers registration applies to ALL organisators** but is PDF-only (pre-filled Adobe form, snapshot 1 January, incl. rijksregisternummer per staff member). |

Source: opgroeien.be Organisator page (jaarregistraties-kinderopvang.zip + brochure). Common envelope on all six XSDs: Header (DocumentIndicator, UsedViewer), DocumentGegevens, FormulierGecontroleerd J/N, Config (TargetURL default ko.formulieren@kindengezin.be).

## vendor-certificate/ — features 019/033 onboarding

| File | What it is |
|---|---|
| `contract-certificaataanvraag-softwareleverancier.docx` | Contract (V5.0, 27/10/2021) for the vendor-level KIND&GEZIN certificate on ChildCare's KBO: EU data centre, GDPR, verwerkersovereenkomst with every organisator, Opgroeien audit right, revocation on breach. Signed form → dpo@opgroeien.be. |

Related: X.509 CSR procedure (4096-bit, CN=`CBE=<KBO>KG`) in the IKT cookbook; verwerkersovereenkomst template = `verwerkersovereenkomst-sjabloon.md` / `overeenkomst-verantwoordelijke-verwerker.md` in the Claude-markdown corpus.

## compliance/ — features 035 & 036

| File | What it is |
|---|---|
| `Meldingsfiche crisissituatie Kinderopvang.docx` | Official mandatory-reporting form (26/07/2024) → 036's pre-fill fields (melder, locatie, voorval, kind). Report-first-complete-later workflow. Sent to the klantenbeheerder. |
| `slaaphouding-kinderopvang-attest.docx` | Sleep-position attest model (31/03/2025) → 035: one per child, signed by every contract holder, two declaration checkboxes per signer. |
| `actielijsten/*.docx` | Six of the SEVEN risk-analysis checklists (binnen, omgevingsfactoren, kwaliteitsvol handelen, ziekte, slapen, verzorging) → 035's checklist seed content. Structure: per question "Hoe zorg je daar nu voor?" / "Kan de aanpak nog verbeteren? Hoe en wanneer?". ⚠ MISSING: the "buiten" (outdoor) list — https://www.opgroeien.be/sites/default/files/documenten/risicoanalyse-kinderopvang-actielijst-buiten.docx |

Source: opgroeien.be → Kwaliteit → Veiligheid page.

## document-models/ — feature 024

| File | What it is |
|---|---|
| `huishoudelijkreglement-model.docx` | Official baby's & peuters huishoudelijk reglement model (16/02/2026). Mandatory rubrics in black, optional guidance in red — mirror this fixed/editable split in the template engine. |
| `schriftelijkeovereenkomst-model.docx` | Official baby's & peuters schriftelijke overeenkomst model (15/04/2025). Includes the "wijziging huishoudelijk reglement in het nadeel van de contracthouder" clause (termination right) → reglement version + acknowledgment tracking. |
| `schoolkind-variants/*` | Same models for school-age childcare — wrong segment for Phase 1, kept for future reference. |

Source: opgroeien.be → Administratie → Ouders en kinderen.

## regulation/

| File | What it is |
|---|---|
| `kindratio-kinderopvang_special.pdf` | The kindratio special (upd. 25/06/2024) → feature 041: new mandatory ratio from 01/01/2027 (baby-only ≤12m 1:5, >12m 1:8, mixed 1:7, rest max 14, group max 18; gezinsopvang 4/quarter, max 7), transition rules, subsidy 60/40, countable staffing profiles. ⚠ Broken text layer — read visually. |
| `kinderopvang-faq.html` | Opgroeien kinderopvang FAQ snapshot — incl. the "attest inkomenstarief aanvragen voor een ouder" step-by-step (019). |

## Related external references (not stored here)

- **Belcotax-on-web / fiscaal attest 281.86** (features 015, 019 item 5): https://financien.belgium.be/nl/E-services/Belcotaxonweb/technische-documentatie/attest-kinderopvang — deeper material in the Claude-markdown corpus: `161-presentatie-bow-2-20250526.md`, `161-belcotax-bow-mappage-fiche-01-voorbeeld-nl.md`, `protocol-ogr-vutg-fiscale-attesten.md`.
- **Opgroeien vendor contact** for all integrations: software-ontwikkeling@opgroeien.be / @kindengezin.be.

## Open questions (answers pending — do not invent)

1. AARON production-token onboarding procedure (033).
2. AARON resubmission semantics: does re-POSTing locatieId+periode replace? (033)
3. FO-SU-05: webservice path vs email submission (019 item 4).
4. Jaarregistraties: form-to-subsidy mapping + per-form deadlines (034).
5. PSP choice Stripe/Mollie/POM (014).
6. Reglement-acknowledgment: extend 024 or separate feature.
