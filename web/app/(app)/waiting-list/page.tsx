"use client";
import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { ListPlus, Plus } from "lucide-react";
import { apiClient } from "../../../lib/apiClient";
import { WaitingListTable } from "../../../components/WaitingListTable";
import { WaitingListEntryDialog, type WaitingListEntryFormValues } from "../../../components/WaitingListEntryDialog";
import { EnrollChildLinkDialog } from "../../../components/EnrollChildLinkDialog";
import { LinkContactDialog } from "../../../components/children/LinkContactDialog";
import { OccupancyPanel } from "../../../components/OccupancyPanel";
import { EmptyState } from "../../../components/EmptyState";
import { ErrorState } from "../../../components/ErrorState";
import { Button } from "../../../components/ui/button";
import type { ApiErrorBody, LocationResponse, WaitingListEntryResponse, WaitingListStatus } from "../../../lib/types";

type LoadState = "loading" | "loaded" | "error";
type StatusFilter = "waiting" | "all" | WaitingListStatus;

// Feature 023 (FR-014): WaitingListEntry only stores a single "contact name" field, but the
// contact-creation flow (feature 006/030's LinkContactDialog) wants first/last separately — a
// best-effort split (first token = first name, remainder = last name) still saves the director
// from retyping the common case, without inventing a first/last split on the entry itself.
function splitContactName(fullName: string): { firstName: string; lastName: string } {
  const trimmed = fullName.trim();
  const spaceIndex = trimmed.indexOf(" ");
  if (spaceIndex === -1) return { firstName: trimmed, lastName: "" };
  return { firstName: trimmed.slice(0, spaceIndex), lastName: trimmed.slice(spaceIndex + 1).trim() };
}

// Deliberately avoids Date.toISOString() (feature 012 precedent: shifts the date backward by
// a day in positive-UTC-offset timezones) — builds the string from local date components.
function todayDateString(): string {
  const d = new Date();
  const year = d.getFullYear();
  const month = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

export default function WaitingListPage() {
  const t = useTranslations("waitingList");
  const [locations, setLocations] = useState<LocationResponse[]>([]);
  const [locationId, setLocationId] = useState("");
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("waiting");
  const [entries, setEntries] = useState<WaitingListEntryResponse[]>([]);
  const [state, setState] = useState<LoadState>("loading");
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<WaitingListEntryResponse | null>(null);
  const [saving, setSaving] = useState(false);
  const [linkTarget, setLinkTarget] = useState<WaitingListEntryResponse | null>(null);
  const [contactPrefillTarget, setContactPrefillTarget] = useState<{ childId: string; entry: WaitingListEntryResponse } | null>(null);
  const [occupancyDate, setOccupancyDate] = useState(todayDateString());
  const [notice, setNotice] = useState("");

  useEffect(() => {
    (apiClient.GET as any)("/api/locations").then((result: { response: Response; data?: LocationResponse[] }) => {
      if (!result.response.ok || !result.data) return;
      setLocations(result.data);
      if (result.data.length > 0) setLocationId((current) => current || result.data![0].id);
    });
  }, []);

  const load = useCallback(async () => {
    if (!locationId) return;
    setState("loading");
    const result = await (apiClient.GET as any)("/api/waiting-list", { params: { query: { locationId, status: statusFilter } } });
    if (!result.response.ok) {
      setState("error");
      return;
    }
    setEntries((result.data ?? []) as WaitingListEntryResponse[]);
    setState("loaded");
  }, [locationId, statusFilter]);

  useEffect(() => {
    load();
  }, [load]);

  function errorKeyToMessage(errorKey: string): string {
    switch (errorKey) {
      case "errors.waiting_list.not_reorderable_in_current_status":
        return t("notReorderable");
      case "errors.waiting_list.invalid_status_transition":
        return t("invalidStatusTransition");
      default:
        return t("genericError");
    }
  }

  async function saveEntry(values: WaitingListEntryFormValues) {
    if (!locationId) return;
    setSaving(true);
    const body = {
      childFirstName: values.childFirstName.trim(),
      childLastName: values.childLastName.trim(),
      dateOfBirth: values.dateOfBirth,
      contactName: values.contactName.trim(),
      contactEmail: values.contactEmail.trim() || null,
      contactPhone: values.contactPhone.trim() || null,
      locationId,
      requestedStartDate: values.requestedStartDate || null,
      notes: values.notes.trim() || null,
    };
    const result = editing
      ? await (apiClient.PATCH as any)("/api/waiting-list/{id}", { params: { path: { id: editing.id } }, body })
      : await (apiClient.POST as any)("/api/waiting-list", { body });
    setSaving(false);
    if (!result.response.ok) {
      setNotice(t("genericError"));
      return;
    }
    setDialogOpen(false);
    setEditing(null);
    setNotice("");
    await load();
  }

  async function reorder(entry: WaitingListEntryResponse, direction: "up" | "down") {
    const result = await (apiClient.POST as any)("/api/waiting-list/{id}/reorder", {
      params: { path: { id: entry.id } },
      body: { direction },
    });
    if (!result.response.ok) {
      const errorKey = ((result.error ?? {}) as ApiErrorBody).errorKey ?? "";
      setNotice(errorKeyToMessage(errorKey));
      return;
    }
    setEntries((result.data ?? []) as WaitingListEntryResponse[]);
    setNotice("");
  }

  async function transition(entry: WaitingListEntryResponse, status: WaitingListStatus) {
    const result = await (apiClient.POST as any)("/api/waiting-list/{id}/status", {
      params: { path: { id: entry.id } },
      body: { status },
    });
    if (!result.response.ok) {
      const errorKey = ((result.error ?? {}) as ApiErrorBody).errorKey ?? "";
      setNotice(errorKeyToMessage(errorKey));
      return;
    }
    setNotice("");
    await load();
  }

  async function linkExisting(childId: string) {
    if (!linkTarget) return;
    setSaving(true);
    const result = await (apiClient.POST as any)("/api/waiting-list/{id}/link-child", {
      params: { path: { id: linkTarget.id } },
      body: { childId, createNewChild: false },
    });
    setSaving(false);
    if (!result.response.ok) {
      setNotice(t("genericError"));
      return;
    }
    const linkedEntry = linkTarget;
    setLinkTarget(null);
    setNotice("");
    // FR-014: offer the pre-filled contact-creation step for the just-linked child, same as a
    // brand-new child below — a director confirms rather than retypes either way.
    setContactPrefillTarget({ childId, entry: linkedEntry });
    await load();
  }

  async function createNewChild() {
    if (!linkTarget) return;
    setSaving(true);
    const result = await (apiClient.POST as any)("/api/waiting-list/{id}/link-child", {
      params: { path: { id: linkTarget.id } },
      body: { childId: null, createNewChild: true },
    });
    setSaving(false);
    if (!result.response.ok) {
      setNotice(t("genericError"));
      return;
    }
    const createdChildId = (result.data as WaitingListEntryResponse).childId;
    const linkedEntry = linkTarget;
    setLinkTarget(null);
    setNotice("");
    // FR-014: the child-profile creation flow is already pre-filled server-side (feature 012a's
    // CreateChildCommand call, entry name/DOB); this offers the same for contact creation.
    if (createdChildId) setContactPrefillTarget({ childId: createdChildId, entry: linkedEntry });
    await load();
  }

  return (
    <div>
      <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold text-text dark:text-text-dark">{t("title")}</h1>
        <div className="flex flex-wrap items-center gap-3">
          <select
            value={locationId}
            onChange={(e) => setLocationId(e.target.value)}
            aria-label={t("locationLabel")}
            className="h-10 rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
          >
            {locations.map((location) => (
              <option key={location.id} value={location.id}>{location.name}</option>
            ))}
          </select>
          <select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value as StatusFilter)}
            aria-label={t("statusLabel")}
            className="h-10 rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
          >
            <option value="waiting">{t("statusFilter.waiting")}</option>
            <option value="all">{t("statusFilter.all")}</option>
            <option value="offered">{t("statusFilter.offered")}</option>
            <option value="enrolled">{t("statusFilter.enrolled")}</option>
            <option value="withdrawn">{t("statusFilter.withdrawn")}</option>
          </select>
          <Button onClick={() => { setEditing(null); setDialogOpen(true); }}>
            <Plus className="h-4 w-4" strokeWidth={2} />
            {t("add")}
          </Button>
        </div>
      </div>

      {notice && (
        <div className="mb-4 rounded-lg bg-surface-soft p-3 text-sm text-text dark:bg-surface-soft-dark dark:text-text-dark">
          {notice}
        </div>
      )}

      <div className="grid grid-cols-1 gap-6 xl:grid-cols-[1fr_20rem]">
        <div>
          {state === "loading" && <div className="h-64 rounded-lg bg-surface-soft dark:bg-surface-soft-dark" />}
          {state === "error" && <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />}
          {state === "loaded" && entries.length === 0 && <EmptyState icon={ListPlus} message={t("emptyState")} />}
          {state === "loaded" && entries.length > 0 && (
            <WaitingListTable
              entries={entries}
              onEdit={(entry) => { setEditing(entry); setDialogOpen(true); }}
              onReorder={reorder}
              onTransition={transition}
              onLinkChild={setLinkTarget}
              onViewOccupancy={(entry) => setOccupancyDate(entry.requestedStartDate ?? todayDateString())}
            />
          )}
        </div>
        {locationId && <OccupancyPanel locationId={locationId} defaultDate={occupancyDate} />}
      </div>

      <WaitingListEntryDialog
        open={dialogOpen}
        entry={editing}
        onOpenChange={(open) => { setDialogOpen(open); if (!open) setEditing(null); }}
        onSubmit={saveEntry}
        saving={saving}
      />

      <EnrollChildLinkDialog
        open={linkTarget !== null}
        entry={linkTarget}
        onOpenChange={(open) => !open && setLinkTarget(null)}
        onLinkExisting={linkExisting}
        onCreateNew={createNewChild}
        saving={saving}
      />

      {contactPrefillTarget && (
        <LinkContactDialog
          childId={contactPrefillTarget.childId}
          open={contactPrefillTarget !== null}
          onOpenChange={(open) => !open && setContactPrefillTarget(null)}
          onLinked={() => setContactPrefillTarget(null)}
          initialFirstName={splitContactName(contactPrefillTarget.entry.contactName).firstName}
          initialLastName={splitContactName(contactPrefillTarget.entry.contactName).lastName}
          initialPhone={contactPrefillTarget.entry.contactPhone ?? ""}
          initialEmail={contactPrefillTarget.entry.contactEmail ?? ""}
          initialRelationship="Guardian"
        />
      )}
    </div>
  );
}
