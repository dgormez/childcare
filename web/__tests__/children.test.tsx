import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import ChildrenPage from "../app/(app)/children/page";
import ChildDetailPage from "../app/(app)/children/[id]/page";
import { apiClient } from "../lib/apiClient";
import type { ChildResponse, VaccineRecordResponse, HealthRecordResponse } from "../lib/types";

const push = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push }),
  useParams: () => ({ id: "child-1" }),
}));

vi.mock("../lib/apiClient", () => ({
  apiClient: { GET: vi.fn(), POST: vi.fn(), PUT: vi.fn(), DELETE: vi.fn() },
}));

function renderComponent(ui: React.ReactElement) {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      {ui}
    </NextIntlClientProvider>,
  );
}

function okResponse(data: unknown) {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
}

function makeChild(overrides: Partial<ChildResponse> = {}): ChildResponse {
  return { id: "child-1", firstName: "Emma", lastName: "Peeters", dateOfBirth: "2023-05-10", deactivatedAt: null, ...overrides };
}

function makeVaccine(overrides: Partial<VaccineRecordResponse> = {}): VaccineRecordResponse {
  return {
    id: "vaccine-1",
    childId: "child-1",
    vaccineName: "DTP",
    doseNumber: 2,
    administeredOn: "2026-06-01",
    nextDueDate: null,
    administeredBy: "Dr. Peeters",
    notes: null,
    recordedBy: "director-1",
    createdAt: "2026-06-01T09:00:00Z",
    updatedAt: "2026-06-01T09:00:00Z",
    ...overrides,
  };
}

function makeHealthRecord(overrides: Partial<HealthRecordResponse> = {}): HealthRecordResponse {
  return {
    id: "health-1",
    childId: "child-1",
    recordType: "allergy",
    title: "Peanut allergy",
    description: "Confirmed by allergist.",
    validFrom: null,
    validUntil: null,
    isExpired: false,
    attachmentDownloadUrl: null,
    recordedBy: "director-1",
    createdAt: "2026-01-01T09:00:00Z",
    updatedAt: null,
    ...overrides,
  };
}

function mockGet(byPath: Record<string, unknown>) {
  vi.mocked(apiClient.GET).mockImplementation((path: unknown) => {
    if (typeof path === "string" && path in byPath) return Promise.resolve(okResponse(byPath[path])) as never;
    return Promise.resolve(okResponse([])) as never;
  });
}

beforeEach(() => {
  vi.mocked(apiClient.GET).mockReset();
  vi.mocked(apiClient.POST).mockReset();
  vi.mocked(apiClient.PUT).mockReset();
  vi.mocked(apiClient.DELETE).mockReset();
  push.mockReset();
});

describe("ChildrenPage", () => {
  it("lists children and navigates to a child's detail page on row click", async () => {
    mockGet({ "/api/children": [makeChild()] });
    renderComponent(<ChildrenPage />);

    const row = await screen.findByText("Emma Peeters");
    await userEvent.click(row);
    expect(push).toHaveBeenCalledWith("/children/child-1");
  });

  it("shows an empty state when there are no children", async () => {
    mockGet({ "/api/children": [] });
    renderComponent(<ChildrenPage />);
    expect(await screen.findByText("No children yet.")).toBeInTheDocument();
  });
});

describe("ChildDetailPage — Gezondheid / vaccines", () => {
  it("shows an overdue badge for a past-due vaccine and a due-soon badge for one due within 30 days", async () => {
    mockGet({
      "/api/children/{id}": makeChild(),
      "/api/children/{childId}/vaccine-records": [
        makeVaccine({ id: "v-overdue", vaccineName: "Hep B", doseNumber: null, nextDueDate: "2020-01-01" }),
        makeVaccine({ id: "v-due-soon", vaccineName: "MMR", doseNumber: null, nextDueDate: new Date(Date.now() + 5 * 86400000).toISOString().slice(0, 10) }),
      ],
    });

    renderComponent(<ChildDetailPage />);

    expect(await screen.findByText("Hep B")).toBeInTheDocument();
    expect(screen.getByText("Overdue")).toBeInTheDocument();
    expect(screen.getByText("Due soon")).toBeInTheDocument();
  });

  it("shows an empty state when the child has no vaccine records", async () => {
    mockGet({ "/api/children/{id}": makeChild(), "/api/children/{childId}/vaccine-records": [] });
    renderComponent(<ChildDetailPage />);
    expect(await screen.findByText("No vaccinations recorded yet.")).toBeInTheDocument();
  });

  it("creates a vaccine record via the add form", async () => {
    mockGet({ "/api/children/{id}": makeChild(), "/api/children/{childId}/vaccine-records": [] });
    vi.mocked(apiClient.POST).mockResolvedValue(okResponse(makeVaccine()) as never);

    renderComponent(<ChildDetailPage />);
    await screen.findByText("No vaccinations recorded yet.");

    await userEvent.click(screen.getByRole("button", { name: "Add vaccination" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.type(within(dialog).getByLabelText("Vaccine name"), "DTP");
    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(apiClient.POST).toHaveBeenCalledWith(
      "/api/children/{childId}/vaccine-records",
      expect.objectContaining({ params: { path: { childId: "child-1" } } }),
    );
  });

  it("deletes a vaccine record after confirmation", async () => {
    mockGet({ "/api/children/{id}": makeChild(), "/api/children/{childId}/vaccine-records": [makeVaccine()] });
    vi.mocked(apiClient.DELETE).mockResolvedValue({ response: new Response(null, { status: 204 }), data: undefined, error: undefined } as never);

    renderComponent(<ChildDetailPage />);
    await screen.findByText("DTP (dose 2)");

    await userEvent.click(screen.getByRole("button", { name: "Delete" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByRole("button", { name: "Delete" }));

    expect(apiClient.DELETE).toHaveBeenCalledWith(
      "/api/children/{childId}/vaccine-records/{id}",
      expect.objectContaining({ params: { path: { childId: "child-1", id: "vaccine-1" } } }),
    );
  });
});

describe("ChildDetailPage — Gezondheid / health records", () => {
  it("shows an empty state when the child has no health records", async () => {
    mockGet({ "/api/children/{id}": makeChild(), "/api/children/{childId}/health-records": [] });
    renderComponent(<ChildDetailPage />);
    expect(await screen.findByText("No health records yet.")).toBeInTheDocument();
  });

  it("shows an expired badge for a record whose validUntil has passed", async () => {
    mockGet({
      "/api/children/{id}": makeChild(),
      "/api/children/{childId}/health-records": [makeHealthRecord({ isExpired: true })],
    });
    renderComponent(<ChildDetailPage />);
    expect(await screen.findByText("Peanut allergy")).toBeInTheDocument();
    expect(screen.getByText("Expired")).toBeInTheDocument();
  });

  it("creates a health record via the add form, saving successfully with no attachment", async () => {
    mockGet({ "/api/children/{id}": makeChild(), "/api/children/{childId}/health-records": [] });
    vi.mocked(apiClient.POST).mockResolvedValue(okResponse(makeHealthRecord()) as never);

    renderComponent(<ChildDetailPage />);
    await screen.findByText("No health records yet.");

    await userEvent.click(screen.getByRole("button", { name: "Add record" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.type(within(dialog).getByLabelText("Title"), "Peanut allergy");
    await userEvent.type(within(dialog).getByLabelText("Description"), "Confirmed by allergist.");
    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(apiClient.POST).toHaveBeenCalledWith(
      "/api/children/{childId}/health-records",
      expect.objectContaining({ params: { path: { childId: "child-1" } } }),
    );
  });

  it("uploads an attachment by requesting an upload URL then PUTting the file directly", async () => {
    mockGet({
      "/api/children/{id}": makeChild(),
      "/api/children/{childId}/health-records": [makeHealthRecord()],
    });
    vi.mocked(apiClient.POST).mockResolvedValue(
      okResponse({ uploadUrl: "https://fake-gcs.test/upload/health-records/health-1/attachment.pdf", expiresInSeconds: 900 }) as never,
    );
    const fetchMock = vi.fn().mockResolvedValue({ ok: true });
    vi.stubGlobal("fetch", fetchMock);

    renderComponent(<ChildDetailPage />);
    await screen.findByText("Peanut allergy");

    const file = new File(["%PDF-1.4"], "letter.pdf", { type: "application/pdf" });
    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    await userEvent.upload(input, file);

    expect(apiClient.POST).toHaveBeenCalledWith(
      "/api/children/{childId}/health-records/{id}/attachment-upload-url",
      expect.objectContaining({ params: { path: { childId: "child-1", id: "health-1" } }, body: { contentType: "application/pdf" } }),
    );
    expect(fetchMock).toHaveBeenCalledWith(
      "https://fake-gcs.test/upload/health-records/health-1/attachment.pdf",
      expect.objectContaining({ method: "PUT" }),
    );

    vi.unstubAllGlobals();
  });

  it("rejects an unsupported attachment file type before calling the API", async () => {
    mockGet({
      "/api/children/{id}": makeChild(),
      "/api/children/{childId}/health-records": [makeHealthRecord()],
    });

    renderComponent(<ChildDetailPage />);
    await screen.findByText("Peanut allergy");

    // A drag-and-drop (unlike the native file picker) bypasses the input's `accept` filter, so
    // client-side validation must still run — applyAccept:false simulates that real-world path.
    const file = new File(["zip contents"], "archive.zip", { type: "application/zip" });
    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    await userEvent.upload(input, file, { applyAccept: false });

    expect((await screen.findAllByText("Only PDF, JPEG, or PNG files are allowed.")).length).toBeGreaterThan(0);
    expect(apiClient.POST).not.toHaveBeenCalled();
  });

  it("deletes a health record after confirmation", async () => {
    mockGet({
      "/api/children/{id}": makeChild(),
      "/api/children/{childId}/health-records": [makeHealthRecord()],
    });
    vi.mocked(apiClient.DELETE).mockResolvedValue({ response: new Response(null, { status: 204 }), data: undefined, error: undefined } as never);

    renderComponent(<ChildDetailPage />);
    await screen.findByText("Peanut allergy");

    await userEvent.click(screen.getByRole("button", { name: "Delete" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByRole("button", { name: "Delete" }));

    expect(apiClient.DELETE).toHaveBeenCalledWith(
      "/api/children/{childId}/health-records/{id}",
      expect.objectContaining({ params: { path: { childId: "child-1", id: "health-1" } } }),
    );
  });
});
