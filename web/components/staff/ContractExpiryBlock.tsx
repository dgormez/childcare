"use client";
import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { Users, AlertTriangle, Clock } from "lucide-react";
import { apiClient } from "../../lib/apiClient";
import { Badge } from "../ui/badge";
import { EmptyState } from "../EmptyState";
import { ErrorState } from "../ErrorState";
import type { ContractExpiringResponse } from "../../lib/types";

type LoadState = "loading" | "loaded" | "error";

/**
 * Director dashboard "Personeel — verlopende contracten" block (spec.md FR-014/FR-015, User
 * Story 3) — structurally mirrors DueSoonBlock.tsx (feature 013c, research.md R8), the
 * established pattern for this exact shape of alert.
 */
export function ContractExpiryBlock() {
  const t = useTranslations("dashboard.contractsExpiring");
  const router = useRouter();
  const [items, setItems] = useState<ContractExpiringResponse[]>([]);
  const [state, setState] = useState<LoadState>("loading");

  const load = useCallback(async () => {
    setState("loading");
    const result = await apiClient.GET("/api/staff/contracts-expiring");
    if (!result.response.ok) {
      setState("error");
      return;
    }
    setItems(result.data as unknown as ContractExpiringResponse[]);
    setState("loaded");
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <section>
      <h2 className="mb-3 text-sm font-semibold text-text dark:text-text-dark">{t("title")}</h2>

      {state === "loading" && <div className="h-32 animate-pulse rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />}
      {state === "error" && <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />}
      {state === "loaded" && items.length === 0 && <EmptyState icon={Users} message={t("emptyState")} />}
      {state === "loaded" && items.length > 0 && (
        <ul className="divide-y divide-border rounded-xl border border-border dark:divide-border-dark dark:border-border-dark">
          {items.map((item) => (
            <li
              key={item.staffProfileId}
              onClick={() => router.push(`/staff/${item.staffProfileId}`)}
              className="flex h-10 cursor-pointer items-center justify-between px-4 hover:bg-surface-soft dark:hover:bg-surface-soft-dark"
            >
              <span className="text-sm text-text dark:text-text-dark">{item.staffName}</span>
              <div className="flex items-center gap-2">
                <span className="tabular-nums text-sm text-text-soft dark:text-text-soft-dark">{item.validUntil}</span>
                {item.isExpired ? (
                  <Badge variant="danger" className="inline-flex items-center gap-1">
                    <AlertTriangle className="h-3 w-3" strokeWidth={2} />
                    {t("expired")}
                  </Badge>
                ) : (
                  <Badge variant="warning" className="inline-flex items-center gap-1">
                    <Clock className="h-3 w-3" strokeWidth={2} />
                    {t("dueSoon")}
                  </Badge>
                )}
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
