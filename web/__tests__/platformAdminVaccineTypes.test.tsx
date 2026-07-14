import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import VaccineTypeManagementPage from "../app/(app)/platform-admin/vaccine-types/page";
import { apiClient } from "../lib/apiClient";
import type { PlatformAdminVaccineTypeResponse } from "../lib/types";

const replace = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace }),
}));

vi.mock("../lib/apiClient", () => ({
  apiClient: { GET: vi.fn(), POST: vi.fn(), PATCH: vi.fn() },
}));

let mockSession: { user: { isPlatformAdmin: boolean } } | null = { user: { isPlatformAdmin: true } };
vi.mock("../components/AuthProvider", () => ({
  useAuth: () => ({ session: mockSession, setSession: vi.fn(), loading: false }),
}));

function renderPage() {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <VaccineTypeManagementPage />
    </NextIntlClientProvider>,
  );
}

function okResponse(data: unknown) {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
}

function errorResponse() {
  return { response: new Response(null, { status: 422 }), data: undefined, error: { errorKey: "errors.validation" } };
}

function makeEntry(overrides: Partial<PlatformAdminVaccineTypeResponse> = {}): PlatformAdminVaccineTypeResponse {
  return {
    id: "type-1",
    name: "HPV",
    category: "basisvaccinatieschema",
    sortOrder: 0,
    isActive: true,
    deactivatedByEmail: null,
    deactivatedAt: null,
    ...overrides,
  };
}

beforeEach(() => {
  mockSession = { user: { isPlatformAdmin: true } };
  vi.mocked(apiClient.GET).mockReset();
  vi.mocked(apiClient.POST).mockReset();
  vi.mocked(apiClient.PATCH).mockReset();
  replace.mockReset();
});

describe("VaccineTypeManagementPage", () => {
  it("loads and renders the catalog table", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse([makeEntry()]) as never);

    renderPage();

    expect(await screen.findByText("HPV")).toBeInTheDocument();
    expect(screen.getByText("Mandatory schedule")).toBeInTheDocument();
    expect(screen.getByText("Active")).toBeInTheDocument();
  });

  it("shows the empty state when the catalog has no entries", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse([]) as never);

    renderPage();

    expect(await screen.findByText("No catalog entries yet.")).toBeInTheDocument();
  });

  it("shows the deactivated-by audit line for inactive entries", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse([
      makeEntry({ isActive: false, deactivatedByEmail: "admin@test.com", deactivatedAt: "2026-07-01T10:00:00Z" }),
    ]) as never);

    renderPage();

    expect(await screen.findByText("Inactive")).toBeInTheDocument();
    expect(screen.getByText(/Deactivated by admin@test\.com on/)).toBeInTheDocument();
  });

  it("creates a new catalog entry via the add dialog", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse([makeEntry()]) as never);
    vi.mocked(apiClient.POST).mockResolvedValue(okResponse(makeEntry({ id: "type-2", name: "MenB" })) as never);

    renderPage();
    await screen.findByText("HPV");

    await userEvent.click(screen.getByRole("button", { name: "Add vaccine" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.type(within(dialog).getByLabelText("Name"), "MenB");
    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(apiClient.POST).toHaveBeenCalledWith(
      "/api/platform-admin/vaccine-types",
      expect.objectContaining({ body: { name: "MenB", category: null } }),
    );
  });

  it("renames an entry via the edit dialog, pre-filled with its current values", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse([makeEntry()]) as never);
    vi.mocked(apiClient.PATCH).mockResolvedValue(okResponse(makeEntry({ name: "HPV Renamed" })) as never);

    renderPage();
    await userEvent.click(await screen.findByText("HPV"));

    const dialog = await screen.findByRole("dialog");
    const nameInput = within(dialog).getByLabelText("Name") as HTMLInputElement;
    expect(nameInput.value).toBe("HPV");

    await userEvent.clear(nameInput);
    await userEvent.type(nameInput, "HPV Renamed");
    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(apiClient.PATCH).toHaveBeenCalledWith(
      "/api/platform-admin/vaccine-types/{id}",
      expect.objectContaining({ params: { path: { id: "type-1" } }, body: { name: "HPV Renamed", category: "basisvaccinatieschema" } }),
    );
  });

  it("reorders an entry with the up/down buttons", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse([makeEntry()]) as never);
    vi.mocked(apiClient.POST).mockResolvedValue(okResponse([makeEntry()]) as never);

    renderPage();
    await screen.findByText("HPV");

    await userEvent.click(screen.getByRole("button", { name: "Move down" }));

    expect(apiClient.POST).toHaveBeenCalledWith(
      "/api/platform-admin/vaccine-types/{id}/reorder",
      expect.objectContaining({ params: { path: { id: "type-1" } }, body: { direction: "down" } }),
    );
  });

  it("shows a boundary notice when reorder is rejected", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse([makeEntry()]) as never);
    vi.mocked(apiClient.POST).mockResolvedValue(errorResponse() as never);

    renderPage();
    await screen.findByText("HPV");

    await userEvent.click(screen.getByRole("button", { name: "Move up" }));

    expect(await screen.findByText("This entry is already at the edge of its category.")).toBeInTheDocument();
  });

  it("deactivates an active entry", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse([makeEntry()]) as never);
    vi.mocked(apiClient.POST).mockResolvedValue(okResponse(makeEntry({ isActive: false })) as never);

    renderPage();
    await screen.findByText("HPV");

    await userEvent.click(screen.getByRole("button", { name: "Deactivate" }));

    expect(apiClient.POST).toHaveBeenCalledWith(
      "/api/platform-admin/vaccine-types/{id}/deactivate",
      expect.objectContaining({ params: { path: { id: "type-1" } } }),
    );
  });

  it("reactivates an inactive entry", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse([makeEntry({ isActive: false, deactivatedByEmail: "admin@test.com", deactivatedAt: "2026-07-01T10:00:00Z" })]) as never);
    vi.mocked(apiClient.POST).mockResolvedValue(okResponse(makeEntry()) as never);

    renderPage();
    await screen.findByText("HPV");

    await userEvent.click(screen.getByRole("button", { name: "Reactivate" }));

    expect(apiClient.POST).toHaveBeenCalledWith(
      "/api/platform-admin/vaccine-types/{id}/reactivate",
      expect.objectContaining({ params: { path: { id: "type-1" } } }),
    );
  });

  it("redirects away a director without the platform-admin flag", async () => {
    mockSession = { user: { isPlatformAdmin: false } };
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse([]) as never);

    renderPage();

    expect(replace).toHaveBeenCalledWith("/dashboard");
  });
});
