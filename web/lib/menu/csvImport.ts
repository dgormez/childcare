import Papa from "papaparse";
import type { DayFields } from "../../components/menu/MonthlyMenuDayGrid";

// Matches UpsertMonthlyMenuCommandValidator's MaximumLength(500) rules on the backend
// (backend/ChildCare.Application/MonthlyMenus/UpsertMonthlyMenuCommand.cs). No shared contract
// generation exists between the two yet — see research.md's "Field-length validation source of
// truth" decision. If the server-side limit ever changes, update both together.
export const MAX_FIELD_LENGTH = 500;

const RECOGNIZED_COLUMNS = ["date", "soup", "main_course", "dessert", "notes"] as const;
const DATE_FORMAT = /^\d{4}-\d{2}-\d{2}$/;

export interface ParsedMenuCsvRow {
  rawDate: string;
  soup?: string;
  mainCourse?: string;
  dessert?: string;
  notes?: string;
  rowNumber: number;
  /** True when this row's column count didn't match the header row (FR-021) — checked before
   * every other validation, regardless of whether the date happens to still look parseable. */
  columnMismatch: boolean;
}

export type MenuCsvErrorReason = "malformed_row" | "invalid_date" | "date_out_of_range" | "duplicate_date" | "field_too_long";

export type ValidatedMenuCsvRow =
  | {
      status: "valid";
      date: string;
      fields: DayFields;
      willOverwriteExisting: boolean;
      rowNumber: number;
    }
  | {
      status: "invalid";
      errorReason: MenuCsvErrorReason;
      rowNumber: number;
      rawDate: string;
    };

export interface MenuCsvImportResult {
  rows: ValidatedMenuCsvRow[];
  validCount: number;
  invalidCount: number;
  fileLevelError: null;
}

// A leading UTF-8 byte-order-mark is the common case for a file exported directly from Excel
// (FR-020) — stripped before parsing so it never corrupts the header's first column name.
const BOM = "﻿";

/**
 * Reads and parses a CSV File entirely client-side (FR-003). Header matching is
 * case-insensitive and whitespace-tolerant (FR-019); unrecognized columns are ignored (FR-004).
 * A file with no discernible CSV structure (wrong delimiter, missing header, binary content)
 * produces a file-level error instead of rows (FR-015).
 */
export async function parseMenuCsv(file: File): Promise<{ rows: ParsedMenuCsvRow[] } | { fileLevelError: string }> {
  let text = await file.text();
  if (text.startsWith(BOM)) {
    text = text.slice(BOM.length);
  }

  // A non-text/binary file decodes with replacement characters (U+FFFD) — treated as
  // unparseable rather than fed to the CSV parser as garbage rows.
  if (text.includes("�")) {
    return { fileLevelError: "unparseable_file" };
  }

  const result = Papa.parse<Record<string, string>>(text, {
    header: true,
    skipEmptyLines: true,
    transformHeader: (header) => header.trim().toLowerCase(),
  });

  const fields = result.meta.fields ?? [];
  if (!fields.includes("date")) {
    // Wrong delimiter (whole header collapses into one field) or a missing header row
    // (the first data row was read as headers and doesn't contain "date") both surface here.
    return { fileLevelError: "unparseable_file" };
  }

  const mismatchedRowIndexes = new Set(
    result.errors.filter((e) => e.type === "FieldMismatch" && e.row !== undefined).map((e) => e.row!),
  );

  const rows: ParsedMenuCsvRow[] = result.data.map((raw, index) => ({
    rawDate: (raw.date ?? "").trim(),
    soup: raw.soup,
    mainCourse: raw.main_course,
    dessert: raw.dessert,
    notes: raw.notes,
    rowNumber: index + 1,
    columnMismatch: mismatchedRowIndexes.has(index),
  }));

  return { rows };
}

function isRealCalendarDate(value: string): boolean {
  if (!DATE_FORMAT.test(value)) return false;
  const [year, month, day] = value.split("-").map(Number);
  const parsed = new Date(Date.UTC(year, month - 1, day));
  // Date rolls an out-of-range day/month forward (e.g. 2026-02-30 -> 2026-03-02) instead of
  // throwing — comparing the round-tripped components catches that silently-wrong case.
  return parsed.getUTCFullYear() === year && parsed.getUTCMonth() === month - 1 && parsed.getUTCDate() === day;
}

function fieldValueOrUndefined(value: string | undefined): string | undefined {
  const trimmed = value?.trim();
  return trimmed ? trimmed : undefined;
}

function hasNonBlankContent(fields: DayFields | undefined): boolean {
  if (!fields) return false;
  return Boolean(fields.soup.trim() || fields.mainCourse.trim() || fields.dessert.trim() || fields.notes.trim());
}

/**
 * Validates parsed rows in FR-022's fixed precedence order (malformed row -> date
 * format/parseability -> out-of-range -> duplicate-within-file -> field length), so a row
 * failing more than one check is still reported with exactly one reason. `currentDays` is the
 * grid's in-memory state at the moment of upload, used to compute `willOverwriteExisting`
 * (FR-024) — see research.md's "Overwrite-visibility computation" decision.
 */
export function validateMenuCsvRows(
  rows: ParsedMenuCsvRow[],
  { year, month }: { year: number; month: number },
  currentDays: Map<string, DayFields>,
): ValidatedMenuCsvRow[] {
  const results = new Map<number, ValidatedMenuCsvRow>();
  const candidateDates = new Map<number, string>();

  for (const row of rows) {
    if (row.columnMismatch) {
      results.set(row.rowNumber, { status: "invalid", errorReason: "malformed_row", rowNumber: row.rowNumber, rawDate: row.rawDate });
      continue;
    }
    if (!isRealCalendarDate(row.rawDate)) {
      results.set(row.rowNumber, { status: "invalid", errorReason: "invalid_date", rowNumber: row.rowNumber, rawDate: row.rawDate });
      continue;
    }
    const [rowYear, rowMonth] = row.rawDate.split("-").map(Number);
    if (rowYear !== year || rowMonth !== month) {
      results.set(row.rowNumber, { status: "invalid", errorReason: "date_out_of_range", rowNumber: row.rowNumber, rawDate: row.rawDate });
      continue;
    }
    candidateDates.set(row.rowNumber, row.rawDate);
  }

  const dateCounts = new Map<string, number>();
  for (const date of candidateDates.values()) {
    dateCounts.set(date, (dateCounts.get(date) ?? 0) + 1);
  }
  const duplicateRowNumbers = new Set<number>();
  for (const [rowNumber, date] of candidateDates) {
    if ((dateCounts.get(date) ?? 0) > 1) {
      results.set(rowNumber, { status: "invalid", errorReason: "duplicate_date", rowNumber, rawDate: date });
      duplicateRowNumbers.add(rowNumber);
    }
  }

  for (const row of rows) {
    if (results.has(row.rowNumber) || !candidateDates.has(row.rowNumber) || duplicateRowNumbers.has(row.rowNumber)) continue;

    const fields: DayFields = {
      soup: row.soup?.trim() ?? "",
      mainCourse: row.mainCourse?.trim() ?? "",
      dessert: row.dessert?.trim() ?? "",
      notes: row.notes?.trim() ?? "",
    };
    const tooLong = [fields.soup, fields.mainCourse, fields.dessert, fields.notes].some((v) => v.length > MAX_FIELD_LENGTH);
    if (tooLong) {
      results.set(row.rowNumber, { status: "invalid", errorReason: "field_too_long", rowNumber: row.rowNumber, rawDate: row.rawDate });
      continue;
    }

    const date = candidateDates.get(row.rowNumber)!;
    results.set(row.rowNumber, {
      status: "valid",
      date,
      fields,
      willOverwriteExisting: hasNonBlankContent(currentDays.get(date)),
      rowNumber: row.rowNumber,
    });
  }

  return rows.map((row) => results.get(row.rowNumber)!);
}

export function buildMenuCsvImportResult(rows: ValidatedMenuCsvRow[]): MenuCsvImportResult {
  return {
    rows,
    validCount: rows.filter((r) => r.status === "valid").length,
    invalidCount: rows.filter((r) => r.status === "invalid").length,
    fileLevelError: null,
  };
}

/**
 * Overwrites only the grid's day rows whose dates matched a valid CSV row (FR-012) — including
 * a blank imported field clearing existing non-blank content (whole-day overwrite-by-date, not
 * a partial per-field merge). Returns a new map; does not mutate `currentDays`, so importing
 * twice before Save composes the second import on top of the first's already-merged state
 * (FR-025) as long as the caller passes the previous call's return value back in.
 */
export function mergeMenuCsvRowsIntoGrid(currentDays: Map<string, DayFields>, validRows: ValidatedMenuCsvRow[]): Map<string, DayFields> {
  const next = new Map(currentDays);
  for (const row of validRows) {
    if (row.status !== "valid") continue;
    next.set(row.date, row.fields);
  }
  return next;
}

/** Header row plus one example data row dated the 1st of the given month (FR-016), matching
 * `parseMenuCsv`/`validateMenuCsvRows`'s own expected format exactly (SC-003's round-trip
 * guarantee — see research.md and `contracts/csv-format.md`). */
export function buildMenuCsvTemplate(year: number, month: number): string {
  const monthStr = String(month).padStart(2, "0");
  const exampleDate = `${year}-${monthStr}-01`;
  return Papa.unparse({
    fields: [...RECOGNIZED_COLUMNS],
    data: [[exampleDate, "Tomatensoep", "Kip met puree", "Yoghurt", ""]],
  });
}
