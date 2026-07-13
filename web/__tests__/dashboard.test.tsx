import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import DashboardPage from "../app/(app)/dashboard/page";
import { apiClient } from "../lib/apiClient";
import type { VaccinationsDueSoonResponse } from "../lib/types";

const push = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push }),
}));

vi.mock("../lib/apiClient", () => ({
  apiClient: { GET: vi.fn() },
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

beforeEach(() => {
  vi.mocked(apiClient.GET).mockReset();
  push.mockReset();
});

describe("DashboardPage — due-soon block", () => {
  it("renders overdue and due-soon rows sorted as returned by the backend", async () => {
    const items: VaccinationsDueSoonResponse[] = [
      { childId: "child-1", childName: "Emma Peeters", locationId: "loc-1", vaccineName: "Hep B", nextDueDate: "2026-07-05", isOverdue: true },
      { childId: "child-2", childName: "Louis Janssens", locationId: "loc-1", vaccineName: "MMR", nextDueDate: "2026-07-20", isOverdue: false },
    ];
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse(items) as never);

    renderComponent(<DashboardPage />);

    expect(await screen.findByText("Emma Peeters — Hep B")).toBeInTheDocument();
    expect(screen.getByText("Louis Janssens — MMR")).toBeInTheDocument();
    expect(screen.getByText("Overdue")).toBeInTheDocument();
    expect(screen.getByText("Due soon")).toBeInTheDocument();
  });

  it("shows a calm empty state when nothing is due", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse([]) as never);
    renderComponent(<DashboardPage />);
    expect(await screen.findByText("No vaccinations due or overdue.")).toBeInTheDocument();
  });
});
