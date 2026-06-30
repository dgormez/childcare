"use client";
import { useState } from "react";
import { useRouter } from "next/navigation";
import toast from "react-hot-toast";
import { useAuth } from "../../../components/AuthProvider";
import { logout } from "../../../lib/auth";
import { forgotPassword, deleteAccount } from "../../../lib/api";

export default function SettingsPage() {
  const { session, setSession } = useAuth();
  const router = useRouter();
  const [resetSent,    setResetSent]    = useState(false);
  const [resetLoading, setResetLoading] = useState(false);
  const [deleting,     setDeleting]     = useState(false);

  const handleLogout = async () => {
    await logout();
    setSession(null);
    router.replace("/login");
  };

  const handleResetPassword = async () => {
    if (!session?.user.email) return;
    setResetLoading(true);
    try {
      await forgotPassword(session.user.email);
      setResetSent(true);
    } catch {
      toast.error("Something went wrong. Please try again.");
    } finally {
      setResetLoading(false);
    }
  };

  const handleDeleteAccount = async () => {
    if (!confirm("This will permanently delete your account and all your data. This cannot be undone.")) return;
    setDeleting(true);
    try {
      await deleteAccount();
      await logout();
      setSession(null);
      router.replace("/login");
    } catch {
      toast.error("Failed to delete account. Please try again.");
      setDeleting(false);
    }
  };

  return (
    <div className="max-w-md">
      <h1 className="text-2xl font-bold text-gray-900 mb-6">Settings</h1>

      {/* Account */}
      <section className="mb-8">
        <h2 className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-3">Account</h2>
        <div className="bg-white border border-gray-200 rounded-xl divide-y divide-gray-100">
          <div className="px-4 py-3.5">
            <p className="text-xs text-gray-400 mb-0.5">Email</p>
            <p className="text-sm font-medium text-gray-900">{session?.user.email}</p>
          </div>
          <div className="px-4 py-3.5">
            <button
              onClick={handleResetPassword}
              disabled={resetLoading || resetSent}
              className="text-sm text-blue-600 hover:text-blue-700 font-medium disabled:opacity-50"
            >
              {resetSent ? "✓ Reset email sent" : resetLoading ? "Sending…" : "Reset password"}
            </button>
          </div>
        </div>
      </section>

      {/* Session */}
      <section className="mb-8">
        <h2 className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-3">Session</h2>
        <div className="bg-white border border-gray-200 rounded-xl">
          <div className="px-4 py-3.5">
            <button
              onClick={handleLogout}
              className="text-sm text-gray-700 hover:text-gray-900 font-medium"
            >
              Sign out
            </button>
          </div>
        </div>
      </section>

      {/* About */}
      <section className="mb-8">
        <h2 className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-3">About</h2>
        <div className="bg-white border border-gray-200 rounded-xl divide-y divide-gray-100">
          <div className="px-4 py-3.5 flex justify-between">
            <span className="text-sm text-gray-500">App</span>
            <span className="text-sm text-gray-900">ChildCare</span>
          </div>
          <div className="px-4 py-3.5 flex justify-between">
            <span className="text-sm text-gray-500">Stack</span>
            <span className="text-sm text-gray-900">Next.js 15 + ASP.NET Core 10</span>
          </div>
        </div>
      </section>

      {/* Danger zone */}
      <section>
        <h2 className="text-xs font-semibold text-red-400 uppercase tracking-wider mb-3">Danger zone</h2>
        <div className="bg-white border border-red-100 rounded-xl">
          <div className="px-4 py-3.5">
            <p className="text-xs text-gray-400 mb-2">
              Permanently deletes your account and all data. This cannot be undone.
            </p>
            <button
              onClick={handleDeleteAccount}
              disabled={deleting}
              className="text-sm text-red-500 hover:text-red-600 font-medium disabled:opacity-50"
            >
              {deleting ? "Deleting…" : "Delete account"}
            </button>
          </div>
        </div>
      </section>
    </div>
  );
}
