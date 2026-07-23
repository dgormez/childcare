"use client";
import { useEffect } from "react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { Mail, Building2, Syringe } from "lucide-react";
import { useAuth } from "../../../components/AuthProvider";
import { cn } from "../../../lib/cn";

// research.md R10: a shared section shell so a future platform-admin dataset doesn't require
// rebuilding this — retrofits 013h's previously-standalone vaccine-types screen in alongside the
// new Invitations/Organisations screens (User Story 5).
const PLATFORM_ADMIN_SECTIONS = [
  { href: "/platform-admin/invitations", labelKey: "invitations", icon: Mail },
  { href: "/platform-admin/organisations", labelKey: "organisations", icon: Building2 },
  { href: "/platform-admin/vaccine-types", labelKey: "vaccineTypes", icon: Syringe },
] as const;

export default function PlatformAdminLayout({ children }: { children: React.ReactNode }) {
  const { session } = useAuth();
  const router = useRouter();
  const pathname = usePathname();
  const t = useTranslations("platformAdmin.nav");

  // FR-016: redirects (not a 404) for a director without the flag, matching AppLayout's
  // existing unauthenticated-redirect convention and 013h's own vaccine-types precedent — now
  // centralized here so every platform-admin route is gated in one place.
  useEffect(() => {
    if (session && !session.user.isPlatformAdmin) router.replace("/dashboard");
  }, [session, router]);

  if (session && !session.user.isPlatformAdmin) return null;

  return (
    <div>
      <nav className="mb-6 flex items-center gap-1 border-b border-border pb-3 dark:border-border-dark">
        {PLATFORM_ADMIN_SECTIONS.map(({ href, labelKey, icon: Icon }) => {
          const active = pathname.startsWith(href);
          return (
            <Link
              key={href}
              href={href}
              className={cn(
                "flex items-center gap-2 rounded-lg px-3 py-2 text-sm font-medium transition",
                active
                  ? "bg-primary-soft text-primary-hover dark:bg-primary-soft-dark dark:text-primary-hover-dark"
                  : "text-text-soft hover:bg-surface-soft dark:text-text-soft-dark dark:hover:bg-surface-soft-dark",
              )}
            >
              <Icon className="h-4 w-4" strokeWidth={2} />
              {t(labelKey)}
            </Link>
          );
        })}
      </nav>
      {children}
    </div>
  );
}
