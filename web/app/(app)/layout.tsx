"use client";
import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "../../components/AuthProvider";
import { Sidebar } from "../../components/Sidebar";
import { logout } from "../../lib/auth";

export default function AppLayout({ children }: { children: React.ReactNode }) {
  const { session, setSession, loading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!loading && !session) router.replace("/login");
  }, [loading, session, router]);

  const handleLogout = async () => {
    await logout();
    setSession(null);
    router.replace("/login");
  };

  if (loading || !session) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-background dark:bg-background-dark">
        <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
      </div>
    );
  }

  return (
    <div className="flex h-screen overflow-hidden bg-background dark:bg-background-dark print:block print:h-auto print:overflow-visible">
      {/* print:hidden — feature 013d's Maaltijdenlijst page is this app's first print-oriented
          screen; navigation chrome has no place on a printed kitchen sheet (contracts/
          meal-list-api.md's "no PDF" decision relies on the browser's own print output). */}
      <div className="print:hidden">
        <Sidebar session={session} onLogout={handleLogout} />
      </div>
      <main className="flex-1 overflow-y-auto p-8 print:overflow-visible print:p-0">{children}</main>
    </div>
  );
}
