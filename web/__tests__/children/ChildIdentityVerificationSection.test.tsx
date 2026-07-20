import { describe, it, expect, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../../i18n/locales/en.json";
import { ChildIdentityVerificationSection } from "../../components/children/ChildIdentityVerificationSection";
import type { ChildResponse } from "../../lib/types";

const baseChild: ChildResponse = {
  id: "child-1",
  firstName: "Emma",
  lastName: "Peeters",
  dateOfBirth: "2023-05-10",
  photoDownloadUrl: null,
  gender: null,
  nationality: null,
  allergiesDescription: null,
  allergySeverity: null,
  medicalConditions: null,
  dietaryRestrictions: null,
  gpName: null,
  gpPhone: null,
  pediatricianName: null,
  pediatricianPhone: null,
  healthInsuranceNumber: null,
  kindcode: null,
  deactivatedAt: null,
  createdAt: "2026-01-01T09:00:00Z",
  updatedAt: "2026-01-01T09:00:00Z",
  idVerifiedAt: null,
  idVerifiedByEmail: null,
  idDocumentType: null,
  idDocumentNote: null,
  firstIdVerifiedAt: null,
  firstIdVerifiedByEmail: null,
  nrnLast4: null,
};

function renderSection(child: ChildResponse, onVerify = vi.fn(), onSetNrn = vi.fn()) {
  render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <ChildIdentityVerificationSection child={child} onVerify={onVerify} onSetNrn={onSetNrn} />
    </NextIntlClientProvider>,
  );
  return { onVerify, onSetNrn };
}

describe("ChildIdentityVerificationSection", () => {
  it("disables the confirm button until a document type is selected", () => {
    renderSection(baseChild);
    expect(screen.getByRole("button", { name: "Confirm" })).toBeDisabled();
  });

  it("submits the selected document type and note, and shows the read-only state after success", async () => {
    const onVerify = vi.fn().mockResolvedValue(true);
    const verified = { ...baseChild, idVerifiedAt: "2026-07-20T10:00:00Z", idVerifiedByEmail: "director@test.com", idDocumentType: "birth_certificate" as const, firstIdVerifiedAt: "2026-07-20T10:00:00Z", firstIdVerifiedByEmail: "director@test.com" };
    const { rerender } = render(
      <NextIntlClientProvider locale="en" messages={messages}>
        <ChildIdentityVerificationSection child={baseChild} onVerify={onVerify} onSetNrn={vi.fn()} />
      </NextIntlClientProvider>,
    );

    await userEvent.selectOptions(screen.getByLabelText("Document type"), "birth_certificate");
    await userEvent.type(screen.getByLabelText("Note"), "seen original");
    await userEvent.click(screen.getByRole("button", { name: "Confirm" }));

    await waitFor(() => expect(onVerify).toHaveBeenCalledWith("birth_certificate", "seen original"));

    rerender(
      <NextIntlClientProvider locale="en" messages={messages}>
        <ChildIdentityVerificationSection child={verified} onVerify={onVerify} onSetNrn={vi.fn()} />
      </NextIntlClientProvider>,
    );

    expect(screen.getByText("Birth certificate")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Correct" })).toBeInTheDocument();
  });

  it("shows only the current attribution line when first and current verification match", () => {
    const verified = { ...baseChild, idVerifiedAt: "2026-07-20T10:00:00Z", idVerifiedByEmail: "director@test.com", idDocumentType: "passport" as const, firstIdVerifiedAt: "2026-07-20T10:00:00Z", firstIdVerifiedByEmail: "director@test.com" };
    renderSection(verified);
    expect(screen.getByText("Verified by")).toBeInTheDocument();
    expect(screen.queryByText("First verified by")).not.toBeInTheDocument();
  });

  it("shows both attribution lines when the current verification differs from the first", () => {
    const corrected = {
      ...baseChild,
      idVerifiedAt: "2026-08-01T10:00:00Z",
      idVerifiedByEmail: "director2@test.com",
      idDocumentType: "eid" as const,
      firstIdVerifiedAt: "2026-07-20T10:00:00Z",
      firstIdVerifiedByEmail: "director@test.com",
    };
    renderSection(corrected);
    expect(screen.getByText("Most recently confirmed by")).toBeInTheDocument();
    expect(screen.getByText("First verified by")).toBeInTheDocument();
  });

  it("shows only the masked NRN after a successful save, never the raw value", async () => {
    const onSetNrn = vi.fn().mockResolvedValue(true);
    const withNrn = { ...baseChild, nrnLast4: "3371" };
    const { rerender } = render(
      <NextIntlClientProvider locale="en" messages={messages}>
        <ChildIdentityVerificationSection child={baseChild} onVerify={vi.fn()} onSetNrn={onSetNrn} />
      </NextIntlClientProvider>,
    );

    await userEvent.click(screen.getByRole("button", { name: "Add" }));
    await userEvent.type(screen.getByPlaceholderText("85.07.30-033.71"), "85073003371");
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(onSetNrn).toHaveBeenCalledWith("85073003371"));

    rerender(
      <NextIntlClientProvider locale="en" messages={messages}>
        <ChildIdentityVerificationSection child={withNrn} onVerify={vi.fn()} onSetNrn={onSetNrn} />
      </NextIntlClientProvider>,
    );

    expect(screen.getByText("•••••••3371")).toBeInTheDocument();
    expect(screen.queryByText("85073003371")).not.toBeInTheDocument();
  });

  it("rejects a non-11-digit NRN client-side, without calling onSetNrn", async () => {
    const onSetNrn = vi.fn();
    renderSection(baseChild, vi.fn(), onSetNrn);

    await userEvent.click(screen.getByRole("button", { name: "Add" }));
    await userEvent.type(screen.getByPlaceholderText("85.07.30-033.71"), "12345");
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByText("Enter a valid National Register Number (11 digits).")).toBeInTheDocument();
    expect(onSetNrn).not.toHaveBeenCalled();
  });
});
