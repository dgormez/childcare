import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import DevicesPage from "../app/(app)/devices/page";
import { apiClient } from "../lib/apiClient";
import type { DeviceSummaryResponse } from "../lib/types";

vi.mock("../lib/apiClient", () => ({
  apiClient: { GET: vi.fn(), POST: vi.fn() },
}));

function renderDevicesPage() {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <DevicesPage />
    </NextIntlClientProvider>,
  );
}

function makeDevice(overrides: Partial<DeviceSummaryResponse> = {}): DeviceSummaryResponse {
  return {
    id: "device-1",
    locationId: "loc-1",
    locationName: "Sunshine House",
    groupId: "group-1",
    groupName: "Ducklings",
    pairedByTenantUserId: "user-1",
    pairedByName: "Jane Director",
    pairedAt: "2026-07-01T09:00:00Z",
    revokedAt: null,
    ...overrides,
  };
}

function okResponse(data: unknown) {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
}

beforeEach(() => {
  vi.mocked(apiClient.GET).mockReset();
  vi.mocked(apiClient.POST).mockReset();
});

describe("DevicesPage", () => {
  it("renders paired devices with location, group, paired-by, and paired-at", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse([makeDevice()]) as never);

    renderDevicesPage();

    expect(await screen.findByText("Sunshine House")).toBeInTheDocument();
    expect(screen.getByText("Ducklings")).toBeInTheDocument();
    expect(screen.getByText("Jane Director")).toBeInTheDocument();
  });

  it("revokes a device after confirmation", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse([makeDevice()]) as never);
    vi.mocked(apiClient.POST).mockResolvedValue(okResponse({}) as never);

    renderDevicesPage();
    await screen.findByText("Sunshine House");

    await userEvent.click(screen.getByRole("button", { name: "Revoke" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByRole("button", { name: "Revoke" }));

    expect(apiClient.POST).toHaveBeenCalledWith(
      "/api/devices/{id}/revoke",
      expect.objectContaining({ params: { path: { id: "device-1" } } }),
    );
  });

  it("shows a revoked device as distinguished, without a revoke action", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(
      okResponse([makeDevice({ id: "device-2", revokedAt: "2026-07-02T09:00:00Z" })]) as never,
    );

    renderDevicesPage();

    expect(await screen.findByText("Revoked")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Revoke" })).not.toBeInTheDocument();
  });

  it("shows an empty state when no devices are paired", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse([]) as never);

    renderDevicesPage();

    expect(await screen.findByText("No devices have been paired yet.")).toBeInTheDocument();
  });

  it("shows a retryable error state when the devices list fails to load", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue({ response: new Response(null, { status: 500 }), data: undefined, error: {} } as never);

    renderDevicesPage();

    expect(await screen.findByText("Couldn't load devices. Please try again.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Retry" })).toBeInTheDocument();
  });
});
