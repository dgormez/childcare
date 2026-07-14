import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import { MealPreferenceRequestQueue } from "../components/menu/MealPreferenceRequestQueue";
import type { MealPreferenceChangeRequestResponse } from "../lib/types";

function makeRequest(overrides: Partial<MealPreferenceChangeRequestResponse> = {}): MealPreferenceChangeRequestResponse {
  return {
    id: "req-1",
    childId: "child-1",
    childName: "Emma Peeters",
    requestedByName: "Sofie Peeters",
    newTexture: "mixed",
    newDietaryType: ["halal"],
    notes: "Ze kan nu goed kauwen.",
    status: "pending",
    createdAt: "2027-06-01T09:00:00Z",
    decidedAt: null,
    decisionNotes: null,
    activeHealthRecords: [{ id: "hr-1", recordType: "doctor_note", title: "Slikprobleem", validFrom: "2027-05-01", validUntil: null }],
    ...overrides,
  };
}

function renderQueue(requests: MealPreferenceChangeRequestResponse[]) {
  const onApprove = vi.fn().mockResolvedValue(undefined);
  const onReject = vi.fn().mockResolvedValue(undefined);

  render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <MealPreferenceRequestQueue requests={requests} onApprove={onApprove} onReject={onReject} />
    </NextIntlClientProvider>,
  );

  return { onApprove, onReject };
}

describe("MealPreferenceRequestQueue", () => {
  it("renders each pending request with texture/dietary tags, notes, and health-record context", () => {
    renderQueue([makeRequest()]);

    expect(screen.getByText("Emma Peeters")).toBeTruthy();
    expect(screen.getByText(messages.mealPreferenceRequests.texture.mixed)).toBeTruthy();
    expect(screen.getByText(messages.mealPreferenceRequests.dietaryType.halal)).toBeTruthy();
    expect(screen.getByText("Ze kan nu goed kauwen.")).toBeTruthy();
    expect(screen.getByText("Slikprobleem")).toBeTruthy();
  });

  it("calls onApprove when Approve is clicked", async () => {
    const { onApprove } = renderQueue([makeRequest()]);
    const user = userEvent.setup();

    await user.click(screen.getByText(messages.mealPreferenceRequests.approve));

    expect(onApprove).toHaveBeenCalledWith(expect.objectContaining({ id: "req-1" }));
  });

  it("calls onReject with the entered reason after confirming the reject dialog", async () => {
    const { onReject } = renderQueue([makeRequest()]);
    const user = userEvent.setup();

    await user.click(screen.getByText(messages.mealPreferenceRequests.reject));
    await user.type(screen.getByLabelText(messages.mealPreferenceRequests.rejectReasonLabel), "Niet nodig volgens de arts.");
    await user.click(screen.getByText(messages.mealPreferenceRequests.confirmReject));

    expect(onReject).toHaveBeenCalledWith(expect.objectContaining({ id: "req-1" }), "Niet nodig volgens de arts.");
  });

  it("renders nothing when there are no pending requests", () => {
    const { container } = render(
      <NextIntlClientProvider locale="en" messages={messages}>
        <MealPreferenceRequestQueue requests={[]} onApprove={vi.fn()} onReject={vi.fn()} />
      </NextIntlClientProvider>,
    );

    expect(container.firstChild).toBeNull();
  });
});
