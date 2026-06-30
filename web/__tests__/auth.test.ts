import { describe, it, expect, vi, beforeEach } from "vitest";
import { getSession } from "../lib/auth";

beforeEach(() => vi.unstubAllGlobals());

// ── getSession ────────────────────────────────────────────────────────────────

describe("getSession", () => {
  it("returns null before any login", () => {
    expect(getSession()).toBeNull();
  });
});

// ── tryRestoreSession ─────────────────────────────────────────────────────────

describe("tryRestoreSession", () => {
  it("returns null when /api/refresh responds not ok", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false }));
    const { tryRestoreSession } = await import("../lib/auth");
    const session = await tryRestoreSession();
    expect(session).toBeNull();
    vi.resetModules();
  });

  it("returns null when fetch throws", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new Error("offline")));
    const { tryRestoreSession } = await import("../lib/auth");
    const session = await tryRestoreSession();
    expect(session).toBeNull();
    vi.resetModules();
  });

  it("restores session when /api/refresh returns a valid AuthResponse", async () => {
    const authResponse = {
      accessToken:  "tok_abc",
      refreshToken: "rt_xyz",
      user:         { id: "u1", email: "test@example.com" },
    };

    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
      ok:   true,
      json: async () => authResponse,
    }));

    const { tryRestoreSession, getSession } = await import("../lib/auth");
    const session = await tryRestoreSession();

    expect(session).not.toBeNull();
    expect(session!.user.email).toBe("test@example.com");
    expect(session!.accessToken).toBe("tok_abc");
    expect(getSession()).toEqual(session);

    vi.resetModules();
  });
});

// ── logout ────────────────────────────────────────────────────────────────────

describe("logout", () => {
  it("clears the session and access token", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true }));

    const { logout, getSession } = await import("../lib/auth");
    await logout();

    expect(getSession()).toBeNull();
    vi.resetModules();
  });
});
