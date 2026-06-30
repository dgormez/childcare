import { NextResponse } from "next/server";
import { cookies } from "next/headers";

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "";

export async function POST() {
  const jar = await cookies();
  const refreshToken = jar.get("refresh_token")?.value;
  if (!refreshToken) return NextResponse.json({ error: "No refresh token" }, { status: 401 });

  const upstream = await fetch(`${API_BASE}/api/auth/refresh`, {
    method:  "POST",
    headers: { "Content-Type": "application/json" },
    body:    JSON.stringify({ refreshToken }),
  });

  if (!upstream.ok) {
    jar.delete("refresh_token");
    return NextResponse.json({ error: "Session expired" }, { status: 401 });
  }

  const data = await upstream.json();

  // Rotate the refresh token cookie
  jar.set("refresh_token", data.refreshToken, {
    httpOnly: true,
    secure:   process.env.NODE_ENV === "production",
    sameSite: "lax",
    path:     "/",
    maxAge:   60 * 60 * 24 * 30,
  });

  return NextResponse.json(data);
}
