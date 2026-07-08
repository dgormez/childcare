import { describe, it, expect, vi, beforeEach } from "vitest";

function createMemoryLocalStorage() {
  const store = new Map<string, string>();
  return {
    getItem: (key: string) => store.get(key) ?? null,
    setItem: (key: string, value: string) => void store.set(key, value),
    removeItem: (key: string) => void store.delete(key),
    clear: () => store.clear(),
  } satisfies Pick<Storage, "getItem" | "setItem" | "removeItem" | "clear">;
}

let memoryLocalStorage = createMemoryLocalStorage();

beforeEach(() => {
  vi.unstubAllGlobals();
  vi.resetModules();
  memoryLocalStorage = createMemoryLocalStorage();
  vi.stubGlobal("localStorage", memoryLocalStorage);
  // apiClient.ts reads this once at import time to build its openapi-fetch baseUrl — an empty
  // string resolves fine against `window.location` in a real browser, but Node's fetch needs an
  // absolute URL.
  vi.stubEnv("NEXT_PUBLIC_API_BASE_URL", "http://localhost:5099");
});

// ── login ─────────────────────────────────────────────────────────────────────

describe("login", () => {
  it("succeeds and returns a session including the organisation name", async () => {
    const authResponse = {
      accessToken: "tok_abc",
      refreshToken: "rt_xyz",
      user: { id: "u1", email: "director@acme.test", emailVerified: true, role: "director", name: "Jane Director" },
    };

    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation((input: string | Request) => {
        const url = typeof input === "string" ? input : input.url;
        if (url.endsWith("/api/auth/login")) {
          return Promise.resolve(new Response(JSON.stringify(authResponse), { status: 200 }));
        }
        if (url === "/api/set-refresh-token") {
          return Promise.resolve(new Response(null, { status: 200 }));
        }
        // GET /api/organisations/me
        return Promise.resolve(new Response(JSON.stringify({ name: "Acme KDV" }), { status: 200 }));
      }),
    );

    const { login } = await import("../lib/auth");
    const session = await login("acme", "director@acme.test", "password123");

    expect(session.user.email).toBe("director@acme.test");
    expect(session.organisationSlug).toBe("acme");
    expect(session.organisationName).toBe("Acme KDV");
    expect(memoryLocalStorage.getItem("childcare_organisation_slug")).toBe("acme");
  });

  it("throws with the server's errorKey on invalid credentials", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ errorKey: "errors.auth.invalid_credentials" }), { status: 401 }),
      ),
    );

    const { login } = await import("../lib/auth");
    await expect(login("acme", "director@acme.test", "wrong-password")).rejects.toThrow(
      "errors.auth.invalid_credentials",
    );
  });
});

// ── tryRestoreSession ─────────────────────────────────────────────────────────

describe("tryRestoreSession", () => {
  it("returns null when no organisation slug was ever stored", async () => {
    const { tryRestoreSession } = await import("../lib/auth");
    expect(await tryRestoreSession()).toBeNull();
  });

  it("returns null when /api/refresh responds not ok", async () => {
    memoryLocalStorage.setItem("childcare_organisation_slug", "acme");
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false }));
    const { tryRestoreSession } = await import("../lib/auth");
    expect(await tryRestoreSession()).toBeNull();
  });

  it("returns null when fetch throws", async () => {
    memoryLocalStorage.setItem("childcare_organisation_slug", "acme");
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new Error("offline")));
    const { tryRestoreSession } = await import("../lib/auth");
    expect(await tryRestoreSession()).toBeNull();
  });

  it("restores the session, including the organisation name, when /api/refresh succeeds", async () => {
    memoryLocalStorage.setItem("childcare_organisation_slug", "acme");
    const authResponse = {
      accessToken: "tok_abc",
      refreshToken: "rt_xyz",
      user: { id: "u1", email: "test@example.com", emailVerified: true, role: "director", name: "Jane Director" },
    };

    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation((url: string) => {
        if (url === "/api/refresh") return Promise.resolve({ ok: true, json: async () => authResponse });
        // GET /api/organisations/me, called by apiClient inside fetchOrganisationName — a real
        // Response instance, since openapi-fetch's middleware pipeline requires one.
        return Promise.resolve(
          new Response(JSON.stringify({ name: "Acme KDV" }), {
            status: 200,
            headers: { "content-type": "application/json" },
          }),
        );
      }),
    );

    const { tryRestoreSession } = await import("../lib/auth");
    const session = await tryRestoreSession();

    expect(session).not.toBeNull();
    expect(session!.user.email).toBe("test@example.com");
    expect(session!.user.name).toBe("Jane Director");
    expect(session!.accessToken).toBe("tok_abc");
    expect(session!.organisationSlug).toBe("acme");
    expect(session!.organisationName).toBe("Acme KDV");
  });
});

// ── logout ────────────────────────────────────────────────────────────────────

describe("logout", () => {
  it("calls the logout BFF route and clears the stored organisation slug", async () => {
    memoryLocalStorage.setItem("childcare_organisation_slug", "acme");
    const mockFetch = vi.fn().mockResolvedValue({ ok: true });
    vi.stubGlobal("fetch", mockFetch);

    const { logout } = await import("../lib/auth");
    await logout();

    expect(mockFetch).toHaveBeenCalledWith("/api/logout", { method: "POST" });
    expect(memoryLocalStorage.getItem("childcare_organisation_slug")).toBeNull();
  });
});
