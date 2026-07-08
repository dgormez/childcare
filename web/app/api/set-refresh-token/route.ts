import { NextRequest, NextResponse } from "next/server";
import { cookies } from "next/headers";

export async function POST(req: NextRequest) {
  const { refreshToken, organisationSlug } = await req.json();
  if (!refreshToken || !organisationSlug) {
    return NextResponse.json({ error: "Missing token or organisationSlug" }, { status: 400 });
  }

  const jar = await cookies();
  const cookieOptions = {
    httpOnly: true,
    secure:   process.env.NODE_ENV === "production",
    sameSite: "lax" as const,
    path:     "/",
    maxAge:   60 * 60 * 24 * 30, // 30 days
  };
  jar.set("refresh_token", refreshToken, cookieOptions);
  // Feature 003 requires organisationSlug on every refresh call — stored alongside the refresh
  // token itself so /api/refresh doesn't need the client to resend it (found while wiring
  // feature 007a: the pre-existing /api/refresh route never sent this field at all).
  jar.set("org_slug", organisationSlug, cookieOptions);

  return NextResponse.json({ ok: true });
}
