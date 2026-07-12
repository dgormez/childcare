import { test, expect } from "./support/fixtures";
import { seedLocation, seedGroup, pairDevice } from "./support/seed";

// Pairing itself happens on-device from the caregiver kiosk app (using a director-provided PIN),
// not from this screen — web only lists and revokes already-paired devices, so seeding uses the
// same pairDevice() helper attendance.spec.ts uses (see KNOWN_GAPS.md's staff-invitations note
// for the general pattern of acting as the actor a flow actually needs).
test.describe("devices", () => {
  test("a freshly registered org shows the empty state", async ({ directorPage }) => {
    await directorPage.goto("/devices");
    await expect(directorPage.getByText("No devices have been paired yet.")).toBeVisible();
  });

  test("a paired device is listed as active and can be revoked", async ({ director, directorPage }) => {
    const location = await seedLocation(director);
    const group = await seedGroup(director, location.id);
    await pairDevice(director, location.id, group.id);

    await directorPage.goto("/devices");
    const row = directorPage.locator("tbody tr").filter({ hasText: location.name });
    await expect(row.getByText("Active", { exact: true })).toBeVisible();

    await row.getByRole("button", { name: "Revoke" }).click();
    await expect(directorPage.getByText("Revoke device")).toBeVisible();
    await directorPage.getByRole("button", { name: "Revoke", exact: true }).last().click();

    await expect(row.getByText("Revoked", { exact: true })).toBeVisible();
    await expect(row.getByRole("button", { name: "Revoke" })).not.toBeVisible();
  });

  test("a failed list load shows an error state with a working retry", async ({ directorPage }) => {
    await directorPage.route("**/api/devices", (route) =>
      route.fulfill({ status: 500, contentType: "application/json", body: "{}" }),
    );

    await directorPage.goto("/devices");
    await expect(directorPage.getByText("Couldn't load devices. Please try again.")).toBeVisible();

    await directorPage.unroute("**/api/devices");
    await directorPage.getByRole("button", { name: "Retry" }).click();
    await expect(directorPage.getByText("No devices have been paired yet.")).toBeVisible();
  });
});
