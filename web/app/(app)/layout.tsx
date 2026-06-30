"use client";
import { useEffect } from "react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useAuth } from "../../components/AuthProvider";
import { logout } from "../../lib/auth";

const NAV = [
  { href: "/habits",       label: "Today",        icon: "📅" },
  { href: "/habits/manage", label: "My Habits",   icon: "📋" },
  { href: "/subscription", label: "Pro",           icon: "⭐" },
  { href: "/settings",     label: "Settings",      icon: "⚙️" },
];

export default function AppLayout({ children }: { children: React.ReactNode }) {
  const { session, setSession, loading } = useAuth();
  const router   = useRouter();
  const pathname = usePathname();

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
      <div className="min-h-screen flex items-center justify-center">
        <div className="w-8 h-8 border-4 border-blue-600 border-t-transparent rounded-full animate-spin" />
      </div>
    );
  }

  return (
    <div className="min-h-screen flex">
      {/* Sidebar */}
      <aside className="w-56 bg-white border-r border-gray-200 flex flex-col py-6 px-3 fixed h-full">
        <div className="px-3 mb-6">
          <span className="text-xs font-semibold text-gray-400 uppercase tracking-wider">ChildCare</span>
        </div>

        <nav className="flex-1 space-y-1">
          {NAV.map((item) => {
            const active = item.href === "/habits"
              ? pathname === "/habits"
              : pathname.startsWith(item.href);
            return (
              <Link
                key={item.href}
                href={item.href}
                className={`flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium transition ${
                  active
                    ? "bg-blue-50 text-blue-700"
                    : "text-gray-600 hover:bg-gray-100"
                }`}
              >
                <span className="text-lg">{item.icon}</span>
                {item.label}
              </Link>
            );
          })}
        </nav>

        <div className="px-3 pt-4 border-t border-gray-100">
          <p className="text-xs text-gray-400 truncate mb-2">{session.user.email}</p>
          <button
            onClick={handleLogout}
            className="text-sm text-gray-500 hover:text-gray-900 transition"
          >
            Sign out
          </button>
        </div>
      </aside>

      {/* Main */}
      <main className="flex-1 ml-56 p-8">
        {children}
      </main>
    </div>
  );
}
