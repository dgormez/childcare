import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import { Sidebar } from "../components/Sidebar";
import { apiClient } from "../lib/apiClient";
import type { Session } from "../lib/auth";

vi.mock("next/navigation", () => ({
  usePathname: () => "/dashboard",
}));

vi.mock("../lib/apiClient", () => ({
  apiClient: { GET: vi.fn() },
}));

function makeSession(isPlatformAdmin: boolean): Session {
  return {
    user: { id: "u1", email: "director@test.com", emailVerified: true, role: "director", name: "Director", isPlatformAdmin } as Session["user"],
    accessToken: "token",
    organisationSlug: "test-org",
    organisationName: "Test Org",
  };
}

function renderSidebar(session: Session) {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <Sidebar session={session} onLogout={vi.fn()} />
    </NextIntlClientProvider>,
  );
}

beforeEach(() => {
  vi.mocked(apiClient.GET).mockResolvedValue({ response: new Response(null, { status: 200 }), data: [] } as never);
});

// Feature 032, tasks.md T043 (/speckit-analyze finding F3): PLATFORM_ADMIN_NAV changed from a
// single object to an array of three entries — this test proves the section still shows/hides
// correctly against the new shape, since no prior test covered Sidebar.tsx's rendering directly.
describe("Sidebar platform-admin section", () => {
  it("shows Invitations, Organisations, and Vaccine Catalog for a platform-admin", () => {
    renderSidebar(makeSession(true));

    expect(screen.getByText("Invitations")).toBeInTheDocument();
    expect(screen.getByText("Organisations")).toBeInTheDocument();
    expect(screen.getByText("Vaccine Catalog")).toBeInTheDocument();
  });

  it("hides the entire platform-admin section for a non-platform-admin director", () => {
    renderSidebar(makeSession(false));

    expect(screen.queryByText("Invitations")).not.toBeInTheDocument();
    expect(screen.queryByText("Organisations")).not.toBeInTheDocument();
    expect(screen.queryByText("Vaccine Catalog")).not.toBeInTheDocument();
  });
});
