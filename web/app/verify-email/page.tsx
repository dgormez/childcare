"use client";
import { useEffect, useState, Suspense } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { verifyEmail } from "../../lib/api";
import { getSession } from "../../lib/auth";

function VerifyEmailContent() {
  const searchParams = useSearchParams();
  const router       = useRouter();
  const token        = searchParams.get("token") ?? "";

  const [status, setStatus] = useState<"loading" | "success" | "error">("loading");

  useEffect(() => {
    if (!token) { setStatus("error"); return; }
    verifyEmail(token)
      .then(() => setStatus("success"))
      .catch(() => setStatus("error"));
  }, [token]);

  const handleContinue = () => {
    // Go to the app if already logged in, otherwise to login
    router.replace(getSession() ? "/habits" : "/login");
  };

  if (status === "loading") {
    return (
      <div className="text-center space-y-3">
        <div className="text-4xl animate-pulse">✉️</div>
        <p className="text-gray-500 text-sm">Verifying your email…</p>
      </div>
    );
  }

  if (status === "success") {
    return (
      <div className="text-center space-y-4">
        <div className="text-5xl">✅</div>
        <h2 className="text-xl font-bold text-gray-900">Email verified!</h2>
        <p className="text-gray-500 text-sm">Your account is now fully active.</p>
        <button
          onClick={handleContinue}
          className="w-full bg-blue-600 hover:bg-blue-700 text-white font-semibold py-3 rounded-xl transition"
        >
          Continue to app
        </button>
      </div>
    );
  }

  return (
    <div className="text-center space-y-4">
      <div className="text-5xl">❌</div>
      <h2 className="text-xl font-bold text-gray-900">Link expired</h2>
      <p className="text-gray-500 text-sm">
        This verification link is invalid or has expired.
      </p>
      <Link
        href="/login"
        className="block w-full bg-blue-600 hover:bg-blue-700 text-white font-semibold py-3 rounded-xl text-center transition"
      >
        Back to sign in
      </Link>
    </div>
  );
}

export default function VerifyEmailPage() {
  return (
    <div className="min-h-screen flex items-center justify-center px-4">
      <div className="w-full max-w-sm">
        <div className="text-center mb-8">
          <h1 className="text-3xl font-bold text-gray-900">ChildCare</h1>
          <p className="text-gray-500 text-sm mt-1">Habit Tracker</p>
        </div>
        <Suspense>
          <VerifyEmailContent />
        </Suspense>
      </div>
    </div>
  );
}
