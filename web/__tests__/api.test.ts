import { describe, it, expect, vi, beforeEach } from "vitest";
import {
  setAccessToken,
  clearAccessToken,
  getAccessToken,
  checkHealth,
} from "../lib/api";

// Each test gets a clean token state
beforeEach(() => clearAccessToken());

// ── Token helpers ─────────────────────────────────────────────────────────────

describe("access token helpers", () => {
  it("starts empty", () => {
    expect(getAccessToken()).toBe("");
  });

  it("set then get returns the token", () => {
    setAccessToken("tok_abc");
    expect(getAccessToken()).toBe("tok_abc");
  });

  it("clear empties the token", () => {
    setAccessToken("tok_abc");
    clearAccessToken();
    expect(getAccessToken()).toBe("");
  });
});

// ── checkHealth ───────────────────────────────────────────────────────────────

describe("checkHealth", () => {
  it("returns true when the health endpoint responds ok", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true }));
    expect(await checkHealth()).toBe(true);
    vi.unstubAllGlobals();
  });

  it("returns false when the health endpoint responds not ok", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false }));
    expect(await checkHealth()).toBe(false);
    vi.unstubAllGlobals();
  });

  it("returns false when fetch throws (network error)", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new Error("network")));
    expect(await checkHealth()).toBe(false);
    vi.unstubAllGlobals();
  });
});

// ── request — 401 retry + SESSION_EXPIRED ─────────────────────────────────────

describe("request (via login)", () => {
  it("retries with a new token after a 401 and succeeds", async () => {
    // login() calls POST /api/auth/login; we simulate:
    //   1st fetch → 401 (expired access token)
    //   2nd fetch → POST /api/refresh → returns new access token
    //   3rd fetch → retry of original request → 200
    const mockFetch = vi
      .fn()
      .mockResolvedValueOnce({
        // original request → 401
        status: 401,
        ok: false,
        statusText: "Unauthorized",
        text: async () => "",
      })
      .mockResolvedValueOnce({
        // /api/refresh → ok, returns new access token
        ok: true,
        json: async () => ({ accessToken: "tok_new" }),
      })
      .mockResolvedValueOnce({
        // retry of original → 200
        status: 200,
        ok: true,
        json: async () => ({ accessToken: "tok_new", refreshToken: "rt", user: { id: "1", email: "a@b.com" } }),
      });

    vi.stubGlobal("fetch", mockFetch);

    const { login } = await import("../lib/api");
    const result = await login("a@b.com", "password");

    expect(result.accessToken).toBe("tok_new");
    expect(mockFetch).toHaveBeenCalledTimes(3);

    vi.unstubAllGlobals();
    vi.resetModules();
  });

  it("throws SESSION_EXPIRED when refresh also fails", async () => {
    const mockFetch = vi
      .fn()
      .mockResolvedValueOnce({
        status: 401,
        ok: false,
        statusText: "Unauthorized",
        text: async () => "",
      })
      .mockResolvedValueOnce({
        // /api/refresh fails
        ok: false,
      });

    vi.stubGlobal("fetch", mockFetch);

    const { login } = await import("../lib/api");
    await expect(login("a@b.com", "password")).rejects.toThrow("SESSION_EXPIRED");

    vi.unstubAllGlobals();
    vi.resetModules();
  });
});
