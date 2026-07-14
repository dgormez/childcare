"use client";
import { useEffect, useId, useRef, useState } from "react";
import { useTranslations } from "next-intl";
import { AlertTriangle, Download, Replace, Upload } from "lucide-react";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "../ui/dialog";
import { Button } from "../ui/button";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "../ui/table";
import { Badge } from "../ui/badge";
import type { DayFields } from "./MonthlyMenuDayGrid";
import {
  buildMenuCsvImportResult,
  buildMenuCsvTemplate,
  mergeMenuCsvRowsIntoGrid,
  parseMenuCsv,
  validateMenuCsvRows,
  type MenuCsvImportResult,
} from "../../lib/menu/csvImport";

interface MonthlyMenuCsvImportDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  year: number;
  month: number;
  currentDays: Map<string, DayFields>;
  onImport: (merged: Map<string, DayFields>) => void;
}

type DialogState = { status: "idle" } | { status: "result"; result: MenuCsvImportResult } | { status: "fileError" };

/**
 * CSV bulk-import for the monthly menu day grid (feature 013i). Client-side only — parses,
 * validates, and previews entirely in the browser; the eventual write is the grid's existing
 * Save action (013e), never a new endpoint. Scoped to the year/month it was opened with
 * (FR-026): if the director changes location/month while this is open, it closes without
 * merging rather than risk applying a stale preview to the wrong month.
 */
export function MonthlyMenuCsvImportDialog({ open, onOpenChange, year, month, currentDays, onImport }: MonthlyMenuCsvImportDialogProps) {
  const t = useTranslations("menu");
  const inputId = useId();
  const [scope, setScope] = useState({ year, month });
  const [state, setState] = useState<DialogState>({ status: "idle" });
  const summaryRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (open) {
      setScope({ year, month });
      setState({ status: "idle" });
    }
    // Only re-capture scope when the dialog transitions open; live year/month changes while
    // open are handled by the effect below, which closes the dialog instead of re-scoping it.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  useEffect(() => {
    if (open && (year !== scope.year || month !== scope.month)) {
      onOpenChange(false);
    }
  }, [open, year, month, scope, onOpenChange]);

  useEffect(() => {
    if (state.status === "result") {
      summaryRef.current?.focus();
    }
  }, [state]);

  async function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    e.target.value = "";
    if (!file) return;

    const parsed = await parseMenuCsv(file);
    if ("fileLevelError" in parsed) {
      setState({ status: "fileError" });
      return;
    }
    const validated = validateMenuCsvRows(parsed.rows, { year: scope.year, month: scope.month }, currentDays);
    setState({ status: "result", result: buildMenuCsvImportResult(validated) });
  }

  function handleDownloadTemplate() {
    const csv = buildMenuCsvTemplate(scope.year, scope.month);
    const blob = new Blob([csv], { type: "text/csv;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `menu-${scope.year}-${String(scope.month).padStart(2, "0")}-template.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }

  function handleConfirm() {
    if (state.status !== "result") return;
    onImport(mergeMenuCsvRowsIntoGrid(currentDays, state.result.rows));
    onOpenChange(false);
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>{t("csvImport.title")}</DialogTitle>
          <DialogDescription>{t("csvImport.description")}</DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div className="flex items-center gap-3">
            <label
              htmlFor={inputId}
              className="inline-flex cursor-pointer items-center gap-2 rounded-lg border border-border px-3 py-2 text-sm font-medium text-text hover:bg-surface-soft focus-within:outline-none focus-within:ring-2 focus-within:ring-primary dark:border-border-dark dark:text-text-dark dark:hover:bg-surface-soft-dark"
            >
              <Upload className="h-4 w-4" strokeWidth={2} />
              {t("csvImport.selectFile")}
            </label>
            <input id={inputId} type="file" accept=".csv,text/csv" className="sr-only" onChange={handleFileChange} />
            <Button variant="secondary" type="button" onClick={handleDownloadTemplate}>
              <Download className="h-4 w-4" strokeWidth={2} />
              {t("csvImport.downloadTemplate")}
            </Button>
          </div>

          {state.status === "fileError" && (
            <div
              role="alert"
              className="flex items-center gap-2 rounded-lg bg-danger-bg px-3 py-2 text-sm text-danger dark:bg-danger-bg-dark dark:text-danger-dark"
            >
              <AlertTriangle className="h-4 w-4 shrink-0" strokeWidth={2} />
              {t("csvImport.fileError")}
            </div>
          )}

          {state.status === "result" && (
            <div>
              <div
                ref={summaryRef}
                tabIndex={-1}
                role="status"
                className="mb-3 text-sm font-medium text-text focus:outline-none dark:text-text-dark"
              >
                {t("csvImport.summary", { applied: state.result.validCount, skipped: state.result.invalidCount })}
              </div>
              {state.result.validCount === 0 ? (
                <div
                  role="alert"
                  className="flex items-center gap-2 rounded-lg bg-danger-bg px-3 py-2 text-sm text-danger dark:bg-danger-bg-dark dark:text-danger-dark"
                >
                  <AlertTriangle className="h-4 w-4 shrink-0" strokeWidth={2} />
                  {t("csvImport.noValidRows")}
                </div>
              ) : (
                <div className="max-h-80 overflow-y-auto">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>{t("columnDate")}</TableHead>
                        <TableHead>{t("csvImport.columnStatus")}</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {state.result.rows.map((row) => (
                        <TableRow key={row.rowNumber}>
                          <TableCell className="whitespace-nowrap font-medium">
                            {row.status === "valid" ? row.date : row.rawDate || t("csvImport.blankDate")}
                          </TableCell>
                          <TableCell>
                            {row.status === "invalid" ? (
                              <Badge variant="danger" className="inline-flex items-center gap-1">
                                <AlertTriangle className="h-3 w-3" strokeWidth={2} />
                                {t(`csvImport.error.${row.errorReason}`)}
                              </Badge>
                            ) : row.willOverwriteExisting ? (
                              <Badge variant="warning" className="inline-flex items-center gap-1">
                                <Replace className="h-3 w-3" strokeWidth={2} />
                                {t("csvImport.willOverwrite")}
                              </Badge>
                            ) : (
                              <Badge variant="success">{t("csvImport.willApply")}</Badge>
                            )}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              )}
            </div>
          )}
        </div>

        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)}>
            {t("csvImport.cancel")}
          </Button>
          <Button onClick={handleConfirm} disabled={state.status !== "result" || state.result.validCount === 0}>
            {t("csvImport.confirm")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
