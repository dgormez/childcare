import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import { MilestonePortfolioView } from "../components/milestones/MilestonePortfolioView";
import { apiClient } from "../lib/apiClient";
import type { DevelopmentalDomainResponse } from "../lib/types";

vi.mock("../lib/apiClient", () => ({
  apiClient: { GET: vi.fn() },
  getAccessToken: () => "token",
}));

const createObjectURL = vi.fn(() => "blob:mock-url");
const revokeObjectURL = vi.fn();

function okResponse(data: unknown) {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
}

function errorResponse() {
  return { response: new Response(null, { status: 500 }), data: undefined, error: undefined };
}

function renderView() {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <MilestonePortfolioView childId="child-1" />
    </NextIntlClientProvider>,
  );
}

const domainWithObservation: DevelopmentalDomainResponse = {
  id: "domain-1",
  code: "language",
  nameNl: "Taal",
  nameFr: "Langage",
  nameEn: "Language",
  sortOrder: 1,
  milestones: [
    {
      id: "milestone-1",
      ageFromMonths: 12,
      ageToMonths: 18,
      descriptionNl: "Nl",
      descriptionFr: "Fr",
      descriptionEn: "Says first words",
      sortOrder: 1,
      currentStatus: "achieved",
      isCurrentFocus: true,
      history: [{ id: "obs-1", status: "achieved", observedAt: "2026-07-01", notes: null, createdAt: "2026-07-01T00:00:00Z" }],
    },
    {
      id: "milestone-2",
      ageFromMonths: 19,
      ageToMonths: 24,
      descriptionNl: "Nl2",
      descriptionFr: "Fr2",
      descriptionEn: "Points to indicate wants",
      sortOrder: 2,
      currentStatus: null,
      isCurrentFocus: false,
      history: null,
    },
  ],
};

beforeEach(() => {
  vi.clearAllMocks();
  URL.createObjectURL = createObjectURL;
  URL.revokeObjectURL = revokeObjectURL;
});

describe("MilestonePortfolioView", () => {
  it("groups milestones by domain and visually distinguishes the current-focus band", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse({ childId: "child-1", domains: [domainWithObservation] }));

    renderView();

    await waitFor(() => expect(screen.getByText("Says first words")).toBeTruthy());
    expect(screen.getByText("Language")).toBeTruthy();
    expect(screen.getByText("Points to indicate wants")).toBeTruthy();
    expect(screen.getByText("Achieved")).toBeTruthy();

    // The current-focus milestone's row carries the primary-soft distinguishing class; the
    // non-focus row does not.
    const focusRow = screen.getByText("Says first words").closest("div.flex.items-center.justify-between");
    const nonFocusRow = screen.getByText("Points to indicate wants").closest("div.flex.items-center.justify-between");
    expect(focusRow?.className).toContain("bg-primary-soft");
    expect(nonFocusRow?.className).not.toContain("bg-primary-soft");
  });

  it("shows a clear empty state for a child with no observations", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(
      okResponse({ childId: "child-1", domains: [{ ...domainWithObservation, milestones: [] }] }),
    );

    renderView();

    await waitFor(() => expect(screen.getByText("No milestones recorded yet for this child.")).toBeTruthy());
  });

  it("shows an error state with retry when the load fails", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(errorResponse());

    renderView();

    await waitFor(() => expect(screen.getByText("Could not load milestones. Please try again.")).toBeTruthy());
  });

  it("downloading the PDF succeeds for a child with data", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse({ childId: "child-1", domains: [domainWithObservation] }));
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, blob: () => Promise.resolve(new Blob(["%PDF"])) });
    vi.stubGlobal("fetch", fetchMock);

    renderView();
    await waitFor(() => expect(screen.getByText("Says first words")).toBeTruthy());

    fireEvent.click(screen.getByText("Download PDF"));

    await waitFor(() => expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining("/api/children/child-1/milestone-portfolio/pdf"),
      expect.objectContaining({ headers: { Authorization: "Bearer token" } }),
    ));
    await waitFor(() => expect(createObjectURL).toHaveBeenCalled());
    expect(screen.queryByText("Could not download the PDF. Please try again.")).toBeNull();
  });

  it("downloading the PDF still succeeds for a child with no observations (empty-state PDF, not a failure)", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(
      okResponse({ childId: "child-1", domains: [{ ...domainWithObservation, milestones: [] }] }),
    );
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, blob: () => Promise.resolve(new Blob(["%PDF"])) });
    vi.stubGlobal("fetch", fetchMock);

    renderView();
    await waitFor(() => expect(screen.getByText("No milestones recorded yet for this child.")).toBeTruthy());

    fireEvent.click(screen.getByText("Download PDF"));

    await waitFor(() => expect(fetchMock).toHaveBeenCalled());
    await waitFor(() => expect(createObjectURL).toHaveBeenCalled());
  });

  it("shows a download error message when the download fails", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse({ childId: "child-1", domains: [domainWithObservation] }));
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false }));

    renderView();
    await waitFor(() => expect(screen.getByText("Says first words")).toBeTruthy());

    fireEvent.click(screen.getByText("Download PDF"));

    await waitFor(() => expect(screen.getByText("Could not download the PDF. Please try again.")).toBeTruthy());
  });
});
