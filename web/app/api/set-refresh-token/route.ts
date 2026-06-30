import { NextRequest, NextResponse } from "next/server";
import { cookies } from "next/headers";

export async function POST(req: NextRequest) {
  const { refreshToken } = await req.json();
  if (!refreshToken) return NextResponse.json({ error: "Missing token" }, { status: 400 });

  const jar = await cookies();
  jar.set("refresh_token", refreshToken, {
    httpOnly: true,
    secure:   process.env.NODE_ENV === "production",
    sameSite: "lax",
    path:     "/",
    maxAge:   60 * 60 * 24 * 30, // 30 days
  });

  return NextResponse.json({ ok: true });
}
