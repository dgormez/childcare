"use client";
import { useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useTranslations } from "next-intl";
import { ChevronLeft, ChevronRight, Users, Tablet, MapPin, FileText, Baby, LogOut, CalendarClock } from "lucide-react";
import { cn } from "../lib/cn";
import type { Session } from "../lib/auth";

const REAL_NAV = [
  { href: "/staff", labelKey: "staff", icon: Users },
  { href: "/devices", labelKey: "devices", icon: Tablet },
  { href: "/attendance", labelKey: "attendance", icon: CalendarClock },
] as const;

// FR-006: inert placeholders for sections later features will build — never real links, so a
// director can't navigate to a half-built screen from the sidebar itself (direct URL entry is
// still handled by each route's own not-yet-available page, per spec Edge Cases).
const PLACEHOLDER_NAV = [
  { labelKey: "locations", icon: MapPin },
  { labelKey: "contracts", icon: FileText },
  { labelKey: "children", icon: Baby },
] as const;

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
              {!collapsed && <span className="truncate">{t(labelKey)}</span>}
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
