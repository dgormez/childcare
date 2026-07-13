import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import { ChildMealPreferenceForm } from "../components/children/ChildMealPreferenceForm";
import { apiClient } from "../lib/apiClient";
import type { MealPreferenceResponse } from "../lib/types";

vi.mock("../lib/apiClient", () => ({
  apiClient: { GET: vi.fn(), PUT: vi.fn() },
}));

function okResponse(data: unknown) {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
}

function makePreference(overrides: Partial<MealPreferenceResponse> = {}): MealPreferenceResponse {
  return {
    childId: "child-1",
    texture: "normal",
    dietaryType: [],
    portionSize: "normal",
    additionalNotes: null,
    updatedBy: null,
    updatedAt: null,
    ...overrides,
  };
}

function renderForm() {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <ChildMealPreferenceForm childId="child-1" />
    </NextIntlClientProvider>,
  );
}

beforeEach(() => {
  vi.mocked(apiClient.GET).mockReset();
  vi.mocked(apiClient.PUT).mockReset();
});

describe("ChildMealPreferenceForm", () => {
  it("submits only changed fields, leaving other current values intact", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(
      okResponse(makePreference({ texture: "mixed", dietaryType: ["halal"], portionSize: "small", additionalNotes: "Note" })) as never,
    );
    vi.mocked(apiClient.PUT).mockResolvedValue(okResponse(makePreference({ texture: "pieces" })) as never);

    renderForm();
    const user = userEvent.setup();

    await screen.findByText(messages.mealList.texture.mixed);
    await user.click(screen.getByText(messages.mealList.preferenceForm.editButton));

    const textureSelect = await screen.findByDisplayValue(messages.mealList.texture.mixed);
    await user.selectOptions(textureSelect, "pieces");
    await user.click(screen.getByText(messages.mealList.preferenceForm.saveButton));

    expect(apiClient.PUT).toHaveBeenCalledWith(
      "/api/children/{childId}/meal-preferences",
      expect.objectContaining({
        params: { path: { childId: "child-1" } },
        body: { texture: "pieces", dietaryType: ["halal"], portionSize: "small", additionalNotes: "Note" },
      }),
    );
  });

  it("shows a validation/save error and does not exit edit mode when the save fails", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse(makePreference()) as never);
    vi.mocked(apiClient.PUT).mockResolvedValue({
      response: new Response(null, { status: 422 }),
      data: undefined,
      error: { errorKey: "errors.meal_preferences.additional_notes_too_long" },
    } as never);

    renderForm();
    const user = userEvent.setup();

    await screen.findByText(messages.mealList.preferenceForm.editButton);
    await user.click(screen.getByText(messages.mealList.preferenceForm.editButton));
    await user.click(screen.getByText(messages.mealList.preferenceForm.saveButton));

    expect(await screen.findByText(messages.mealList.preferenceForm.saveError)).toBeTruthy();
    expect(screen.getByText(messages.mealList.preferenceForm.saveButton)).toBeTruthy();
  });
});
