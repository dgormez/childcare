"use client";
import { AlertTriangle, CalendarDays, GraduationCap } from "lucide-react";
import { useTranslations } from "next-intl";
import { Badge } from "./ui/badge";
import { Button } from "./ui/button";
import type { ClosureDayResponse, ClosureType } from "../lib/types";

interface ClosureListProps {
  closures: ClosureDayResponse[];
  onEdit: (closure: ClosureDayResponse) => void;
  onPublish: (closure: ClosureDayResponse) => void;
  onCancel: (closure: ClosureDayResponse) => void;
}

function iconFor(type: ClosureType) {
  if (type === "training") return GraduationCap;
  if (type === "extraordinary") return AlertTriangle;
  return CalendarDays;
}

export function ClosureList({ closures, onEdit, onPublish, onCancel }: ClosureListProps) {
  const t = useTranslations("closures");

  return (
    <div className="divide-y divide-border dark:divide-border-dark">
      {closures.map((closure) => {
        const Icon = iconFor(closure.closureType);
        return (
          <div key={closure.id} className="grid min-h-10 grid-cols-[1fr_auto] items-center gap-4 py-2">
            <div className="min-w-0">
              <div className="flex items-center gap-2">
                <Icon className="h-4 w-4 text-text-soft dark:text-text-soft-dark" strokeWidth={2} />
                <p className="truncate text-sm font-medium text-text dark:text-text-dark">{closure.label}</p>
                <Badge variant={closure.status === "published" ? "success" : closure.status === "cancelled" ? "danger" : "neutral"}>
                  {t(`status.${closure.status}`)}
                </Badge>
              </div>
              <p className="mt-1 text-xs text-text-soft dark:text-text-soft-dark">
                {closure.date} · {t(`type.${closure.closureType}`)}
                {closure.deliverySummary.failed > 0 ? ` · ${t("deliveryFailed", { count: closure.deliverySummary.failed })}` : ""}
              </p>
            </div>
            <div className="flex items-center gap-2">
              {closure.status === "draft" && (
                <>
                  <Button variant="ghost" size="sm" onClick={() => onEdit(closure)}>{t("edit")}</Button>
                  <Button size="sm" onClick={() => onPublish(closure)}>{t("publish")}</Button>
                </>
              )}
              {closure.status !== "cancelled" && (
                <Button variant="destructive" size="sm" onClick={() => onCancel(closure)}>
                  {closure.status === "draft" ? t("remove") : t("cancelClosure")}
                </Button>
              )}
            </div>
          </div>
        );
      })}
    </div>
  );
}
