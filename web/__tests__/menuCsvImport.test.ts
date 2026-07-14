import { describe, it, expect } from "vitest";
import type { DayFields } from "../components/menu/MonthlyMenuDayGrid";
import {
  buildMenuCsvTemplate,
  mergeMenuCsvRowsIntoGrid,
  parseMenuCsv,
  validateMenuCsvRows,
  type ValidatedMenuCsvRow,
} from "../lib/menu/csvImport";

function csvFile(content: string, name = "menu.csv"): File {
  return new File([content], name, { type: "text/csv" });
}

const emptyDays = new Map<string, DayFields>();

function blankFields(): DayFields {
  return { soup: "", mainCourse: "", dessert: "", notes: "" };
}

async function parseAndValidate(csv: string, year: number, month: number, currentDays = emptyDays): Promise<ValidatedMenuCsvRow[]> {
  const parsed = await parseMenuCsv(csvFile(csv));
  if ("fileLevelError" in parsed) throw new Error("expected rows, got a file-level error");
  return validateMenuCsvRows(parsed.rows, { year, month }, currentDays);
}

describe("parseMenuCsv + validateMenuCsvRows", () => {
  it("parses and validates a full valid month (US1)", async () => {
    const csv = [
      "date,soup,main_course,dessert,notes",
      "2027-06-01,Tomatensoep,Kip met puree,Yoghurt,",
      "2027-06-02,,Pasta bolognese,Fruit,Geen warme maaltijd",
    ].join("\n");

    const rows = await parseAndValidate(csv, 2027, 6);

    expect(rows).toHaveLength(2);
    expect(rows.every((r) => r.status === "valid")).toBe(true);
    const first = rows[0];
    if (first.status !== "valid") throw new Error("expected valid");
    expect(first.date).toBe("2027-06-01");
    expect(first.fields).toEqual({ soup: "Tomatensoep", mainCourse: "Kip met puree", dessert: "Yoghurt", notes: "" });
  });

  it("strips a leading UTF-8 BOM and matches headers case-insensitively/whitespace-tolerantly (FR-019, FR-020)", async () => {
    const csv = "﻿ Date , Soup ,Main_Course,Dessert,Notes\n2027-06-01,Tomatensoep,Kip met puree,Yoghurt,";
    const rows = await parseAndValidate(csv, 2027, 6);

    expect(rows).toHaveLength(1);
    expect(rows[0].status).toBe("valid");
  });

  it("flags an unparseable/wrong-format date as invalid_date and never guesses at DD/MM/YYYY (FR-005)", async () => {
    const csv = "date,soup,main_course,dessert,notes\n01/06/2027,Soep,Vis,Pudding,";
    const rows = await parseAndValidate(csv, 2027, 6);

    expect(rows[0]).toMatchObject({ status: "invalid", errorReason: "invalid_date" });
  });

  it("rejects a calendar-overflow date like 2027-02-30 rather than silently rolling it forward", async () => {
    const csv = "date,soup,main_course,dessert,notes\n2027-02-30,Soep,Vis,Pudding,";
    const rows = await parseAndValidate(csv, 2027, 2);

    expect(rows[0]).toMatchObject({ status: "invalid", errorReason: "invalid_date" });
  });

  it("flags an out-of-range date as date_out_of_range", async () => {
    const csv = "date,soup,main_course,dessert,notes\n2027-07-01,Soep,Vis,Pudding,";
    const rows = await parseAndValidate(csv, 2027, 6);

    expect(rows[0]).toMatchObject({ status: "invalid", errorReason: "date_out_of_range" });
  });

  it("flags duplicate dates within the same file as duplicate_date, both excluded, others unaffected", async () => {
    const csv = [
      "date,soup,main_course,dessert,notes",
      "2027-06-01,Soep A,Vis,Pudding,",
      "2027-06-01,Soep B,Kip,Yoghurt,",
      "2027-06-02,Soep C,Pasta,Fruit,",
    ].join("\n");
    const rows = await parseAndValidate(csv, 2027, 6);

    expect(rows[0]).toMatchObject({ status: "invalid", errorReason: "duplicate_date" });
    expect(rows[1]).toMatchObject({ status: "invalid", errorReason: "duplicate_date" });
    expect(rows[2]).toMatchObject({ status: "valid", date: "2027-06-02" });
  });

  it("flags a field over 500 characters as field_too_long", async () => {
    const longNote = "x".repeat(501);
    const csv = `date,soup,main_course,dessert,notes\n2027-06-01,Soep,Vis,Pudding,${longNote}`;
    const rows = await parseAndValidate(csv, 2027, 6);

    expect(rows[0]).toMatchObject({ status: "invalid", errorReason: "field_too_long" });
  });

  it("applies the FR-022 precedence order when a row could match more than one condition", async () => {
    // An unparseable date row that also happens to repeat elsewhere must be reported as
    // invalid_date, never duplicate_date - duplicate detection only runs over rows that
    // already passed the date-format and range checks.
    const csv = [
      "date,soup,main_course,dessert,notes",
      "not-a-date,Soep,Vis,Pudding,",
      "not-a-date,Soep,Vis,Pudding,",
    ].join("\n");
    const rows = await parseAndValidate(csv, 2027, 6);

    expect(rows[0]).toMatchObject({ status: "invalid", errorReason: "invalid_date" });
    expect(rows[1]).toMatchObject({ status: "invalid", errorReason: "invalid_date" });
  });

  it("treats a row with a valid date and every other field blank as valid (FR-023)", async () => {
    const csv = "date,soup,main_course,dessert,notes\n2027-06-01,,,,";
    const rows = await parseAndValidate(csv, 2027, 6);

    expect(rows[0]).toMatchObject({ status: "valid", date: "2027-06-01", fields: blankFields() });
  });

  it("flags a row with a mismatched column count as invalid rather than aborting the file (FR-021)", async () => {
    const csv = ["date,soup,main_course,dessert,notes", "2027-06-01,Soep", "2027-06-02,Soep,Vis,Pudding,geen extra"].join("\n");
    const rows = await parseAndValidate(csv, 2027, 6);

    expect(rows[0]).toMatchObject({ status: "invalid", errorReason: "malformed_row" });
    expect(rows[1]).toMatchObject({ status: "valid", date: "2027-06-02" });
  });

  it("sets willOverwriteExisting only for valid rows whose date already has non-blank grid content (FR-024)", async () => {
    const currentDays = new Map<string, DayFields>([
      ["2027-06-01", { soup: "Bestaande soep", mainCourse: "", dessert: "", notes: "" }],
      ["2027-06-02", blankFields()],
    ]);
    const csv = ["date,soup,main_course,dessert,notes", "2027-06-01,Nieuwe soep,,,", "2027-06-02,Nieuwe soep,,,"].join("\n");
    const rows = await parseAndValidate(csv, 2027, 6, currentDays);

    const [day1, day2] = rows;
    if (day1.status !== "valid" || day2.status !== "valid") throw new Error("expected both valid");
    expect(day1.willOverwriteExisting).toBe(true);
    expect(day2.willOverwriteExisting).toBe(false);
  });

  it("returns a file-level error for a non-CSV/binary file rather than processing garbage rows (FR-015)", async () => {
    const parsed = await parseMenuCsv(new File([new Uint8Array([0x00, 0xff, 0x00, 0xff])], "menu.csv"));
    expect("fileLevelError" in parsed).toBe(true);
  });

  it("returns a file-level error when the header row doesn't contain a recognizable date column", async () => {
    const parsed = await parseMenuCsv(csvFile("not,a,menu,file\n1,2,3,4"));
    expect("fileLevelError" in parsed).toBe(true);
  });
});

describe("mergeMenuCsvRowsIntoGrid", () => {
  it("overwrites only the dates present in validRows, leaving every other date untouched (FR-012)", () => {
    const current = new Map<string, DayFields>([
      ["2027-06-01", { soup: "Oude soep", mainCourse: "Oud gerecht", dessert: "", notes: "" }],
      ["2027-06-02", { soup: "Ongewijzigd", mainCourse: "", dessert: "", notes: "" }],
    ]);
    const validRows: ValidatedMenuCsvRow[] = [
      { status: "valid", date: "2027-06-01", fields: { soup: "Nieuwe soep", mainCourse: "", dessert: "", notes: "" }, willOverwriteExisting: true, rowNumber: 1 },
    ];

    const merged = mergeMenuCsvRowsIntoGrid(current, validRows);

    expect(merged.get("2027-06-01")).toEqual({ soup: "Nieuwe soep", mainCourse: "", dessert: "", notes: "" });
    expect(merged.get("2027-06-02")).toEqual({ soup: "Ongewijzigd", mainCourse: "", dessert: "", notes: "" });
    expect(current.get("2027-06-01")?.soup).toBe("Oude soep"); // input map not mutated
  });

  it("applies only the valid rows from a mixed valid/invalid batch, leaving invalid rows' dates untouched", async () => {
    const current = new Map<string, DayFields>([["2027-06-01", blankFields()]]);
    const csv = ["date,soup,main_course,dessert,notes", "2027-06-01,Soep,Vis,Pudding,", "not-a-date,X,Y,Z,"].join("\n");
    const validated = await parseAndValidate(csv, 2027, 6, current);

    const merged = mergeMenuCsvRowsIntoGrid(current, validated);

    expect(merged.get("2027-06-01")).toEqual({ soup: "Soep", mainCourse: "Vis", dessert: "Pudding", notes: "" });
    expect(merged.size).toBe(1);
  });

  it("composes two sequential imports before Save: the second import's matching dates overwrite the first's, non-matching dates remain (FR-025)", async () => {
    const original = new Map<string, DayFields>();
    const firstCsv = ["date,soup,main_course,dessert,notes", "2027-06-01,Eerste,X,Y,", "2027-06-02,Onaangeraakt,X,Y,"].join("\n");
    const firstValidated = await parseAndValidate(firstCsv, 2027, 6, original);
    const afterFirstImport = mergeMenuCsvRowsIntoGrid(original, firstValidated);

    const secondCsv = "date,soup,main_course,dessert,notes\n2027-06-01,Tweede,X,Y,";
    const secondValidated = await parseAndValidate(secondCsv, 2027, 6, afterFirstImport);
    const afterSecondImport = mergeMenuCsvRowsIntoGrid(afterFirstImport, secondValidated);

    expect(afterSecondImport.get("2027-06-01")?.soup).toBe("Tweede");
    expect(afterSecondImport.get("2027-06-02")?.soup).toBe("Onaangeraakt");
  });
});

describe("buildMenuCsvTemplate", () => {
  it("produces the expected header row plus one example row dated within the given year/month", () => {
    const csv = buildMenuCsvTemplate(2027, 6);
    expect(csv.split(/\r\n|\n/)[0]).toBe("date,soup,main_course,dessert,notes");
    expect(csv).toContain("2027-06-01");
  });

  it("round-trips as fully valid through parseMenuCsv + validateMenuCsvRows (SC-003)", async () => {
    const csv = buildMenuCsvTemplate(2027, 6);
    const rows = await parseAndValidate(csv, 2027, 6);

    expect(rows.length).toBeGreaterThan(0);
    expect(rows.every((r) => r.status === "valid")).toBe(true);
  });
});
