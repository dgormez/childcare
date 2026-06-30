"use client";
import { useState } from "react";
import Link from "next/link";
import { forgotPassword } from "../../../lib/api";

export default function ForgotPasswordPage() {
  const [email,   setEmail]   = useState("");
  const [sent,    setSent]    = useState(false);
  const [error,   setError]   = useState("");
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    setLoading(true);
    try {
      await forgotPassword(email);
      setSent(true);
    } catch {
      setError("Something went wrong. Please try again.");
    } finally {
      setLoading(false);
    }
  };

  if (sent) {
    return (
      <div className="text-center space-y-4">
        <div className="text-5xl">📬</div>
        <h2 className="text-xl font-bold text-gray-900">Check your inbox</h2>
        <p className="text-gray-500 text-sm">
          If <strong>{email}</strong> is registered, we&apos;ve sent a reset link.
        </p>
        <Link href="/login" className="text-blue-600 hover:underline text-sm">Back to sign in</Link>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <div>
        <h2 className="text-xl font-bold text-gray-900 mb-1">Reset password</h2>
        <p className="text-sm text-gray-500 mb-4">Enter your email and we&apos;ll send a reset link.</p>
        <label className="block text-sm font-medium text-gray-700 mb-1">Email</label>
        <input
          type="email" required
          value={email} onChange={(e) => setEmail(e.target.value)}
          className="w-full border border-gray-300 rounded-xl px-4 py-3 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          placeholder="you@example.com"
        />
      </div>

      {error && <p className="text-red-500 text-sm">{error}</p>}

      <button
        type="submit" disabled={loading}
        className="w-full bg-blue-600 hover:bg-blue-700 text-white font-semibold py-3 rounded-xl transition disabled:opacity-50"
      >
        {loading ? "Sending…" : "Send reset link"}
      </button>

      <p className="text-center">
        <Link href="/login" className="text-sm text-blue-600 hover:underline">Back to sign in</Link>
      </p>
    </form>
  );
}
