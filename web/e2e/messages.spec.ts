import { test, expect } from "./support/fixtures";
import { seedChild, seedContactWithPickup, seedParent, seedMessageThread } from "./support/seed";

test.describe("messages", () => {
  test("a freshly registered org shows the empty state", async ({ directorPage }) => {
    await directorPage.goto("/messages");
    await expect(directorPage.getByText("No messages yet.")).toBeVisible();
  });

  test("inviting an eligible contact to the parent app succeeds", async ({ director, directorPage }) => {
    const child = await seedChild(director);
    const contact = await seedContactWithPickup(director, child.id);

    await directorPage.goto("/messages");
    await directorPage.getByRole("button", { name: "Invite parent" }).click();
    await directorPage.getByPlaceholder("Search by name or email…").fill(contact.email);
    await directorPage.getByRole("button", { name: "Send invite" }).click();

    await expect(directorPage.getByText("Invitation sent.")).toBeVisible();
  });

  test("a thread started by a parent appears in the list and can be replied to", async ({ director, directorPage }) => {
    const child = await seedChild(director);
    const parent = await seedParent(director, child.id);
    await seedMessageThread(parent, child.id, "Question about drop-off", "Can we drop off 15 minutes early tomorrow?");

    await directorPage.goto("/messages");
    await expect(directorPage.getByText(`${child.firstName} ${child.lastName}`)).toBeVisible();
    await expect(directorPage.getByText("Question about drop-off")).toBeVisible();

    await directorPage.getByText(`${child.firstName} ${child.lastName}`).click();
    await directorPage.waitForURL(/\/messages\/.+/);
    await expect(directorPage.getByText("Can we drop off 15 minutes early tomorrow?")).toBeVisible();

    await directorPage.getByPlaceholder("Write a reply…").fill("Yes, that works fine.");
    await directorPage.getByRole("button", { name: "Send", exact: true }).click();

    await expect(directorPage.getByText("Yes, that works fine.")).toBeVisible();
  });

  test("a failed list load shows an error state with a working retry", async ({ directorPage }) => {
    await directorPage.route("**/api/message-threads", (route) =>
      route.fulfill({ status: 500, contentType: "application/json", body: "{}" }),
    );

    await directorPage.goto("/messages");
    await expect(directorPage.getByText("Couldn't load messages. Please try again.")).toBeVisible();

    await directorPage.unroute("**/api/message-threads");
    await directorPage.getByRole("button", { name: "Retry" }).click();
    await expect(directorPage.getByText("No messages yet.")).toBeVisible();
  });
});
