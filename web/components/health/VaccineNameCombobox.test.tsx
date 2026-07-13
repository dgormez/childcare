import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../../i18n/locales/en.json";
import { VaccineNameCombobox } from "./VaccineNameCombobox";
import type { CustomVaccineEntryResponse, VaccineTypeResponse } from "../../lib/types";

const vaccineTypes: VaccineTypeResponse[] = [
  { id: "vt-1", name: "DTPa-IPV-Hib-HepB", category: "basisvaccinatieschema", sortOrder: 1 },
  { id: "vt-2", name: "HPV", category: "basisvaccinatieschema", sortOrder: 5 },
  { id: "vt-3", name: "RSV (zuigelingen)", category: "aanbevolen_niet_gratis", sortOrder: 1 },
];

const customEntries: CustomVaccineEntryResponse[] = [{ id: "ce-1", name: "Rabiës" }];

function renderCombobox(overrides: Partial<Parameters<typeof VaccineNameCombobox>[0]> = {}) {
  const onChange = vi.fn();
  const onSelect = vi.fn();
  render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <label htmlFor="vaccine-name">Vaccine name</label>
      <VaccineNameCombobox
        id="vaccine-name"
        value=""
        vaccineTypes={vaccineTypes}
        customEntries={customEntries}
        onChange={onChange}
        onSelect={onSelect}
        {...overrides}
      />
    </NextIntlClientProvider>,
  );
  return { onChange, onSelect };
}

describe("VaccineNameCombobox", () => {
  it("shows catalog entries grouped by category, and custom entries under a separate group", async () => {
    renderCombobox();
    await userEvent.click(screen.getByLabelText("Vaccine name"));

    expect(screen.getByText("Basisvaccinatieschema")).toBeInTheDocument();
    expect(screen.getByText("Recommended, not free")).toBeInTheDocument();
    expect(screen.getByText("Other (used before)")).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "HPV" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Rabiës" })).toBeInTheDocument();
  });

  it("filters options as the user types", async () => {
    const { onChange } = renderCombobox({ value: "hp" });
    await userEvent.click(screen.getByLabelText("Vaccine name"));

    expect(screen.getByRole("option", { name: "HPV" })).toBeInTheDocument();
    expect(screen.queryByRole("option", { name: "Rabiës" })).not.toBeInTheDocument();
    expect(onChange).not.toHaveBeenCalled(); // no keystroke fired yet in this assertion
  });

  it("selecting a catalog option reports its vaccineTypeId", async () => {
    const { onSelect } = renderCombobox();
    await userEvent.click(screen.getByLabelText("Vaccine name"));
    await userEvent.click(screen.getByRole("option", { name: "HPV" }));

    expect(onSelect).toHaveBeenCalledWith({ name: "HPV", vaccineTypeId: "vt-2" });
  });

  it("selecting a custom entry reports a null vaccineTypeId", async () => {
    const { onSelect } = renderCombobox();
    await userEvent.click(screen.getByLabelText("Vaccine name"));
    await userEvent.click(screen.getByRole("option", { name: "Rabiës" }));

    expect(onSelect).toHaveBeenCalledWith({ name: "Rabiës", vaccineTypeId: null });
  });

  it("is fully keyboard-operable: arrow down then enter selects the first option", async () => {
    const { onSelect } = renderCombobox({ value: "hp" });
    const input = screen.getByLabelText("Vaccine name");
    await userEvent.click(input);
    await userEvent.keyboard("{ArrowDown}{Enter}");

    expect(onSelect).toHaveBeenCalledWith({ name: "HPV", vaccineTypeId: "vt-2" });
  });

  it("does not fold the open dropdown's option text into the input's accessible label", async () => {
    renderCombobox();
    await userEvent.click(screen.getByLabelText("Vaccine name"));
    // Regression guard: if the listbox were ever nested back inside a wrapping <label>,
    // this exact-match query would start failing again (see VaccineRecordForm.tsx's fix).
    expect(screen.getByLabelText("Vaccine name")).toBeInTheDocument();
  });
});
