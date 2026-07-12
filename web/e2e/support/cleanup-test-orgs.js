#!/usr/bin/env node
/**
 * Manual maintenance script — NOT run automatically by the test suite. Every E2E run provisions
 * a brand-new Postgres schema per seeded org (seed.ts's seedDirector) and never tears it down,
 * by design: unique orgs mean tests never collide or need a reset step. Left unchecked across
 * many runs this accumulates hundreds of schemas, which measurably slows every tenant-scoped
 * request (this is what caused a real scheduling.spec.ts run to start timing out after ~200
 * schemas had piled up). Run this by hand periodically: `npm run test:e2e:cleanup`.
 */
const { execSync } = require("child_process");

function psql(sql) {
  return execSync(`docker exec childcare-postgres psql -U childcare -d childcaredb -t -c "${sql.replace(/"/g, '\\"')}"`, {
    stdio: "pipe",
  }).toString();
}

const schemas = psql(`SELECT "SchemaName" FROM public.tenants WHERE "Slug" LIKE 'e2e-org-%'`)
  .split("\n")
  .map((line) => line.trim())
  .filter(Boolean);

if (schemas.length === 0) {
  console.log("No e2e-org-* test schemas to clean up.");
  process.exit(0);
}

console.log(`Dropping ${schemas.length} e2e-org-* test schema(s)...`);
for (const schema of schemas) {
  execSync(`docker exec childcare-postgres psql -U childcare -d childcaredb -c "DROP SCHEMA IF EXISTS \\"${schema}\\" CASCADE;"`, {
    stdio: "pipe",
  });
}

execSync(`docker exec childcare-postgres psql -U childcare -d childcaredb -c "DELETE FROM public.tenants WHERE \\"Slug\\" LIKE 'e2e-org-%';"`, {
  stdio: "pipe",
});

console.log("Done.");
