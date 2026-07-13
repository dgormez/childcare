import { getMealList, mealListCacheKey } from "../../services/mealList";
import type { MealListResponse } from "../../types";

jest.mock("../../services/apiClient", () => ({
  apiClient: { GET: jest.fn() },
}));

jest.mock("../../services/readCache", () => ({
  getCached: jest.fn(),
  setCached: jest.fn(),
}));

jest.mock("../../store/useStore", () => ({
  useStore: { getState: jest.fn(() => ({ device: { locationId: "loc-1" } })) },
}));

const { apiClient } = require("../../services/apiClient");
const { getCached, setCached } = require("../../services/readCache");
const { useStore } = require("../../store/useStore");

const mealList: MealListResponse = {
  date: "2026-07-13",
  groups: [{ groupId: "g1", groupName: "Butterflies", children: [] }],
  expected: null,
};

beforeEach(() => {
  jest.clearAllMocks();
  useStore.getState.mockReturnValue({ device: { locationId: "loc-1" } });
});

describe("getMealList (feature 013d, US1)", () => {
  it("fetches from the network and caches the result on success", async () => {
    apiClient.GET.mockResolvedValue({ response: { ok: true }, data: mealList });

    const result = await getMealList();

    expect(result).toEqual({ status: "loaded", mealList });
    expect(setCached).toHaveBeenCalledWith(expect.stringContaining("meal-list:loc-1:"), mealList);
  });

  it("falls back to the cached value when the network request fails (offline, spec.md FR-014)", async () => {
    apiClient.GET.mockRejectedValue(new Error("network down"));
    getCached.mockReturnValue(mealList);

    const result = await getMealList();

    expect(result).toEqual({ status: "loaded", mealList });
  });

  it("returns 'unavailable' when the network fails and nothing is cached", async () => {
    apiClient.GET.mockRejectedValue(new Error("network down"));
    getCached.mockReturnValue(null);

    const result = await getMealList();

    expect(result).toEqual({ status: "unavailable" });
  });

  it("returns 'unavailable' immediately when the device has no paired location", async () => {
    useStore.getState.mockReturnValue({ device: null });

    const result = await getMealList();

    expect(result).toEqual({ status: "unavailable" });
    expect(apiClient.GET).not.toHaveBeenCalled();
  });

  it("uses a cache key derived from locationId and today's date", () => {
    expect(mealListCacheKey("loc-1", "2026-07-13")).toBe("meal-list:loc-1:2026-07-13");
  });
});
