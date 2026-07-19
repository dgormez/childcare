"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useTranslations } from "next-intl";
import { ChevronLeft, ChevronRight, Users, Tablet, MapPin, FileText, Baby, LogOut, CalendarClock, CalendarX, CalendarDays, CalendarRange, ListPlus, MessageSquare, Megaphone, Mail, Sparkles, Inbox, ShieldAlert, LayoutDashboard, UtensilsCrossed, Syringe, Receipt, FileCheck2, Settings } from "lucide-react";
import { cn } from "../lib/cn";
import { apiClient } from "../lib/apiClient";
import type { Session } from "../lib/auth";
import type { MessageThreadSummaryResponse } from "../lib/types";

const REAL_NAV = [
  { href: "/dashboard", labelKey: "dashboard", icon: LayoutDashboard },
  { href: "/staff", labelKey: "staff", icon: Users },
  { href: "/children", labelKey: "children", icon: Baby },
  { href: "/devices", labelKey: "devices", icon: Tablet },
  { href: "/locations", labelKey: "locations", icon: MapPin },
  { href: "/attendance", labelKey: "attendance", icon: CalendarClock },
  { href: "/meal-list", labelKey: "mealList", icon: UtensilsCrossed },
  { href: "/menu", labelKey: "menu", icon: CalendarRange },
  { href: "/groups", labelKey: "groups", icon: Sparkles },
  { href: "/closures", labelKey: "closures", icon: CalendarX },
  { href: "/scheduling", labelKey: "scheduling", icon: CalendarDays },
  { href: "/waiting-list", labelKey: "waitingList", icon: ListPlus },
  { href: "/requests", labelKey: "dayReservations", icon: Inbox },
  { href: "/messages", labelKey: "messages", icon: MessageSquare },
  { href: "/announcements", labelKey: "announcements", icon: Megaphone },
  { href: "/communications", labelKey: "communications", icon: Mail },
  { href: "/incidents", labelKey: "incidents", icon: ShieldAlert },
  { href: "/invoices", labelKey: "invoices", icon: Receipt },
  { href: "/fiscal-attestations", labelKey: "fiscalAttestations", icon: FileCheck2 },
  { href: "/settings", labelKey: "organisationSettings", icon: Settings },
] as const;

// FR-006: inert placeholders for sections later features will build — never real links, so a
// director can't navigate to a half-built screen from the sidebar itself (direct URL entry is
// still handled by each route's own not-yet-available page, per spec Edge Cases).
// "locations" moved to REAL_NAV — feature 013f replaced its NotYetAvailable placeholder.
// "children" moved to REAL_NAV — feature 013c replaced its NotYetAvailable placeholder.
const PLACEHOLDER_NAV = [
  { labelKey: "contracts", icon: FileText },
] as const;

// Feature 013h (FR-003) — the only cross-tenant capability in the sidebar, so it's rendered as
// its own bordered section below the tenant-scoped nav rather than folded into REAL_NAV,
// visually distinguishing "this tenant" from "the whole platform." Gated purely on
// session.user.isPlatformAdmin, resolved server-side (AuthenticatedUser.IsPlatformAdmin) since
// this app never decodes the JWT client-side.
const PLATFORM_ADMIN_NAV = { href: "/platform-admin/vaccine-types", labelKey: "vaccineTypes", icon: Syringe } as const;

interface SidebarProps {
  // FR-005b: the caller (AppLayout) never renders Sidebar until organisationName/user.name are
  // both resolved — it shows its own full-page loading state first, so there is never a state
  // where Sidebar itself would need to render a partial/blank name.
  session: Session;
  onLogout: () => void;
}

export function Sidebar({ session, onLogout }: SidebarProps) {
  const t = useTranslations("sidebar");
  const pathname = usePathname();
  const [collapsed, setCollapsed] = useState(false);
  const [unreadThreadCount, setUnreadThreadCount] = useState(0);

  // FR-013: director/staff awareness of new parent messages without manual polling — a single
  // fetch on mount is enough to satisfy "without manual polling" (the requirement is that no
  // separate poll *endpoint* is needed, not that the client polls continuously); the /messages
  // list itself already refreshes this per-thread when visited.
  useEffect(() => {
    (apiClient.GET as any)("/api/message-threads").then((result: { response: Response; data?: MessageThreadSummaryResponse[] }) => {
      if (!result.response.ok || !result.data) return;
      setUnreadThreadCount(result.data.filter((thread) => thread.hasUnread).length);
    });
  }, []);

  return (
    <aside
      className={cn(
        "flex h-full flex-col border-r border-border bg-surface py-6 transition-[width] duration-200 dark:border-border-dark dark:bg-surface-dark",
        collapsed ? "w-16 px-2" : "w-60 px-3",
      )}
    >
      <div className="mb-6 px-2">
        <p className="truncate text-sm font-semibold text-text dark:text-text-dark">
          {collapsed ? session.organisationName.slice(0, 1) : session.organisationName}
        </p>
        {!collapsed && (
          <p className="truncate text-xs text-text-soft dark:text-text-soft-dark">{session.user.name}</p>
        )}
      </div>

      <nav className="flex-1 space-y-1">
        {REAL_NAV.map(({ href, labelKey, icon: Icon }) => {
          const active = pathname.startsWith(href);
          return (
            <Link
              key={href}
              href={href}
              className={cn(
                "flex items-center gap-3 rounded-lg px-2 py-2 text-sm font-medium transition",
                active
                  ? "bg-primary-soft text-primary-hover dark:bg-primary-soft-dark dark:text-primary-hover-dark"
                  : "text-text-soft hover:bg-surface-soft dark:text-text-soft-dark dark:hover:bg-surface-soft-dark",
              )}
            >
              <Icon className="h-5 w-5 shrink-0" strokeWidth={2} />
              {!collapsed && (
                <span className="flex flex-1 items-center justify-between truncate">
                  {t(labelKey)}
                  {href === "/messages" && unreadThreadCount > 0 && (
                    <span className="ml-2 rounded-full bg-primary px-2 py-1 text-xs font-medium text-white">
                      {unreadThreadCount}
                    </span>
                  )}
                </span>
              )}
            </Link>
          );
        })}

        {PLACEHOLDER_NAV.map(({ labelKey, icon: Icon }) => (
          <div
            key={labelKey}
            className="flex cursor-not-allowed items-center gap-3 rounded-lg px-2 py-2 text-sm font-medium text-text-soft opacity-50 dark:text-text-soft-dark"
            aria-disabled="true"
          >
            <Icon className="h-5 w-5 shrink-0" strokeWidth={2} />
            {!collapsed && (
              <span className="flex flex-1 items-center justify-between truncate">
                {t(labelKey)}
                <span className="ml-2 rounded-full bg-surface-soft px-2 py-1 text-xs font-medium dark:bg-surface-soft-dark">
                  {t("comingSoon")}
                </span>
              </span>
            )}
          </div>
        ))}

        {session.user.isPlatformAdmin && (
          <div className="mt-3 space-y-1 border-t border-border pt-3 dark:border-border-dark">
            {(() => {
              const { href, labelKey, icon: Icon } = PLATFORM_ADMIN_NAV;
              const active = pathname.startsWith(href);
              return (
                <Link
                  href={href}
                  className={cn(
                    "flex items-center gap-3 rounded-lg px-2 py-2 text-sm font-medium transition",
                    active
                      ? "bg-primary-soft text-primary-hover dark:bg-primary-soft-dark dark:text-primary-hover-dark"
                      : "text-text-soft hover:bg-surface-soft dark:text-text-soft-dark dark:hover:bg-surface-soft-dark",
                  )}
                >
                  <Icon className="h-5 w-5 shrink-0" strokeWidth={2} />
                  {!collapsed && <span className="truncate">{t(labelKey)}</span>}
                </Link>
              );
            })()}
          </div>
        )}
      </nav>

      <div className="space-y-1 border-t border-border pt-3 dark:border-border-dark">
        <button
          onClick={onLogout}
          className="flex w-full items-center gap-3 rounded-lg px-2 py-2 text-sm font-medium text-text-soft transition hover:bg-surface-soft dark:text-text-soft-dark dark:hover:bg-surface-soft-dark"
        >
          <LogOut className="h-5 w-5 shrink-0" strokeWidth={2} />
          {!collapsed && <span>{t("signOut")}</span>}
        </button>
        <button
          onClick={() => setCollapsed((c) => !c)}
          className="flex w-full items-center gap-3 rounded-lg px-2 py-2 text-sm font-medium text-text-soft transition hover:bg-surface-soft dark:text-text-soft-dark dark:hover:bg-surface-soft-dark"
          aria-label={collapsed ? t("expand") : t("collapse")}
        >
          {collapsed ? <ChevronRight className="h-5 w-5 shrink-0" strokeWidth={2} /> : <ChevronLeft className="h-5 w-5 shrink-0" strokeWidth={2} />}
          {!collapsed && <span>{t("collapse")}</span>}
        </button>
      </div>
    </aside>
  );
}
