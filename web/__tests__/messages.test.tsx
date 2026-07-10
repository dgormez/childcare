import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import MessagesPage from "../app/(app)/messages/page";
import { apiClient } from "../lib/apiClient";
import type { ContactResponse, MessageThreadSummaryResponse } from "../lib/types";

vi.mock("../lib/apiClient", () => ({
  apiClient: { GET: vi.fn(), POST: vi.fn() },
}));

vi.mock("next/link", () => ({
  default: ({ href, children, ...props }: any) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

function renderMessagesPage() {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <MessagesPage />
    </NextIntlClientProvider>,
  );
}

function okResponse(data: unknown) {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
}

function makeThread(overrides: Partial<MessageThreadSummaryResponse> = {}): MessageThreadSummaryResponse {
  return {
    id: "thread-1",
    subject: "Medication question",
    childId: "child-1",
    childName: "Emma Peeters",
    lastActivityAt: "2026-07-10T09:00:00Z",
    hasUnread: true,
    unreadFromParentCount: 1,
    ...overrides,
  };
}

function makeContact(overrides: Partial<ContactResponse> = {}): ContactResponse {
  return {
    id: "contact-1",
    firstName: "Sophie",
    lastName: "Peeters",
    phone: "+32 9 123 45 67",
    email: "sophie@example.com",
    locale: "nl",
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
});

describe("MessagesPage", () => {
  it("loads the thread list with an unread indicator", async () => {
    mockGet({ "/api/message-threads": [makeThread()] });

    renderMessagesPage();

    expect(await screen.findByText("Emma Peeters")).toBeInTheDocument();
    expect(screen.getByText("Medication question")).toBeInTheDocument();
    expect(screen.getByText(/Unread/)).toBeInTheDocument();
  });

  it("renders the empty state with no threads", async () => {
    mockGet({ "/api/message-threads": [] });

    renderMessagesPage();

    expect(await screen.findByText("No messages yet.")).toBeInTheDocument();
  });

  it("shows a general thread using the general-thread label, not a blank child name", async () => {
    mockGet({ "/api/message-threads": [makeThread({ childId: null, childName: null })] });

    renderMessagesPage();

    expect(await screen.findByText("General")).toBeInTheDocument();
  });

  it("invite dialog disables sending to a contact with no email on file", async () => {
    mockGet({
      "/api/message-threads": [],
      "/api/contacts": [makeContact({ email: null, firstName: "NoEmail" })],
    });

    renderMessagesPage();
    await userEvent.click(screen.getByRole("button", { name: "Invite parent" }));

    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText("No email on file")).toBeInTheDocument();
    expect(within(dialog).getByRole("button", { name: "Send invite" })).toBeDisabled();
  });

  it("invite dialog sends an invitation for an eligible contact", async () => {
    mockGet({
      "/api/message-threads": [],
      "/api/contacts": [makeContact()],
    });
    vi.mocked(apiClient.POST).mockResolvedValue(
      okResponse({ invitationId: "inv-1", contactId: "contact-1", email: "sophie@example.com", expiresAt: "2026-07-17" }) as never,
    );

    renderMessagesPage();
    await userEvent.click(screen.getByRole("button", { name: "Invite parent" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByRole("button", { name: "Send invite" }));

    expect(apiClient.POST).toHaveBeenCalledWith("/api/parent-invitations", { body: { contactId: "contact-1" } });
    expect(await within(dialog).findByText("Invitation sent.")).toBeInTheDocument();
  });
});
