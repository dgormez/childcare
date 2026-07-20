import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../../i18n/locales/en.json";
import { ContactIdentityVerificationDialog } from "../../components/children/ContactIdentityVerificationDialog";
import { apiClient } from "../../lib/apiClient";
import type { ChildContactResponse } from "../../lib/types";

vi.mock("../../lib/apiClient", () => ({
  apiClient: { POST: vi.fn() },
}));

function okResponse(data: unknown) {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
}

function errorResponse() {
  return { response: new Response(null, { status: 422 }), data: undefined, error: { errorKey: "errors.validation" } };
}

const unverifiedContact: ChildContactResponse = {
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

const correctedContact: ChildContactResponse = {
  ...unverifiedContact,
  idVerifiedAt: "2026-08-01T10:00:00Z",
  idVerifiedByEmail: "director2@test.com",
  idDocumentType: "eid",
  firstIdVerifiedAt: "2026-07-20T10:00:00Z",
  firstIdVerifiedByEmail: "director@test.com",
};

beforeEach(() => {
  vi.mocked(apiClient.POST).mockReset();
});

describe("ContactIdentityVerificationDialog", () => {
  it("submits and closes on success", async () => {
    vi.mocked(apiClient.POST).mockResolvedValue(okResponse({}) as never);
    const onOpenChange = vi.fn();
    const onVerified = vi.fn();

    render(
      <NextIntlClientProvider locale="en" messages={messages}>
        <ContactIdentityVerificationDialog contact={unverifiedContact} open onOpenChange={onOpenChange} onVerified={onVerified} />
      </NextIntlClientProvider>,
    );

    await userEvent.selectOptions(screen.getByLabelText("Document type"), "passport");
    await userEvent.click(screen.getByRole("button", { name: "Confirm" }));

    await waitFor(() => expect(apiClient.POST).toHaveBeenCalledWith(
      "/api/contacts/{id}/identity-verification",
      expect.objectContaining({ params: { path: { id: "contact-1" } }, body: { documentType: "passport", note: null } }),
    ));
    await waitFor(() => expect(onOpenChange).toHaveBeenCalledWith(false));
    expect(onVerified).toHaveBeenCalled();
  });

  it("shows a save error and stays open when the request fails", async () => {
    vi.mocked(apiClient.POST).mockResolvedValue(errorResponse() as never);
    const onOpenChange = vi.fn();

    render(
      <NextIntlClientProvider locale="en" messages={messages}>
        <ContactIdentityVerificationDialog contact={unverifiedContact} open onOpenChange={onOpenChange} onVerified={vi.fn()} />
      </NextIntlClientProvider>,
    );

    await userEvent.selectOptions(screen.getByLabelText("Document type"), "passport");
    await userEvent.click(screen.getByRole("button", { name: "Confirm" }));

    expect(await screen.findByText("Could not save the verification. Please try again.")).toBeInTheDocument();
    expect(onOpenChange).not.toHaveBeenCalled();
  });

  it("shows both attribution lines only when the current verification differs from the first", () => {
    render(
      <NextIntlClientProvider locale="en" messages={messages}>
        <ContactIdentityVerificationDialog contact={correctedContact} open onOpenChange={vi.fn()} onVerified={vi.fn()} />
      </NextIntlClientProvider>,
    );

    expect(screen.getByText(/First verified by/)).toBeInTheDocument();
  });

  it("shows no attribution banner when there's nothing verified yet", () => {
    render(
      <NextIntlClientProvider locale="en" messages={messages}>
        <ContactIdentityVerificationDialog contact={unverifiedContact} open onOpenChange={vi.fn()} onVerified={vi.fn()} />
      </NextIntlClientProvider>,
    );

    expect(screen.queryByText(/First verified by/)).not.toBeInTheDocument();
  });
});
