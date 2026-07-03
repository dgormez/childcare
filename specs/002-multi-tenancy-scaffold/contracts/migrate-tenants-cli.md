# Contract: Tenant Migration Rollout (CLI)

`dotnet run --project backend/ChildCare.Api -- migrate-tenants`

Operator-only CLI subcommand (research.md R8) — not an HTTP endpoint. Satisfies FR-010/FR-011 (User Story 3).

## Invocation

Run from a machine with network access to the deployed database (or locally against Docker Postgres). Requires the same `ConnectionStrings:DefaultConnection` configuration the API itself uses.

```sh
dotnet run --project backend/ChildCare.Api -- migrate-tenants
```

The subcommand is checked at the very top of `Program.cs`'s `args` before the normal web host is built — it does not start the API, does not bind a port, and exits when done.

## Behavior

1. Query `PublicDbContext.Tenants` for every tenant with `ProvisioningStatus == Ready`.
2. For each, build a `TenantDbContext` for that tenant's schema (`ITenantDbContextResolver.ForSchema(tenant.SchemaName)`, research.md R1) and call `context.Database.MigrateAsync()`.
3. A tenant with no pending migrations is a no-op (FR-011) — EF Core's own `__EFMigrationsHistory` table per schema already tracks what's applied; this subcommand does not maintain any separate bookkeeping.
4. Tenants not yet `Ready` (still provisioning, or failed) are skipped — they have no committed baseline to extend yet.

## Output

Prints one line per tenant processed (slug + outcome: migrated / already up to date / failed with the error message), and a final summary count. A single tenant's failure does not stop the run — it's logged and the subcommand continues to the next tenant, then exits non-zero if any tenant failed, so a CI/operator script can detect partial failure.

## Notes

- Not rate-limited or authenticated in the HTTP sense — it's a local/CI-invoked process with database credentials, not a network-exposed endpoint.
- Idempotent by construction (FR-011) — safe to re-run after a partial failure; already-migrated tenants are simply skipped on the next pass.
