import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../../i18n/locales/en.json";
import { LinkContactDialog } from "../../components/children/LinkContactDialog";
import { apiClient } from "../../lib/apiClient";
import type { ContactResponse } from "../../lib/types";

vi.mock("../../lib/apiClient", () => ({
  apiClient: { GET: vi.fn(), POST: vi.fn() },
}));

function okResponse(data: unknown) {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
}

function createdResponse(data: unknown) {
  return { response: new Response(null, { status: 201 }), data, error: undefined };
}

const existingContact: ContactResponse = {
  id: "contact-existing",
  firstName: "Anna",
  lastName: "Peeters",
  phone: "+32 9 123 45 67",
  email: "anna@test.com",
  locale: "nl",
  idVerifiedAt: null,
  idVerifiedByEmail: null,
  idDocumentType: null,
  idDocumentNote: null,
  firstIdVerifiedAt: null,
  firstIdVerifiedByEmail: null,
};

function renderDialog(onLinked = vi.fn()) {
  render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <LinkContactDialog childId="child-1" open onOpenChange={vi.fn()} onLinked={onLinked} />
    </NextIntlClientProvider>,
  );
  return onLinked;
}

beforeEach(() => {
  vi.mocked(apiClient.GET).mockReset();
  vi.mocked(apiClient.POST).mockReset();
  vi.mocked(apiClient.GET).mockResolvedValue(okResponse([existingContact]) as never);
});

describe("LinkContactDialog", () => {
  it("surfaces a link-existing suggestion when the entered email matches an existing contact", async () => {
    renderDialog();

    await waitFor(() => expect(apiClient.GET).toHaveBeenCalledWith("/api/contacts"));
    await userEvent.type(screen.getByLabelText("Email"), "anna@test.com");

    expect(await screen.findByText(/This looks like an existing contact/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Link existing contact instead" })).toBeInTheDocument();
  });

  it("links the existing contact instead of creating a new one when confirmed", async () => {
    vi.mocked(apiClient.POST).mockResolvedValue(createdResponse({ contactId: existingContact.id }) as never);
    const onLinked = renderDialog();

    await waitFor(() => expect(apiClient.GET).toHaveBeenCalledWith("/api/contacts"));
    await userEvent.type(screen.getByLabelText("Email"), "anna@test.com");
    await userEvent.click(await screen.findByRole("button", { name: "Link existing contact instead" }));
    await userEvent.click(screen.getByRole("button", { name: "Add contact" }));

    await waitFor(() => expect(apiClient.POST).toHaveBeenCalledWith(
      "/api/children/{childId}/contacts",
      expect.objectContaining({
        params: { path: { childId: "child-1" } },
        body: { contactId: existingContact.id, relationship: "Mother", canPickup: true, isPrimary: false },
      }),
    ));
    expect(apiClient.POST).not.toHaveBeenCalledWith("/api/contacts", expect.anything());
    await waitFor(() => expect(onLinked).toHaveBeenCalled());
  });

  it("creates and links a new contact when no existing contact matches", async () => {
    vi.mocked(apiClient.POST).mockImplementation((path: string) => {
      if (path === "/api/contacts") return Promise.resolve(createdResponse({ id: "contact-new" }) as never);
      return Promise.resolve(createdResponse({ contactId: "contact-new" }) as never);
    });
    const onLinked = renderDialog();

    await waitFor(() => expect(apiClient.GET).toHaveBeenCalledWith("/api/contacts"));
    await userEvent.type(screen.getByLabelText("First name"), "Bram");
    await userEvent.type(screen.getByLabelText("Last name"), "Janssens");
    await userEvent.type(screen.getByLabelText("Email"), "bram@test.com");
    await userEvent.click(screen.getByRole("button", { name: "Add contact" }));

    await waitFor(() => expect(apiClient.POST).toHaveBeenCalledWith(
      "/api/contacts",
      expect.objectContaining({ body: { firstName: "Bram", lastName: "Janssens", phone: "", email: "bram@test.com", locale: "nl" } }),
    ));
    await waitFor(() => expect(apiClient.POST).toHaveBeenCalledWith(
      "/api/children/{childId}/contacts",
      expect.objectContaining({
        params: { path: { childId: "child-1" } },
        body: { contactId: "contact-new", relationship: "Mother", canPickup: true, isPrimary: false },
      }),
    ));
    await waitFor(() => expect(onLinked).toHaveBeenCalled());
  });
});
