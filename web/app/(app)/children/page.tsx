"use client";
import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { Users } from "lucide-react";
import { apiClient } from "../../../lib/apiClient";
import { EmptyState } from "../../../components/EmptyState";
import { ErrorState } from "../../../components/ErrorState";
import type { ChildResponse } from "../../../lib/types";

type LoadState = "loading" | "loaded" | "error";

/**
 * Minimal children list — this feature (013c) is the first to need a real /children screen
 * (007a/013b both deferred it, see BACKLOG.md's shipped-notes) but only needs enough to reach a
 * child's Gezondheid tab, not a full child-file. A future feature building the full child
 * profile screen should replace this list's row action, not this list itself.
 */
export default function ChildrenPage() {
  const t = useTranslations("children");
  const router = useRouter();
  const [children, setChildren] = useState<ChildResponse[]>([]);
  const [state, setState] = useState<LoadState>("loading");

  const load = useCallback(async () => {
    setState("loading");
    const result = await apiClient.GET("/api/children");
    if (!result.response.ok) {
      setState("error");
      return;
    }
    setChildren(result.data as unknown as ChildResponse[]);
    setState("loaded");
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <div>
      <h1 className="mb-6 text-2xl font-semibold text-text dark:text-text-dark">{t("title")}</h1>

      {state === "loading" && <div className="h-64 animate-pulse rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />}
      {state === "error" && <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />}
      {state === "loaded" && children.length === 0 && <EmptyState icon={Users} message={t("emptyState")} />}
      {state === "loaded" && children.length > 0 && (
        <table className="w-full text-left text-sm">
          <thead>
            <tr className="border-b border-border text-text-soft dark:border-border-dark dark:text-text-soft-dark">
              <th className="py-2 pr-4 font-medium">{t("columnName")}</th>
              <th className="py-2 pr-4 font-medium">{t("columnDateOfBirth")}</th>
              <th className="py-2 pr-4 font-medium">{t("columnStatus")}</th>
            </tr>
          </thead>
          <tbody>
            {children.map((child) => (
              <tr
                key={child.id}
                onClick={() => router.push(`/children/${child.id}`)}
                className="h-10 cursor-pointer border-b border-border last:border-0 hover:bg-surface-soft dark:border-border-dark dark:hover:bg-surface-soft-dark"
              >
                <td className="py-2 pr-4 text-text dark:text-text-dark">{child.firstName} {child.lastName}</td>
                <td className="py-2 pr-4 tabular-nums text-text-soft dark:text-text-soft-dark">{child.dateOfBirth}</td>
                <td className="py-2 pr-4 text-text-soft dark:text-text-soft-dark">
                  {child.deactivatedAt ? t("statusDeactivated") : t("statusActive")}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
