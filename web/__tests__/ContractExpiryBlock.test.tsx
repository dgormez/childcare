import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import { ContractExpiryBlock } from "../components/staff/ContractExpiryBlock";
import { apiClient } from "../lib/apiClient";
import type { ContractExpiringResponse } from "../lib/types";

const push = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push }),
}));

vi.mock("../lib/apiClient", () => ({
  apiClient: { GET: vi.fn() },
}));

function renderComponent() {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <ContractExpiryBlock />
    </NextIntlClientProvider>,
  );
}

function okResponse(data: unknown) {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
}

function errorResponse() {
  return { response: new Response(null, { status: 500 }), data: undefined, error: { errorKey: "errors.generic" } };
}

beforeEach(() => {
  vi.mocked(apiClient.GET).mockReset();
  push.mockReset();
});

describe("ContractExpiryBlock", () => {
  it("shows a calm empty state when nothing is expiring", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse([]) as never);
    renderComponent();
    expect(await screen.findByText("No expiring or lapsed employment contracts.")).toBeInTheDocument();
  });

  it("renders expired and expiring-soon rows with the right badge", async () => {
    const items: ContractExpiringResponse[] = [
      { staffProfileId: "staff-1", staffName: "Jane Doe", validUntil: "2026-07-01", isExpired: true },
      { staffProfileId: "staff-2", staffName: "Tom Smith", validUntil: "2026-08-10", isExpired: false },
    ];
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse(items) as never);
    renderComponent();

    expect(await screen.findByText("Jane Doe")).toBeInTheDocument();
    expect(screen.getByText("Tom Smith")).toBeInTheDocument();
    expect(screen.getByText("Expired")).toBeInTheDocument();
    expect(screen.getByText("Expiring soon")).toBeInTheDocument();
  });

  it("navigates to the staff detail page on click", async () => {
    const items: ContractExpiringResponse[] = [
      { staffProfileId: "staff-1", staffName: "Jane Doe", validUntil: "2026-07-01", isExpired: true },
    ];
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse(items) as never);
    renderComponent();

    await userEvent.click(await screen.findByText("Jane Doe"));
    expect(push).toHaveBeenCalledWith("/staff/staff-1");
  });

  it("shows an error state with retry on load failure", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(errorResponse() as never);
    renderComponent();
    expect(await screen.findByText("Could not load this overview. Please try again.")).toBeInTheDocument();
  });
});
