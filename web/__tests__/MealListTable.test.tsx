import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import { MealListTable } from "../components/meal-list/MealListTable";
import type { MealListGroupEntry, MealListChildEntry } from "../lib/types";

function renderTable(groups: MealListGroupEntry[]) {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <MealListTable groups={groups} />
    </NextIntlClientProvider>,
  );
}

function makeChild(overrides: Partial<MealListChildEntry> = {}): MealListChildEntry {
  return {
    childId: "child-1",
    firstName: "Emma",
    lastName: "Peeters",
    texture: "normal",
    dietaryType: [],
    portionSize: "normal",
    additionalNotes: null,
    hasPreference: true,
    allergySeverity: "none",
    hasStandingMedication: false,
    ...overrides,
  };
}

describe("MealListTable", () => {
  it("groups children by group/section, rendering each group's name as a header", () => {
    const groups: MealListGroupEntry[] = [
      { groupId: "g1", groupName: "Butterflies", children: [makeChild({ childId: "c1" })] },
      { groupId: "g2", groupName: "Ladybugs", children: [makeChild({ childId: "c2", firstName: "Liam" })] },
    ];
    renderTable(groups);

    expect(screen.getByText("Butterflies")).toBeTruthy();
    expect(screen.getByText("Ladybugs")).toBeTruthy();
    expect(screen.getByText(/Emma/)).toBeTruthy();
    expect(screen.getByText(/Liam/)).toBeTruthy();
  });

  it('renders "Geen voorkeur" (No preference) for a child with hasPreference=false, not a hidden row', () => {
    const groups: MealListGroupEntry[] = [
      { groupId: "g1", groupName: "Butterflies", children: [makeChild({ hasPreference: false, texture: "normal" })] },
    ];
    renderTable(groups);

    expect(screen.getByText(messages.mealList.noPreference)).toBeTruthy();
  });

  it("renders the child's actual texture when hasPreference=true", () => {
    const groups: MealListGroupEntry[] = [
      { groupId: "g1", groupName: "Butterflies", children: [makeChild({ hasPreference: true, texture: "pureed" })] },
    ];
    renderTable(groups);

    expect(screen.getByText(messages.mealList.texture.pureed)).toBeTruthy();
  });
});
