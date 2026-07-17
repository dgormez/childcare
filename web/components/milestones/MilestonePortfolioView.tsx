"use client";
import { useEffect, useState } from "react";
import { useTranslations, useLocale } from "next-intl";
import { CheckCircle2, Sparkles, Circle, Download, Sprout } from "lucide-react";
import { apiClient, getAccessToken } from "../../lib/apiClient";
import { Button } from "../ui/button";
import { EmptyState } from "../EmptyState";
import { ErrorState } from "../ErrorState";
import type { DevelopmentalDomainResponse, DevelopmentalMilestoneResponse, MilestoneObservationStatus } from "../../lib/types";

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "";

interface MilestonePortfolioViewProps {
  childId: string;
}

type LoadState = "loading" | "loaded" | "error";

function localizedName(entry: { nameNl: string; nameFr: string; nameEn: string }, locale: string): string {
  if (locale === "fr") return entry.nameFr;
  if (locale === "en") return entry.nameEn;
  return entry.nameNl;
}

function localizedDescription(entry: { descriptionNl: string; descriptionFr: string; descriptionEn: string }, locale: string): string {
  if (locale === "fr") return entry.descriptionFr;
  if (locale === "en") return entry.descriptionEn;
  return entry.descriptionNl;
}

// Never color alone (design-system.md's Status Indicators section) — a distinct icon per status.
function StatusIcon({ status }: { status: MilestoneObservationStatus | null }) {
  if (status === "achieved") return <CheckCircle2 className="h-4 w-4 text-success dark:text-success-dark" strokeWidth={2} />;
  if (status === "emerging") return <Sparkles className="h-4 w-4 text-text-soft dark:text-text-soft-dark" strokeWidth={2} />;
  return <Circle className="h-4 w-4 text-text-soft dark:text-text-soft-dark" strokeWidth={2} />;
}

function MilestoneRow({ milestone, locale, t }: { milestone: DevelopmentalMilestoneResponse; locale: string; t: (key: string) => string }) {
  return (
    <div
      className={`flex items-center justify-between gap-4 border-b border-border py-2 last:border-0 dark:border-border-dark ${
        milestone.isCurrentFocus ? "bg-primary-soft dark:bg-primary-soft-dark" : ""
      }`}
    >
      <div className="min-w-0 flex-1">
        <p className="text-sm text-text dark:text-text-dark">{localizedDescription(milestone, locale)}</p>
        <p className="text-xs text-text-soft dark:text-text-soft-dark">
          {milestone.ageFromMonths}–{milestone.ageToMonths} {t("ageMonths")}
          {milestone.isCurrentFocus && ` · ${t("currentFocus")}`}
        </p>
      </div>
      <div className="flex items-center gap-1">
        <StatusIcon status={milestone.currentStatus} />
        <span className="text-xs text-text-soft dark:text-text-soft-dark">
          {milestone.currentStatus ? t(`status.${milestone.currentStatus}`) : t("noObservations")}
        </span>
      </div>
    </div>
  );
}

/** Self-contained "Milestones" tab section on the child-detail screen (feature 016, US2/US4) —
 * fetches the full, history-included portfolio (StaffOrDirector-equivalent DeviceOrStaffOrDirector
 * policy) and groups by domain, distinguishing the age-appropriate band. */
export function MilestonePortfolioView({ childId }: MilestonePortfolioViewProps) {
  const t = useTranslations("children.milestones");
  const locale = useLocale();
  const [domains, setDomains] = useState<DevelopmentalDomainResponse[]>([]);
  const [state, setState] = useState<LoadState>("loading");
  const [downloading, setDownloading] = useState(false);
  const [notice, setNotice] = useState("");

  const load = async () => {
    setState("loading");
    const result = await apiClient.GET("/api/children/{childId}/milestone-portfolio", { params: { path: { childId } } });
    if (!result.response.ok) {
      setState("error");
      return;
    }
    const data = result.data as unknown as { domains: DevelopmentalDomainResponse[] };
    setDomains(data.domains);
    setState("loaded");
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [childId]);

  async function downloadPdf() {
    setDownloading(true);
    setNotice("");
    try {
      const response = await fetch(`${API_BASE}/api/children/${childId}/milestone-portfolio/pdf`, {
        headers: { Authorization: `Bearer ${getAccessToken()}` },
      });
      if (!response.ok) throw new Error("download failed");
      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `milestone-portfolio-${childId}.pdf`;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch {
      setNotice(t("downloadPdfError"));
    } finally {
      setDownloading(false);
    }
  }

  if (state === "loading") return <div className="h-32 animate-pulse rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />;
  if (state === "error") return <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />;

  const hasAnyMilestones = domains.some((d) => d.milestones.length > 0);

  return (
    <div>
      <div className="mb-4 flex items-center justify-between">
        <h2 className="text-lg font-semibold text-text dark:text-text-dark">{t("title")}</h2>
        <Button size="sm" variant="secondary" onClick={downloadPdf} disabled={downloading} className="inline-flex items-center gap-1">
          <Download className="h-4 w-4" strokeWidth={2} />
          {t("downloadPdf")}
        </Button>
      </div>

      {notice && <p className="mb-3 text-sm text-danger dark:text-danger-dark">{notice}</p>}

      {!hasAnyMilestones ? (
        <EmptyState icon={Sprout} message={t("emptyState")} />
      ) : (
        <div className="space-y-6">
          {domains.map((domain) => (
            <div key={domain.id}>
              <h3 className="mb-2 text-sm font-semibold text-text dark:text-text-dark">{localizedName(domain, locale)}</h3>
              {domain.milestones.map((milestone) => (
                <MilestoneRow key={milestone.id} milestone={milestone} locale={locale} t={t} />
              ))}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
