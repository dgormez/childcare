import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import { MonthlyMenuCsvImportDialog } from "../components/menu/MonthlyMenuCsvImportDialog";
import type { DayFields } from "../components/menu/MonthlyMenuDayGrid";

function csvFile(content: string): File {
  return new File([content], "menu.csv", { type: "text/csv" });
}

function blankFields(): DayFields {
  return { lunchMeal: "", alternativeLunchMeal: "", snack: "", notes: "" };
}

function renderDialog(overrides: Partial<{ year: number; month: number; currentDays: Map<string, DayFields>; open: boolean }> = {}) {
  const onOpenChange = vi.fn();
  const onImport = vi.fn();
  const props = {
    open: overrides.open ?? true,
    onOpenChange,
    year: overrides.year ?? 2027,
    month: overrides.month ?? 6,
    currentDays: overrides.currentDays ?? new Map<string, DayFields>(),
    onImport,
  };

  const utils = render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <MonthlyMenuCsvImportDialog {...props} />
    </NextIntlClientProvider>,
  );

  return { onOpenChange, onImport, ...utils };
}

async function uploadFile(content: string) {
  const user = userEvent.setup();
  const input = document.querySelector('input[type="file"]') as HTMLInputElement;
  await user.upload(input, csvFile(content));
  return user;
}

beforeEach(() => {
  vi.stubGlobal("URL", { ...URL, createObjectURL: vi.fn(() => "blob:mock"), revokeObjectURL: vi.fn() });
});

describe("MonthlyMenuCsvImportDialog", () => {
  it("uploading a full valid-month CSV shows an all-valid preview and confirming merges it (US1)", async () => {
    const { onImport, onOpenChange } = renderDialog();
    const user = await uploadFile(
      ["date,lunch_meal,alternative_lunch_meal,snack,notes", "2027-06-01,Tomatensoep,Kip met puree,Yoghurt,"].join("\n"),
    );

    expect(await screen.findByText(messages.menu.csvImport.willApply)).toBeTruthy();
    expect(screen.getByText("1 row will apply, 0 skipped")).toBeTruthy();

    await user.click(screen.getByText(messages.menu.csvImport.confirm));

    expect(onImport).toHaveBeenCalledTimes(1);
    const merged = onImport.mock.calls[0][0] as Map<string, DayFields>;
    expect(merged.get("2027-06-01")).toEqual({ lunchMeal: "Tomatensoep", alternativeLunchMeal: "Kip met puree", snack: "Yoghurt", notes: "" });
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it("shows per-row reasons and the summary count for a mixed valid/invalid CSV, and confirming applies only the valid rows", async () => {
    const { onImport } = renderDialog();
    const user = await uploadFile(
      ["date,lunch_meal,alternative_lunch_meal,snack,notes", "2027-06-01,Soep,Vis,Pudding,", "not-a-date,X,Y,Z,"].join("\n"),
    );

    expect(await screen.findByText("1 row will apply, 1 skipped")).toBeTruthy();
    expect(screen.getByText(messages.menu.csvImport.error.invalid_date)).toBeTruthy();

    await user.click(screen.getByText(messages.menu.csvImport.confirm));

    const merged = onImport.mock.calls[0][0] as Map<string, DayFields>;
    expect(merged.size).toBe(1);
    expect(merged.has("2027-06-01")).toBe(true);
  });

  it("shows the overwrite indicator on a preview row that would replace existing non-blank content (FR-024, SC-005)", async () => {
    const currentDays = new Map<string, DayFields>([["2027-06-01", { lunchMeal: "Bestaande soep", alternativeLunchMeal: "", snack: "", notes: "" }]]);
    renderDialog({ currentDays });
    await uploadFile("date,lunch_meal,alternative_lunch_meal,snack,notes\n2027-06-01,Nieuwe soep,,,");

    expect(await screen.findByText(messages.menu.csvImport.willOverwrite)).toBeTruthy();
  });

  it("shows a rejection and disables Confirm for a CSV with zero valid rows, and leaves the grid untouched", async () => {
    const { onImport } = renderDialog();
    await uploadFile("date,lunch_meal,alternative_lunch_meal,snack,notes\nnot-a-date,X,Y,Z,");

    expect(await screen.findByText(messages.menu.csvImport.noValidRows)).toBeTruthy();
    expect(screen.getByText(messages.menu.csvImport.confirm).closest("button")).toBeDisabled();

    expect(onImport).not.toHaveBeenCalled();
  });

  it("shows a distinct top-level error for a malformed/non-CSV file and leaves the grid untouched", async () => {
    const { onImport } = renderDialog();
    const user = userEvent.setup();
    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    await user.upload(input, new File([new Uint8Array([0x00, 0xff, 0x00, 0xff])], "menu.csv"));

    expect(await screen.findByText(messages.menu.csvImport.fileError)).toBeTruthy();
    expect(screen.queryByText(messages.menu.csvImport.noValidRows)).toBeNull();
    expect(onImport).not.toHaveBeenCalled();
  });

  it("clicking Download template triggers a download whose content matches buildMenuCsvTemplate's output", async () => {
    const clickSpy = vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => {});
    const user = userEvent.setup();
    renderDialog({ year: 2027, month: 6 });

    await user.click(screen.getByText(messages.menu.csvImport.downloadTemplate));

    expect(clickSpy).toHaveBeenCalledTimes(1);
    expect(URL.createObjectURL).toHaveBeenCalledTimes(1);
    const blob = (URL.createObjectURL as ReturnType<typeof vi.fn>).mock.calls[0][0] as Blob;
    const text = await blob.text();
    expect(text.split(/\r\n|\n/)[0]).toBe("date,lunch_meal,alternative_lunch_meal,snack,notes");
    expect(text).toContain("2027-06-01");

    clickSpy.mockRestore();
  });

  it("closes without merging anything when the selected month changes while it is open (FR-026)", async () => {
    const onOpenChange = vi.fn();
    const onImport = vi.fn();
    const currentDays = new Map<string, DayFields>();

    const { rerender } = render(
      <NextIntlClientProvider locale="en" messages={messages}>
        <MonthlyMenuCsvImportDialog open year={2027} month={6} currentDays={currentDays} onOpenChange={onOpenChange} onImport={onImport} />
      </NextIntlClientProvider>,
    );

    rerender(
      <NextIntlClientProvider locale="en" messages={messages}>
        <MonthlyMenuCsvImportDialog open year={2027} month={7} currentDays={currentDays} onOpenChange={onOpenChange} onImport={onImport} />
      </NextIntlClientProvider>,
    );

    expect(onOpenChange).toHaveBeenCalledWith(false);
    expect(onImport).not.toHaveBeenCalled();
  });

  it("Cancel closes the dialog without importing", async () => {
    const { onOpenChange, onImport } = renderDialog();
    const user = userEvent.setup();

    await user.click(screen.getByText(messages.menu.csvImport.cancel));

    expect(onOpenChange).toHaveBeenCalledWith(false);
    expect(onImport).not.toHaveBeenCalled();
  });
});
