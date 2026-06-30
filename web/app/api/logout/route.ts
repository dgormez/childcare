import { NextResponse } from "next/server";
import { cookies } from "next/headers";

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "";

export async function POST() {
  const jar          = await cookies();
  const refreshToken = jar.get("refresh_token")?.value;

  if (refreshToken) {
    // Best-effort revocation — don't block logout if the backend is unreachable
    try {
      await fetch(`${API_BASE}/api/auth/logout`, {
        method:  "POST",
        headers: { "Content-Type": "application/json" },
        body:    JSON.stringify({ refreshToken }),
      });
    } catch { /* ignore */ }
  }

  jar.delete("refresh_token");
  return NextResponse.json({ ok: true });
}
