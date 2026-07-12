import { test, expect } from "./support/fixtures";
import { seedLocation } from "./support/seed";

test.describe("announcements", () => {
  test("a freshly registered org shows the empty state", async ({ director, directorPage }) => {
    await seedLocation(director);
    await directorPage.goto("/announcements");
    await expect(directorPage.getByText("No announcements sent yet.")).toBeVisible();
  });

  test("Send stays disabled until subject and message are filled", async ({ director, directorPage }) => {
    await seedLocation(director);
    await directorPage.goto("/announcements");
    await directorPage.getByRole("button", { name: "New announcement" }).click();

    const send = directorPage.getByRole("button", { name: "Send announcement" });
    await expect(send).toBeDisabled();

    await directorPage.getByLabel("Subject").fill("Reminder");
    await expect(send).toBeDisabled();

    await directorPage.getByLabel("Message").fill("Please remember to pack sunscreen.");
    await expect(send).toBeEnabled();
  });

  test("sending an announcement to the whole location lists it", async ({ director, directorPage }) => {
    const location = await seedLocation(director);
    await directorPage.goto("/announcements");

    await directorPage.getByRole("button", { name: "New announcement" }).click();
    await directorPage.getByLabel("Subject").fill("Closed for training day");
    await directorPage.getByLabel("Message").fill("We'll be closed next Friday for staff training.");
    await directorPage.getByRole("button", { name: "Send announcement" }).click();

    await expect(directorPage.getByText("Closed for training day")).toBeVisible();
    await expect(directorPage.getByText(`${location.name} — Whole location`)).toBeVisible();
  });

  test("a failed list load shows an error state with a working retry", async ({ director, directorPage }) => {
    await seedLocation(director);
    await directorPage.route("**/api/announcements", (route) =>
      route.fulfill({ status: 500, contentType: "application/json", body: "{}" }),
    );

    await directorPage.goto("/announcements");
    await expect(directorPage.getByText("Couldn't load announcements. Please try again.")).toBeVisible();

    await directorPage.unroute("**/api/announcements");
    await directorPage.getByRole("button", { name: "Retry" }).click();
    await expect(directorPage.getByText("No announcements sent yet.")).toBeVisible();
  });
});
