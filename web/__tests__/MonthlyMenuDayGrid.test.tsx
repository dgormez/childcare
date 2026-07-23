import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import { MonthlyMenuDayGrid } from "../components/menu/MonthlyMenuDayGrid";
import type { MonthlyMenuDaySave } from "../components/menu/MonthlyMenuDayGrid";
import type { MonthlyMenuResponse } from "../lib/types";

function renderGrid(menu: MonthlyMenuResponse) {
  const onSave = vi.fn((_days: MonthlyMenuDaySave[]) => Promise.resolve());
  const onPublish = vi.fn(() => Promise.resolve());
  const onUnpublish = vi.fn(() => Promise.resolve());

  render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <MonthlyMenuDayGrid year={2027} month={6} menu={menu} saving={false} onSave={onSave} onPublish={onPublish} onUnpublish={onUnpublish} />
    </NextIntlClientProvider>,
  );

  return { onSave, onPublish, onUnpublish };
}

function draftMenu(): MonthlyMenuResponse {
  return {
    exists: true,
    isPublished: false,
    publishedAt: null,
    variant: null,
    days: [{ date: "2027-06-01", lunchMeal: "Tomatensoep", alternativeLunchMeal: "Kip met puree", snack: "Yoghurt", notes: null }],
  };
}

describe("MonthlyMenuDayGrid", () => {
  it("renders every day of the month with existing values pre-filled", () => {
    renderGrid(draftMenu());

    // June 2027 has 30 days.
    expect(screen.getAllByLabelText(messages.menu.columnLunchMeal)).toHaveLength(30);
    expect(screen.getByDisplayValue("Tomatensoep")).toBeTruthy();
    expect(screen.getByDisplayValue("Kip met puree")).toBeTruthy();
  });

  it("Save draft calls onSave with the edited day values, blanks converted to null", async () => {
    const { onSave } = renderGrid(draftMenu());
    const user = userEvent.setup();

    const snackInputs = screen.getAllByLabelText(messages.menu.columnSnack);
    await user.clear(snackInputs[0]);
    await user.type(snackInputs[0], "Fruit");

    await user.click(screen.getByText(messages.menu.saveDraft));

    expect(onSave).toHaveBeenCalledTimes(1);
    const days = onSave.mock.calls[0][0];
    expect(days).toHaveLength(30);
    expect(days[0]).toEqual({ date: "2027-06-01", lunchMeal: "Tomatensoep", alternativeLunchMeal: "Kip met puree", snack: "Fruit", notes: null });
    expect(days[1]).toEqual({ date: "2027-06-02", lunchMeal: null, alternativeLunchMeal: null, snack: null, notes: null });
  });

  it("shows Publish for a draft menu and calls onPublish", async () => {
    const { onPublish } = renderGrid(draftMenu());
    const user = userEvent.setup();

    expect(screen.getByText(messages.menu.statusDraft)).toBeTruthy();
    await user.click(screen.getByText(messages.menu.publish));

    expect(onPublish).toHaveBeenCalledTimes(1);
  });

  it("shows Un-publish (distinct from Publish) for a published menu and calls onUnpublish", async () => {
    const { onUnpublish, onPublish } = renderGrid({ ...draftMenu(), isPublished: true, publishedAt: "2027-06-01T08:00:00Z" });
    const user = userEvent.setup();

    expect(screen.getByText(messages.menu.statusPublished)).toBeTruthy();
    expect(screen.queryByText(messages.menu.publish)).toBeNull();

    await user.click(screen.getByText(messages.menu.unpublish));

    expect(onUnpublish).toHaveBeenCalledTimes(1);
    expect(onPublish).not.toHaveBeenCalled();
  });
});
