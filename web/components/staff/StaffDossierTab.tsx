"use client";
import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { FileText, Trash2 } from "lucide-react";
import { apiClient } from "../../lib/apiClient";
import { Button } from "../ui/button";
import { EmptyState } from "../EmptyState";
import { ErrorState } from "../ErrorState";
import { StaffDocumentForm } from "./StaffDocumentForm";
import { TimeEntryFunctionsForm } from "./TimeEntryFunctionsForm";
import type { StaffDocumentResponse, StaffTimeEntryFunction } from "../../lib/types";

type LoadState = "loading" | "loaded" | "error";

/**
 * Director HR dossier for one staff member (spec.md FR-011/FR-012/FR-013, User Story 3) — the
 * default/first tab on the staff detail screen (research.md R9, spec.md SC-002).
 */
export function StaffDossierTab({
  staffProfileId,
  timeEntryFunctions,
}: {
  staffProfileId: string;
  timeEntryFunctions: StaffTimeEntryFunction[];
}) {
  const t = useTranslations("staff.dossier");
  const [documents, setDocuments] = useState<StaffDocumentResponse[]>([]);
  const [state, setState] = useState<LoadState>("loading");
  const [formOpen, setFormOpen] = useState(false);

  const load = useCallback(async () => {
    setState("loading");
    const result = await apiClient.GET("/api/staff/{id}/documents", { params: { path: { id: staffProfileId } } });
    if (!result.response.ok) {
      setState("error");
      return;
    }
    setDocuments(result.data as unknown as StaffDocumentResponse[]);
    setState("loaded");
  }, [staffProfileId]);

  useEffect(() => {
    load();
  }, [load]);

  async function handleDelete(document: StaffDocumentResponse) {
    const result = await apiClient.DELETE("/api/staff/{id}/documents/{documentId}", {
      params: { path: { id: staffProfileId, documentId: document.id } },
    });
    if (result.response.ok) load();
  }

  return (
    <div className="space-y-6">
      <TimeEntryFunctionsForm staffProfileId={staffProfileId} initialFunctions={timeEntryFunctions} />

      <div>
        <div className="mb-3 flex items-center justify-between">
          <h3 className="text-sm font-semibold text-text dark:text-text-dark">{t("documentsTitle")}</h3>
          <Button size="sm" onClick={() => setFormOpen(true)}>
            {t("addDocument")}
          </Button>
        </div>

        {state === "loading" && <div className="h-32 animate-pulse rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />}
        {state === "error" && <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />}
        {state === "loaded" && documents.length === 0 && <EmptyState icon={FileText} message={t("emptyState")} />}
        {state === "loaded" && documents.length > 0 && (
          <ul className="divide-y divide-border rounded-xl border border-border dark:divide-border-dark dark:border-border-dark">
            {documents.map((document) => (
              <li key={document.id} className="flex h-12 items-center justify-between px-4">
                <div>
                  <a
                    href={document.downloadUrl ?? undefined}
                    target="_blank"
                    rel="noreferrer"
                    className="text-sm text-primary-hover hover:underline dark:text-primary-hover-dark"
                  >
                    {document.title}
                  </a>
                  <span className="ml-2 text-xs text-text-soft dark:text-text-soft-dark">
                    {t(`form.documentTypes.${document.documentType}`)}
                    {document.validUntil ? ` — ${t("validUntilShort")} ${document.validUntil}` : ""}
                  </span>
                </div>
                <button
                  type="button"
                  onClick={() => handleDelete(document)}
                  aria-label={t("deleteDocument")}
                  className="text-text-soft hover:text-danger dark:text-text-soft-dark dark:hover:text-danger-dark"
                >
                  <Trash2 className="h-4 w-4" strokeWidth={2} />
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>

      <StaffDocumentForm
        staffProfileId={staffProfileId}
        open={formOpen}
        onOpenChange={setFormOpen}
        onSaved={() => {
          setFormOpen(false);
          load();
        }}
      />
    </div>
  );
}
