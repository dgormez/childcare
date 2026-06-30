import { useStore } from "../../store/useStore";

beforeEach(() => {
  useStore.setState({ auth: null, habits: [], completions: [], isSyncing: false, lastSyncAt: null, isOnline: true });
});

describe("auth", () => {
  it("setAuth stores auth state", () => {
    useStore.getState().setAuth({ userId: "u1", email: "a@b.com", accessToken: "tok" });
    expect(useStore.getState().auth).toEqual({ userId: "u1", email: "a@b.com", accessToken: "tok" });
  });

  it("updateAccessToken replaces token only", () => {
    useStore.getState().setAuth({ userId: "u1", email: "a@b.com", accessToken: "old" });
    useStore.getState().updateAccessToken("new");
    expect(useStore.getState().auth?.accessToken).toBe("new");
    expect(useStore.getState().auth?.email).toBe("a@b.com");
  });

  it("updateAccessToken is a no-op when not logged in", () => {
    useStore.getState().updateAccessToken("x");
    expect(useStore.getState().auth).toBeNull();
  });

  it("resetAuth clears auth and habits", () => {
    useStore.getState().setAuth({ userId: "u1", email: "a@b.com", accessToken: "tok" });
    useStore.getState().addHabit({ id: "h1", userId: "u1", name: "Run", color: "#3b82f6", icon: "🏃", createdAt: "" });
    useStore.getState().resetAuth();
    expect(useStore.getState().auth).toBeNull();
    expect(useStore.getState().habits).toHaveLength(0);
  });
});

describe("habits", () => {
  const habit = { id: "h1", userId: "u1", name: "Run", color: "#3b82f6", icon: "🏃", createdAt: "" };

  it("addHabit appends to the list", () => {
    useStore.getState().addHabit(habit);
    expect(useStore.getState().habits[0]).toEqual(habit);
  });

  it("updateHabit replaces by id", () => {
    useStore.getState().addHabit(habit);
    useStore.getState().updateHabit({ ...habit, name: "Updated" });
    expect(useStore.getState().habits[0].name).toBe("Updated");
  });

  it("removeHabit removes by id", () => {
    useStore.getState().addHabit(habit);
    useStore.getState().removeHabit("h1");
    expect(useStore.getState().habits).toHaveLength(0);
  });

  it("setHabits replaces the entire list", () => {
    useStore.getState().addHabit(habit);
    useStore.getState().setHabits([{ ...habit, id: "h2" }]);
    expect(useStore.getState().habits).toHaveLength(1);
    expect(useStore.getState().habits[0].id).toBe("h2");
  });
});

describe("completions", () => {
  const completion = { id: "c1", habitId: "h1", userId: "u1", date: "2026-06-16", createdAt: "" };

  it("addCompletion appends", () => {
    useStore.getState().addCompletion(completion);
    expect(useStore.getState().completions).toHaveLength(1);
  });

  it("removeCompletion removes by habitId + date", () => {
    useStore.getState().addCompletion(completion);
    useStore.getState().removeCompletion("h1", "2026-06-16");
    expect(useStore.getState().completions).toHaveLength(0);
  });

  it("setCompletions replaces entire list", () => {
    useStore.getState().addCompletion(completion);
    useStore.getState().setCompletions([{ ...completion, id: "c2" }]);
    expect(useStore.getState().completions).toHaveLength(1);
    expect(useStore.getState().completions[0].id).toBe("c2");
  });
});
