import { test, expect } from "./support/fixtures";
import { seedLocation } from "./support/seed";

// No "Add Location" UI exists yet (see KNOWN_GAPS.md) — locations are seeded directly through
// the API. These specs cover what the UI does expose: the list, the detail/edit "General" tab,
// and the loading/error/empty states. The "Reservation Settings" tab is excluded — it's
// actively in-progress work elsewhere in the tree (spec 013f) uncommitted at time of writing.
test.describe("locations", () => {
  test("a freshly registered org shows the empty state", async ({ directorPage }) => {
    await directorPage.goto("/locations");
    await expect(directorPage.getByText("No locations yet.")).toBeVisible();
  });

  test("clicking a location in the list opens its detail page", async ({ director, directorPage }) => {
    const location = await seedLocation(director);
    await directorPage.goto("/locations");

    await expect(directorPage.getByText(location.name)).toBeVisible();
    await directorPage.getByText(location.name).click();

    await directorPage.waitForURL(new RegExp(`/locations/${location.id}`));
    await expect(directorPage.getByRole("heading", { name: location.name })).toBeVisible();
  });

  test("editing a location's details saves and persists", async ({ director, directorPage }) => {
    const location = await seedLocation(director);
    await directorPage.goto(`/locations/${location.id}`);

    const nameField = directorPage.getByLabel("Name", { exact: true });
    await nameField.fill("Renamed Location");
    await directorPage.getByLabel("Max capacity").fill("42");
    await directorPage.getByRole("button", { name: "Save changes" }).click();

    await expect(directorPage.getByText("Changes saved.")).toBeVisible();

    await directorPage.reload();
    await expect(directorPage.getByLabel("Name", { exact: true })).toHaveValue("Renamed Location");
    await expect(directorPage.getByLabel("Max capacity")).toHaveValue("42");
  });

  test("clearing the required name field shows a save error and does not persist", async ({ director, directorPage }) => {
    const location = await seedLocation(director);
    await directorPage.goto(`/locations/${location.id}`);

    await directorPage.getByLabel("Name", { exact: true }).fill("");
    await directorPage.getByRole("button", { name: "Save changes" }).click();

    await expect(directorPage.getByText("Couldn't save changes. Please try again.")).toBeVisible();

    await directorPage.reload();
    await expect(directorPage.getByLabel("Name", { exact: true })).toHaveValue(location.name);
  });

  test("a failed list load shows an error state with a working retry", async ({ directorPage }) => {
    await directorPage.route("**/api/locations", (route) =>
      route.fulfill({ status: 500, contentType: "application/json", body: "{}" }),
    );

    await directorPage.goto("/locations");
    await expect(directorPage.getByText("Couldn't load locations. Please try again.")).toBeVisible();

    await directorPage.unroute("**/api/locations");
    await directorPage.getByRole("button", { name: "Retry" }).click();
    await expect(directorPage.getByText("No locations yet.")).toBeVisible();
  });
});
