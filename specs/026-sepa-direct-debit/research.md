# Research: SEPA Direct Debit Batch Collection

## R1 — pain.008.001.02 XML generation approach

**Decision**: Hand-generate the XML directly via `System.Xml.Linq` (`XDocument`/`XElement`)
against the ISO20022 `pain.008.001.02` element structure — no third-party SEPA-generation NuGet
package.

**Rationale**: Unlike feature 025's CODA *parsing* problem (a genuinely complex fixed-width
Belgian bank format with real edge cases, where the BACKLOG explicitly required reusing an
existing library), pain.008 *generation* for this feature's actual scope — one `PmtInf` block per
batch (single location, single execution date, single sequence type per instruction) with a flat
list of `DrctDbtTxInf` debit instructions — is a small, well-documented, mechanical tree of
elements. Writing it directly avoids a licensing question entirely (no MIT-licensed .NET SEPA
generator was found; the credible options — `Perrich.SepaWriter`, forks of
`php-sepa-xml`-inspired ports — are unmaintained or GPL) and matches this codebase's existing
posture of hand-rolling well-understood standard formats where a library would add more
integration risk than it removes (e.g., `OgmReferenceGenerator` already hand-implements the
Belgian OGM checksum rather than pulling in a package for it).

**Alternatives considered**: `Perrich.SepaWriter` (MIT) — evaluated and rejected: last released
2019, targets an older pain.008 variant (`001.001.03`, not `001.001.02` used by Belgian banks per
this feature's own requirement), and its typed model does not cleanly expose per-instruction
`SeqTp` (FRST/RCUR, spec.md FR-002a) without fighting the library's assumptions. A from-scratch
GPL/AGPL SEPA library — rejected on the same licensing grounds as CODA generation would have
required, avoided entirely by not depending on one.

## R2 — Schema validation

**Decision**: Validate the generated `XDocument` against the official `pain.008.001.02` XSD
using .NET's built-in `System.Xml.Schema.XmlSchemaSet` + `XDocument.Validate(...)` — no
third-party validator. The schema file itself
(`backend/ChildCare.Infrastructure/Sepa/Schemas/pain.008.001.02.xsd`) is the official SWIFTStandards-
generated ISO20022 schema (byte-identical copies cross-verified from two independent, unrelated
open-source projects that already ship it for the same purpose —
`Dolibarr/dolibarr:test/assets/xsd/pain.008.001.02.xsd` and
`w2c/sepa-sdd-xml-generator:validation_schemes/pain.008.001.02.xsd`), archived in-repo the same
way `docs/integrations/opgroeien/` archives other official third-party schemas this codebase
must conform to exactly. The schema is fully self-contained (no `xs:import`/`xs:include` of
other files), so no further schema files are needed.

**Rationale**: `System.Xml.Schema` is part of the BCL — zero new dependency, and schema
validation is exactly what it exists for. Embedding the real EPC/ISO20022-published schema (not
a hand-written approximation of it) is the only way FR-006's "validate against the EPC schema"
requirement can mean what it says; a hand-rolled structural check would silently miss the exact
class of strictness (element ordering, enumerated codes, field-length facets) the requirement
exists to catch.

**Implementation note**: mirrors `ScribanEmailTemplateRenderer`'s existing embedded-resource
pattern (feature 020) — `<EmbeddedResource Include="Sepa\Schemas\*.xsd" />` in
`ChildCare.Infrastructure.csproj`, loaded once via `GetManifestResourceStream` and cached, not
re-read from disk per batch.

## R3 — Debit instruction sequence type (FRST/RCUR)

**Decision**: Per spec.md's self-answered clarification, compute `SeqTp` per invoice at
generation time: `FRST` if no prior `SepaBatch` has ever included an invoice for that contract
under its *current* `SepaMandateReference`; `RCUR` otherwise. Implemented as a query against
existing `SepaBatch`/`Invoice.SepaBatchId` rows filtered by `Contract.SepaMandateReference` — no
new column needed to track "has this mandate been used before," since the batch history itself
already answers it.

**Rationale**: Avoids a redundant, independently-driftable "first use" flag on `Contract` — the
batch history is already the authoritative record of what has actually been collected, and a
revoke-and-resign naturally resets the answer because it produces a new
`SepaMandateReference` (spec.md FR-011/FR-012), which no prior batch could possibly reference.

**Alternatives considered**: A `Contract.SepaFirstDebitDone` boolean, flipped on first batch
inclusion — rejected as a second source of truth that must be kept in lockstep with the batch
history for no benefit, at this codebase's batch-history query volumes (a handful of batches per
tenant per month).

## R4 — Reusing `IIbanProtector` for the debtor IBAN

**Decision**: Reuse `IIbanProtector` (`ChildCare.Application.Common`, feature 024) and its
existing `"Contract.SepaIban"` purpose string unchanged, to decrypt `Contract.SepaIbanEncrypted`
for XML generation.

**Rationale**: This is the exact same IBAN feature 024 already protects — not a distinct data
class the way feature 025's CODA sender IBAN was (which correctly got its own
`ICodaSenderIbanProtector` purpose string, per that feature's own shipped-note reasoning: a bank
statement counterparty account and a signed mandate's account are different data even when both
happen to be IBANs). Here there is no such distinction — it is literally the same ciphertext
column, read for a new purpose (XML generation) rather than a new *kind* of data. Each decryption
is logged as an access event (spec.md FR-014), following the same access-logging convention 025
established (a structured `ILogger` line, not a persisted audit table — see 025's research.md
R5) rather than inventing a new logging mechanism for this feature alone.

**Alternatives considered**: A separate `ISepaBatchIbanProtector` purpose string — rejected;
would fragment one physical IBAN's ciphertext across two purpose strings for the same logical
data, the opposite of the distinction 025's `ICodaSenderIbanProtector` exists to preserve.

## R5 — Where the pain.008 creditor headers actually come from

**Decision**: No new settings entity (per spec.md's Clarifications). Creditor identifier =
`Tenant.SepaCreditorIdentifier` (024); creditor name = `Tenant.Name`; creditor IBAN =
`Location.BankAccountNumber` (014, already used as the invoice PDF's "pay to this account"
value). Batch generation reads all three at request time; no denormalized copy is stored on
`SepaBatch`.

**Rationale**: Confirmed by reading `Tenant.cs`, `Location.cs`, and every call site of
`SepaCreditorIdentifier`/`BankAccountNumber` — both fields already exist, are already
director-editable (`UpdateOrganisationCommand` for the CID via 024's existing endpoint,
`UpdateLocationInvoiceSettingsCommand` for the bank account via 014's existing endpoint), and are
already exactly the values a pain.008 file's `PmtInf/CdtrAcct`, `GrpHdr/InitgPty`, and `PmtInf/
Cdtr` headers need. Introducing a second settings surface for the same three values, as the
originating BACKLOG description assumed, would create a real drift risk (which copy is
authoritative if a director updates one but not the other) for no benefit.

## R6 — Debtor name for each debit instruction

**Decision**: Use the child's primary contact's name — `ChildContacts` (ordered by `IsPrimary`
descending) joined to `Contacts`, giving `FirstName`/`LastName` — the exact same query
`GenerateInvoicePdfQuery` already runs to populate an invoice's addressee ("Aan") field.

**Rationale**: A pain.008 debtor name (`DrctDbtTxInf/Dbtr/Nm`) must name the account holder
authorizing the debit. Feature 024's mandate signing is a public, unauthenticated link that
captures only signature data, the IBAN, and signing IP (`SubmitContractSigningCommand`) — no
separate signatory-name field exists anywhere, confirmed by reading `Contract.cs` and
`SubmitContractSigningCommand.cs` directly rather than assuming one exists. The primary contact
is the only name this codebase already associates with paying for that child, so it is both the
correct legal debtor identity and the same value a director already sees printed on that family's
invoices — no new field, no new capture step.

**Alternatives considered**: A dedicated `Contract.SepaDebtorName` captured alongside the IBAN at
signing time — rejected as redundant with data already resolvable from the existing primary
contact, and it would risk drifting from the invoice's own addressee name for the same family.
The child's name — rejected; a pain.008 debtor is a legal/banking identity (the paying adult),
never the child, and every other financial-document precedent in this codebase (invoice
addressee, fiscal attestation recipient) already makes the same distinction.
