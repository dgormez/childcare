"use client";
import { useCallback, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { ArrowLeft, Syringe, HeartPulse, AlertTriangle, Clock, Plus } from "lucide-react";
import { apiClient } from "../../../../lib/apiClient";
import { Button } from "../../../../components/ui/button";
import { Badge } from "../../../../components/ui/badge";
import { EmptyState } from "../../../../components/EmptyState";
import { ErrorState } from "../../../../components/ErrorState";
import { ConfirmDialog } from "../../../../components/ConfirmDialog";
import { Tabs, TabsList, TabsTrigger, TabsContent } from "../../../../components/ui/tabs";
import { ChildProfileTab } from "../../../../components/children/ChildProfileTab";
import { ChildFormDialog, type ChildFormValues } from "../../../../components/children/ChildFormDialog";
import { VaccineRecordForm, type VaccineRecordFormValues } from "../../../../components/health/VaccineRecordForm";
import { HealthRecordForm, type HealthRecordFormValues } from "../../../../components/health/HealthRecordForm";
import { HealthRecordAttachmentControl } from "../../../../components/health/HealthRecordAttachmentControl";
import type { ChildResponse, VaccineRecordResponse, HealthRecordResponse } from "../../../../lib/types";

type LoadState = "loading" | "loaded" | "error";

/**
 * Child-detail screen — "Profiel" (006a) and "Gezondheid" (013c) tabs on one screen, the first
 * tabbed structure here; 013c's own shipped-notes flagged this screen as the one future
 * per-child-tab work should extend, rather than replace.
 */
export default function ChildDetailPage() {
  const tc = useTranslations("children");
  const t = useTranslations("children.health");
  const router = useRouter();
  const params = useParams<{ id: string }>();

  const [child, setChild] = useState<ChildResponse | null>(null);
  const [vaccines, setVaccines] = useState<VaccineRecordResponse[]>([]);
  const [healthRecords, setHealthRecords] = useState<HealthRecordResponse[]>([]);
  const [state, setState] = useState<LoadState>("loading");

  const [editDialogOpen, setEditDialogOpen] = useState(false);
  const [editSaving, setEditSaving] = useState(false);
  const [editSaveError, setEditSaveError] = useState<string | null>(null);

  const [vaccineFormOpen, setVaccineFormOpen] = useState(false);
  const [editingVaccine, setEditingVaccine] = useState<VaccineRecordResponse | null>(null);
  const [vaccineSaving, setVaccineSaving] = useState(false);
  const [vaccineSaveError, setVaccineSaveError] = useState<string | null>(null);
  const [vaccineDeleteTarget, setVaccineDeleteTarget] = useState<VaccineRecordResponse | null>(null);
  const [vaccineDeleting, setVaccineDeleting] = useState(false);
  const [vaccineDeleteError, setVaccineDeleteError] = useState<string | null>(null);

  const [healthFormOpen, setHealthFormOpen] = useState(false);
  const [editingHealthRecord, setEditingHealthRecord] = useState<HealthRecordResponse | null>(null);
  const [healthSaving, setHealthSaving] = useState(false);
  const [healthSaveError, setHealthSaveError] = useState<string | null>(null);
  const [healthDeleteTarget, setHealthDeleteTarget] = useState<HealthRecordResponse | null>(null);
  const [healthDeleting, setHealthDeleting] = useState(false);
  const [healthDeleteError, setHealthDeleteError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setState("loading");
    const [childResult, vaccinesResult, healthRecordsResult] = await Promise.all([
      apiClient.GET("/api/children/{id}", { params: { path: { id: params.id } } }),
      apiClient.GET("/api/children/{childId}/vaccine-records", { params: { path: { childId: params.id } } }),
      apiClient.GET("/api/children/{childId}/health-records", { params: { path: { childId: params.id } } }),
    ]);
    if (!childResult.response.ok || !vaccinesResult.response.ok || !healthRecordsResult.response.ok) {
      setState("error");
      return;
    }
    setChild(childResult.data as unknown as ChildResponse);
    setVaccines(vaccinesResult.data as unknown as VaccineRecordResponse[]);
    setHealthRecords(healthRecordsResult.data as unknown as HealthRecordResponse[]);
    setState("loaded");
  }, [params.id]);

  useEffect(() => {
    load();
  }, [load]);

  async function submitChildEdit(values: ChildFormValues) {
    setEditSaving(true);
    setEditSaveError(null);
    const result = await apiClient.PUT("/api/children/{id}", {
      params: { path: { id: params.id } },
      body: values,
    });
    setEditSaving(false);
    if (!result.response.ok) {
      setEditSaveError(tc("form.saveError"));
      return;
    }
    setEditDialogOpen(false);
    await load();
  }

  async function uploadChildPhoto(file: File): Promise<boolean> {
    const urlResult = await apiClient.POST("/api/children/{id}/photo/upload-url", {
      params: { path: { id: params.id } },
    });
    if (!urlResult.response.ok) return false;

    const { uploadUrl } = urlResult.data as unknown as { uploadUrl: string };
    const putResult = await fetch(uploadUrl, { method: "PUT", body: file, headers: { "Content-Type": file.type } });
    if (!putResult.ok) return false;

    await load();
    return true;
  }

  async function submitVaccineRecord(values: VaccineRecordFormValues) {
    setVaccineSaving(true);
    setVaccineSaveError(null);
    const body = {
      vaccineName: values.vaccineName,
      doseNumber: values.doseNumber,
      administeredOn: values.administeredOn,
      nextDueDate: values.nextDueDate,
      administeredBy: values.administeredBy,
      notes: values.notes,
    };
    const result = editingVaccine
      ? await apiClient.PUT("/api/children/{childId}/vaccine-records/{id}", {
          params: { path: { childId: params.id, id: editingVaccine.id } },
          body,
        })
      : await apiClient.POST("/api/children/{childId}/vaccine-records", {
          params: { path: { childId: params.id } },
          body,
        });
    setVaccineSaving(false);
    if (!result.response.ok) {
      setVaccineSaveError(t("vaccines.saveError"));
      return;
    }
    setVaccineFormOpen(false);
    setEditingVaccine(null);
    await load();
  }

  async function confirmDeleteVaccine() {
    if (!vaccineDeleteTarget) return;
    setVaccineDeleting(true);
    setVaccineDeleteError(null);
    const result = await apiClient.DELETE("/api/children/{childId}/vaccine-records/{id}", {
      params: { path: { childId: params.id, id: vaccineDeleteTarget.id } },
    });
    setVaccineDeleting(false);
    if (!result.response.ok) {
      setVaccineDeleteError(t("vaccines.deleteError"));
      return;
    }
    setVaccineDeleteTarget(null);
    await load();
  }

  async function submitHealthRecord(values: HealthRecordFormValues) {
    setHealthSaving(true);
    setHealthSaveError(null);
    const body = {
      recordType: values.recordType,
      title: values.title,
      description: values.description,
      validFrom: values.validFrom,
      validUntil: values.validUntil,
    };
    const result = editingHealthRecord
      ? await apiClient.PUT("/api/children/{childId}/health-records/{id}", {
          params: { path: { childId: params.id, id: editingHealthRecord.id } },
          body,
        })
      : await apiClient.POST("/api/children/{childId}/health-records", {
          params: { path: { childId: params.id } },
          body,
        });
    setHealthSaving(false);
    if (!result.response.ok) {
      setHealthSaveError(t("records.saveError"));
      return;
    }
    setHealthFormOpen(false);
    setEditingHealthRecord(null);
    await load();
  }

  async function confirmDeleteHealthRecord() {
    if (!healthDeleteTarget) return;
    setHealthDeleting(true);
    setHealthDeleteError(null);
    const result = await apiClient.DELETE("/api/children/{childId}/health-records/{id}", {
      params: { path: { childId: params.id, id: healthDeleteTarget.id } },
    });
    setHealthDeleting(false);
    if (!result.response.ok) {
      setHealthDeleteError(t("records.deleteError"));
      return;
    }
    setHealthDeleteTarget(null);
    await load();
  }

  async function uploadHealthRecordAttachment(record: HealthRecordResponse, file: File): Promise<boolean> {
    const urlResult = await apiClient.POST("/api/children/{childId}/health-records/{id}/attachment-upload-url", {
      params: { path: { childId: params.id, id: record.id } },
      body: { contentType: file.type },
    });
    if (!urlResult.response.ok) return false;

    const { uploadUrl } = urlResult.data as unknown as { uploadUrl: string };
    const putResult = await fetch(uploadUrl, { method: "PUT", body: file, headers: { "Content-Type": file.type } });
    if (!putResult.ok) return false;

    await load();
    return true;
  }

  if (state === "loading") return <div className="h-64 animate-pulse rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />;
  if (state === "error" || !child) return <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />;

  const todayIso = new Date().toISOString().slice(0, 10);

  return (
    <div>
      <button
        onClick={() => router.push("/children")}
        className="mb-4 flex items-center gap-2 text-sm text-text-soft hover:text-text dark:text-text-soft-dark dark:hover:text-text-dark"
      >
        <ArrowLeft className="h-4 w-4" strokeWidth={2} />
        {t("backToList")}
      </button>

      <h1 className="mb-6 text-2xl font-semibold text-text dark:text-text-dark">{child.firstName} {child.lastName}</h1>

      <Tabs defaultValue="profile">
        <TabsList>
          <TabsTrigger value="profile">{tc("tabProfile")}</TabsTrigger>
          <TabsTrigger value="health">{tc("tabHealth")}</TabsTrigger>
        </TabsList>

        <TabsContent value="profile">
          <ChildProfileTab
            child={child}
            onEdit={() => { setEditSaveError(null); setEditDialogOpen(true); }}
            onPhotoUpload={uploadChildPhoto}
          />
        </TabsContent>

        <TabsContent value="health">
      <h2 className="mb-4 text-lg font-semibold text-text dark:text-text-dark">{t("title")}</h2>

      <div className="mb-8">
        <div className="mb-3 flex items-center justify-between">
          <h3 className="text-sm font-semibold text-text dark:text-text-dark">{t("vaccines.title")}</h3>
          <Button
            size="sm"
            className="inline-flex items-center gap-1"
            onClick={() => {
              setEditingVaccine(null);
              setVaccineSaveError(null);
              setVaccineFormOpen(true);
            }}
          >
            <Plus className="h-4 w-4" strokeWidth={2} />
            {t("vaccines.addButton")}
          </Button>
        </div>

        {vaccines.length === 0 ? (
          <EmptyState icon={Syringe} message={t("vaccines.emptyState")} />
        ) : (
          <table className="w-full text-left text-sm">
            <thead>
              <tr className="border-b border-border text-text-soft dark:border-border-dark dark:text-text-soft-dark">
                <th className="py-2 pr-4 font-medium">{t("vaccines.columnVaccine")}</th>
                <th className="py-2 pr-4 font-medium">{t("vaccines.columnAdministeredOn")}</th>
                <th className="py-2 pr-4 font-medium">{t("vaccines.columnNextDueDate")}</th>
                <th className="py-2 pr-4 font-medium" />
              </tr>
            </thead>
            <tbody>
              {vaccines.map((v) => {
                const isOverdue = !!v.nextDueDate && v.nextDueDate < todayIso;
                const isDueSoon = !!v.nextDueDate && !isOverdue &&
                  v.nextDueDate <= new Date(Date.now() + 30 * 86400000).toISOString().slice(0, 10);
                return (
                  <tr key={v.id} className="h-10 border-b border-border last:border-0 dark:border-border-dark">
                    <td className="py-2 pr-4 text-text dark:text-text-dark">
                      {v.vaccineName}{v.doseNumber ? ` (${t("vaccines.dose", { n: v.doseNumber })})` : ""}
                    </td>
                    <td className="py-2 pr-4 tabular-nums text-text-soft dark:text-text-soft-dark">{v.administeredOn}</td>
                    <td className="py-2 pr-4">
                      <span className="tabular-nums text-text-soft dark:text-text-soft-dark">{v.nextDueDate ?? "—"}</span>
                      {isOverdue && (
                        <Badge variant="danger" className="ml-2 inline-flex items-center gap-1">
                          <AlertTriangle className="h-3 w-3" strokeWidth={2} />
                          {t("vaccines.overdue")}
                        </Badge>
                      )}
                      {isDueSoon && (
                        <Badge variant="warning" className="ml-2 inline-flex items-center gap-1">
                          <Clock className="h-3 w-3" strokeWidth={2} />
                          {t("vaccines.dueSoon")}
                        </Badge>
                      )}
                    </td>
                    <td className="py-2 pr-4 text-right">
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => {
                          setEditingVaccine(v);
                          setVaccineSaveError(null);
                          setVaccineFormOpen(true);
                        }}
                      >
                        {t("vaccines.edit")}
                      </Button>
                      <Button
                        variant="destructive"
                        size="sm"
                        onClick={() => {
                          setVaccineDeleteError(null);
                          setVaccineDeleteTarget(v);
                        }}
                      >
                        {t("vaccines.delete")}
                      </Button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
      </div>

      <div className="mb-8">
        <div className="mb-3 flex items-center justify-between">
          <h3 className="text-sm font-semibold text-text dark:text-text-dark">{t("records.title")}</h3>
          <Button
            size="sm"
            className="inline-flex items-center gap-1"
            onClick={() => {
              setEditingHealthRecord(null);
              setHealthSaveError(null);
              setHealthFormOpen(true);
            }}
          >
            <Plus className="h-4 w-4" strokeWidth={2} />
            {t("records.addButton")}
          </Button>
        </div>

        {healthRecords.length === 0 ? (
          <EmptyState icon={HeartPulse} message={t("records.emptyState")} />
        ) : (
          <table className="w-full text-left text-sm">
            <thead>
              <tr className="border-b border-border text-text-soft dark:border-border-dark dark:text-text-soft-dark">
                <th className="py-2 pr-4 font-medium">{t("records.columnTitle")}</th>
                <th className="py-2 pr-4 font-medium">{t("records.columnType")}</th>
                <th className="py-2 pr-4 font-medium">{t("records.columnAttachment")}</th>
                <th className="py-2 pr-4 font-medium" />
              </tr>
            </thead>
            <tbody>
              {healthRecords.map((r) => (
                <tr key={r.id} className="h-10 border-b border-border last:border-0 dark:border-border-dark">
                  <td className="py-2 pr-4 text-text dark:text-text-dark">
                    {r.title}
                    {r.isExpired && (
                      <Badge variant="neutral" className="ml-2">{t("records.expired")}</Badge>
                    )}
                  </td>
                  <td className="py-2 pr-4 text-text-soft dark:text-text-soft-dark">{t(`records.form.recordType.${r.recordType}`)}</td>
                  <td className="py-2 pr-4">
                    <HealthRecordAttachmentControl
                      attachmentDownloadUrl={r.attachmentDownloadUrl}
                      onUpload={(file) => uploadHealthRecordAttachment(r, file)}
                    />
                  </td>
                  <td className="py-2 pr-4 text-right">
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => {
                        setEditingHealthRecord(r);
                        setHealthSaveError(null);
                        setHealthFormOpen(true);
                      }}
                    >
                      {t("records.edit")}
                    </Button>
                    <Button
                      variant="destructive"
                      size="sm"
                      onClick={() => {
                        setHealthDeleteError(null);
                        setHealthDeleteTarget(r);
                      }}
                    >
                      {t("records.delete")}
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
        </TabsContent>
      </Tabs>

      <ChildFormDialog
        open={editDialogOpen}
        mode="edit"
        child={child}
        onOpenChange={setEditDialogOpen}
        onSubmit={submitChildEdit}
        saving={editSaving}
        error={editSaveError}
      />

      <VaccineRecordForm
        open={vaccineFormOpen}
        record={editingVaccine}
        onOpenChange={setVaccineFormOpen}
        onSubmit={submitVaccineRecord}
        saving={vaccineSaving}
        error={vaccineSaveError}
      />

      <ConfirmDialog
        open={!!vaccineDeleteTarget}
        onOpenChange={(open) => !open && setVaccineDeleteTarget(null)}
        title={t("vaccines.deleteConfirmTitle")}
        description={vaccineDeleteError ?? t("vaccines.deleteConfirmDescription")}
        confirmLabel={t("vaccines.delete")}
        cancelLabel={t("vaccines.cancel")}
        onConfirm={confirmDeleteVaccine}
        confirmDestructive
        confirming={vaccineDeleting}
      />

      <HealthRecordForm
        open={healthFormOpen}
        record={editingHealthRecord}
        onOpenChange={setHealthFormOpen}
        onSubmit={submitHealthRecord}
        saving={healthSaving}
        error={healthSaveError}
      />

      <ConfirmDialog
        open={!!healthDeleteTarget}
        onOpenChange={(open) => !open && setHealthDeleteTarget(null)}
        title={t("records.deleteConfirmTitle")}
        description={healthDeleteError ?? t("records.deleteConfirmDescription")}
        confirmLabel={t("records.delete")}
        cancelLabel={t("records.cancel")}
        onConfirm={confirmDeleteHealthRecord}
        confirmDestructive
        confirming={healthDeleting}
      />
    </div>
  );
}
