import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../../i18n/locales/en.json";
import { ChildContactsTab } from "../../components/children/ChildContactsTab";
import { apiClient } from "../../lib/apiClient";
import type { ChildContactResponse } from "../../lib/types";

vi.mock("../../lib/apiClient", () => ({
  apiClient: { GET: vi.fn(), POST: vi.fn(), PUT: vi.fn(), DELETE: vi.fn() },
}));

function okResponse(data: unknown) {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
}

function errorResponse(status = 500) {
  return { response: new Response(null, { status }), data: undefined, error: { errorKey: "errors.contact.not_found" } };
}

function renderTab() {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <ChildContactsTab childId="child-1" />
    </NextIntlClientProvider>,
  );
}

const primaryContact: ChildContactResponse = {
  contactId: "contact-1",
  firstName: "Anna",
  lastName: "Peeters",
  phone: "+32 9 123 45 67",
  email: "anna@test.com",
  locale: "nl",
  relationship: "Mother",
  canPickup: true,
  isPrimary: true,
  idVerifiedAt: null,
  idVerifiedByEmail: null,
  idDocumentType: null,
  idDocumentNote: null,
  firstIdVerifiedAt: null,
  firstIdVerifiedByEmail: null,
};

const secondaryContact: ChildContactResponse = {
  contactId: "contact-2",
  firstName: "Tom",
  lastName: "Peeters",
  phone: "+32 9 987 65 43",
  email: "tom@test.com",
  locale: "nl",
  relationship: "Father",
  canPickup: true,
  isPrimary: false,
  idVerifiedAt: null,
  idVerifiedByEmail: null,
  idDocumentType: null,
  idDocumentNote: null,
  firstIdVerifiedAt: null,
  firstIdVerifiedByEmail: null,
};

beforeEach(() => {
  vi.mocked(apiClient.GET).mockReset();
  vi.mocked(apiClient.PUT).mockReset();
  vi.mocked(apiClient.DELETE).mockReset();
});

describe("ChildContactsTab", () => {
  it("renders linked contacts with relationship and a primary badge", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse([primaryContact, secondaryContact]) as never);
    renderTab();

    expect(await screen.findByText("Anna Peeters")).toBeInTheDocument();
    expect(screen.getByText("Tom Peeters")).toBeInTheDocument();
    expect(screen.getByText("Primary")).toBeInTheDocument();
    expect(screen.getByText(/Mother/)).toBeInTheDocument();
    expect(screen.getByText(/Father/)).toBeInTheDocument();
  });

  it("shows an empty state with zero contacts", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse([]) as never);
    renderTab();

    expect(await screen.findByText("No contacts linked yet.")).toBeInTheDocument();
  });

  it("changing the primary contact calls the update-link endpoint and reflects the new primary", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse([primaryContact, secondaryContact]) as never);
    vi.mocked(apiClient.PUT).mockResolvedValue(okResponse({}) as never);
    renderTab();

    await screen.findByText("Anna Peeters");
    await userEvent.click(screen.getByLabelText("Set as primary"));

    await waitFor(() => expect(apiClient.PUT).toHaveBeenCalledWith(
      "/api/children/{childId}/contacts/{contactId}",
      expect.objectContaining({
        params: { path: { childId: "child-1", contactId: "contact-2" } },
        body: { relationship: "Father", canPickup: true, isPrimary: true },
      }),
    ));
  });

  it("shows an error notice when setting a new primary fails", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse([primaryContact, secondaryContact]) as never);
    vi.mocked(apiClient.PUT).mockResolvedValue(errorResponse() as never);
    renderTab();

    await screen.findByText("Anna Peeters");
    await userEvent.click(screen.getByLabelText("Set as primary"));

    expect(await screen.findByText("Couldn't update the primary contact. Please try again.")).toBeInTheDocument();
  });

  it("removes a contact after confirmation", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse([primaryContact, secondaryContact]) as never);
    vi.mocked(apiClient.DELETE).mockResolvedValue(okResponse({}) as never);
    renderTab();

    await screen.findByText("Anna Peeters");
    await userEvent.click(screen.getAllByLabelText("Remove contact")[0]);
    await userEvent.click(screen.getByRole("button", { name: "Remove" }));

    await waitFor(() => expect(apiClient.DELETE).toHaveBeenCalledWith(
      "/api/children/{childId}/contacts/{contactId}",
      expect.objectContaining({ params: { path: { childId: "child-1", contactId: "contact-1" } } }),
    ));
  });
});
