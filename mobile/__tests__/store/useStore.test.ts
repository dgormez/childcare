import { useStore } from "../../store/useStore";

const authState = { userId: "u1", email: "a@b.com", role: "staff", organisationSlug: "org-a", accessToken: "tok" };

beforeEach(() => {
  useStore.setState({ auth: null });
});

describe("auth", () => {
  it("setAuth stores auth state", () => {
    useStore.getState().setAuth(authState);
    expect(useStore.getState().auth).toEqual(authState);
  });

  it("updateAccessToken replaces token only", () => {
    useStore.getState().setAuth(authState);
    useStore.getState().updateAccessToken("new");
    expect(useStore.getState().auth?.accessToken).toBe("new");
    expect(useStore.getState().auth?.email).toBe("a@b.com");
  });

  it("updateAccessToken is a no-op when not logged in", () => {
    useStore.getState().updateAccessToken("x");
    expect(useStore.getState().auth).toBeNull();
  });

  it("resetAuth clears auth", () => {
    useStore.getState().setAuth(authState);
    useStore.getState().resetAuth();
    expect(useStore.getState().auth).toBeNull();
  });
});
