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
  return {
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
    ...overrides,
  };
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

// Default meal-preferences response for any ChildDetailPage test that doesn't explicitly set
// one — feature 013d's ChildMealPreferenceForm fetches this on every render of the Profiel tab,
// so every test rendering that page needs a well-formed response here, not the generic `[]`
// fallback other unmapped paths get (which would leave `texture`/`portionSize` undefined).
const DEFAULT_MEAL_PREFERENCE = {
  childId: "child-1",
  texture: "normal",
  dietaryType: [],
  portionSize: "normal",
  additionalNotes: null,
  updatedBy: null,
  updatedAt: null,
};

function mockGet(byPath: Record<string, unknown>) {
  vi.mocked(apiClient.GET).mockImplementation((path: unknown) => {
    if (typeof path === "string" && path in byPath) return Promise.resolve(okResponse(byPath[path])) as never;
    if (path === "/api/children/{childId}/meal-preferences") return Promise.resolve(okResponse(DEFAULT_MEAL_PREFERENCE)) as never;
    return Promise.resolve(okResponse([])) as never;
  });
}

/** The child-detail screen defaults to the "Profile" tab (006a FR-003) — Gezondheid content is
 * only mounted once that tab is selected (Radix Tabs unmounts inactive content by default). */
async function openHealthTab() {
  await userEvent.click(await screen.findByRole("tab", { name: "Health" }));
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

  it("creates a child with only required fields via the New child dialog", async () => {
    mockGet({ "/api/children": [] });
    vi.mocked(apiClient.POST).mockResolvedValue(okResponse(makeChild()) as never);

    renderComponent(<ChildrenPage />);
    await screen.findByText("No children yet.");

    await userEvent.click(screen.getByRole("button", { name: "New child" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.type(within(dialog).getByLabelText("First name"), "Emma");
    await userEvent.type(within(dialog).getByLabelText("Last name"), "Peeters");
    await userEvent.type(within(dialog).getByLabelText("Date of birth"), "2023-05-10");
    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(apiClient.POST).toHaveBeenCalledWith(
      "/api/children",
      expect.objectContaining({
        body: expect.objectContaining({ firstName: "Emma", lastName: "Peeters", dateOfBirth: "2023-05-10" }),
      }),
    );
    expect(push).toHaveBeenCalledWith("/children/child-1");
  });

  it("does not submit the New child form when a required field is missing", async () => {
    mockGet({ "/api/children": [] });
    renderComponent(<ChildrenPage />);
    await screen.findByText("No children yet.");

    await userEvent.click(screen.getByRole("button", { name: "New child" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(within(dialog).getByText("First name is required.")).toBeInTheDocument();
    expect(apiClient.POST).not.toHaveBeenCalled();
  });
});

describe("ChildDetailPage — Profile tab", () => {
  it("switches between Profile and Health tabs without a route change", async () => {
    mockGet({ "/api/children/{id}": makeChild(), "/api/children/{childId}/vaccine-records": [], "/api/children/{childId}/health-records": [] });
    renderComponent(<ChildDetailPage />);

    await screen.findByRole("tab", { name: "Profile", selected: true });
    await openHealthTab();
    await screen.findByRole("tab", { name: "Health", selected: true });

    // A tab switch is a client-side state change, not a navigation — the router is never asked
    // to push a new route (SC-005).
    expect(push).not.toHaveBeenCalled();
  });

  it("shows GP and pediatrician contact as distinct fields when both are present", async () => {
    mockGet({
      "/api/children/{id}": makeChild({ gpName: "Dr. Peeters", gpPhone: "+32 9 111 22 33", pediatricianName: "Dr. Claes", pediatricianPhone: "+32 9 444 55 66" }),
    });
    renderComponent(<ChildDetailPage />);

    expect(await screen.findByText("Dr. Peeters")).toBeInTheDocument();
    expect(screen.getByText("+32 9 111 22 33")).toBeInTheDocument();
    expect(screen.getByText("Dr. Claes")).toBeInTheDocument();
    expect(screen.getByText("+32 9 444 55 66")).toBeInTheDocument();
  });

  it("shows 'Not set' for an unset field rather than an error or blank row", async () => {
    mockGet({ "/api/children/{id}": makeChild() });
    renderComponent(<ChildDetailPage />);

    expect((await screen.findAllByText("Not set")).length).toBeGreaterThan(0);
  });

  it("edits the pediatrician contact independently of the GP contact", async () => {
    const child = makeChild({ gpName: "Dr. Peeters", gpPhone: "+32 9 111 22 33" });
    mockGet({ "/api/children/{id}": child });
    vi.mocked(apiClient.PUT).mockResolvedValue(
      okResponse({ ...child, pediatricianName: "Dr. Claes", pediatricianPhone: "+32 9 444 55 66" }) as never,
    );

    renderComponent(<ChildDetailPage />);
    await screen.findByText("Emma Peeters");

    await userEvent.click(screen.getByRole("button", { name: "Edit" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.type(within(dialog).getByLabelText("Pediatrician name"), "Dr. Claes");
    await userEvent.type(within(dialog).getByLabelText("Pediatrician phone"), "+32 9 444 55 66");
    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(apiClient.PUT).toHaveBeenCalledWith(
      "/api/children/{id}",
      expect.objectContaining({
        params: { path: { id: "child-1" } },
        body: expect.objectContaining({
          gpName: "Dr. Peeters",
          gpPhone: "+32 9 111 22 33",
          pediatricianName: "Dr. Claes",
          pediatricianPhone: "+32 9 444 55 66",
        }),
      }),
    );
  });

  it("does not submit the edit form when a required field is cleared (US2 AC5)", async () => {
    mockGet({ "/api/children/{id}": makeChild() });
    renderComponent(<ChildDetailPage />);
    await screen.findByText("Emma Peeters");

    await userEvent.click(screen.getByRole("button", { name: "Edit" }));
    const dialog = await screen.findByRole("dialog");
    const firstName = within(dialog).getByLabelText("First name");
    await userEvent.clear(firstName);
    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(within(dialog).getByText("First name is required.")).toBeInTheDocument();
    expect(apiClient.PUT).not.toHaveBeenCalled();
  });

  it("uploads a profile photo and reloads the child", async () => {
    const child = makeChild();
    mockGet({ "/api/children/{id}": child });
    vi.mocked(apiClient.POST).mockResolvedValue(
      okResponse({ uploadUrl: "https://fake-gcs.test/upload/children/child-1/photo.jpg", objectPath: "children/child-1/photo.jpg" }) as never,
    );
    const fetchMock = vi.fn().mockResolvedValue({ ok: true });
    vi.stubGlobal("fetch", fetchMock);

    renderComponent(<ChildDetailPage />);
    await screen.findByText("Emma Peeters");

    const file = new File(["jpeg bytes"], "photo.jpg", { type: "image/jpeg" });
    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    await userEvent.upload(input, file);

    expect(apiClient.POST).toHaveBeenCalledWith(
      "/api/children/{id}/photo/upload-url",
      expect.objectContaining({ params: { path: { id: "child-1" } } }),
    );
    expect(fetchMock).toHaveBeenCalledWith(
      "https://fake-gcs.test/upload/children/child-1/photo.jpg",
      expect.objectContaining({ method: "PUT" }),
    );

    vi.unstubAllGlobals();
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
    await openHealthTab();

    expect(await screen.findByText("Hep B")).toBeInTheDocument();
    expect(screen.getByText("Overdue")).toBeInTheDocument();
    expect(screen.getByText("Due soon")).toBeInTheDocument();
  });

  it("shows an empty state when the child has no vaccine records", async () => {
    mockGet({ "/api/children/{id}": makeChild(), "/api/children/{childId}/vaccine-records": [] });
    renderComponent(<ChildDetailPage />);
    await openHealthTab();
    expect(await screen.findByText("No vaccinations recorded yet.")).toBeInTheDocument();
  });

  it("creates a vaccine record via the add form", async () => {
    mockGet({ "/api/children/{id}": makeChild(), "/api/children/{childId}/vaccine-records": [] });
    vi.mocked(apiClient.POST).mockResolvedValue(okResponse(makeVaccine()) as never);

    renderComponent(<ChildDetailPage />);
    await openHealthTab();
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
    await openHealthTab();
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
    await openHealthTab();
    expect(await screen.findByText("No health records yet.")).toBeInTheDocument();
  });

  it("shows an expired badge for a record whose validUntil has passed", async () => {
    mockGet({
      "/api/children/{id}": makeChild(),
      "/api/children/{childId}/health-records": [makeHealthRecord({ isExpired: true })],
    });
    renderComponent(<ChildDetailPage />);
    await openHealthTab();
    expect(await screen.findByText("Peanut allergy")).toBeInTheDocument();
    expect(screen.getByText("Expired")).toBeInTheDocument();
  });

  it("creates a health record via the add form, saving successfully with no attachment", async () => {
    mockGet({ "/api/children/{id}": makeChild(), "/api/children/{childId}/health-records": [] });
    vi.mocked(apiClient.POST).mockResolvedValue(okResponse(makeHealthRecord()) as never);

    renderComponent(<ChildDetailPage />);
    await openHealthTab();
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
    await openHealthTab();
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
    await openHealthTab();
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
    await openHealthTab();
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
