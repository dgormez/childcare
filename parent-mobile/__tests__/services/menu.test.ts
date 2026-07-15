import { getMonthlyMenu, monthlyMenuCacheKey } from "../../services/menu";
import type { ParentMonthlyMenuEntry } from "../../types";

jest.mock("../../services/apiClient", () => ({
  apiClient: { GET: jest.fn() },
}));

const { apiClient } = require("../../services/apiClient");

const entries: ParentMonthlyMenuEntry[] = [
  { locationId: "loc-1", locationName: "KDV Zonnebloem", childId: "c1", childName: "Timmy", resolvedVariant: null, isPublished: true, days: [], closureDates: [] },
];

beforeEach(() => {
  jest.clearAllMocks();
});

describe("getMonthlyMenu (feature 013e, US2)", () => {
  it("fetches from the network and caches the result on success", async () => {
    apiClient.GET.mockResolvedValue({ response: { ok: true }, data: entries });

    const result = await getMonthlyMenu(2027, 6);

    expect(result).toEqual({ status: "loaded", entries });
  });

  it("falls back to the in-session cache when a later fetch for the same month fails (offline)", async () => {
    apiClient.GET.mockResolvedValueOnce({ response: { ok: true }, data: entries });
    await getMonthlyMenu(2027, 6);

    apiClient.GET.mockRejectedValueOnce(new Error("network down"));
    const result = await getMonthlyMenu(2027, 6);

    expect(result).toEqual({ status: "loaded", entries });
  });

  it("falls back to the cache when the response is not ok", async () => {
    apiClient.GET.mockResolvedValueOnce({ response: { ok: true }, data: entries });
    await getMonthlyMenu(2027, 6);

    apiClient.GET.mockResolvedValueOnce({ response: { ok: false }, data: undefined });
    const result = await getMonthlyMenu(2027, 6);

    expect(result).toEqual({ status: "loaded", entries });
  });

  it("returns 'unavailable' when the fetch fails and nothing is cached for that month", async () => {
    apiClient.GET.mockRejectedValue(new Error("network down"));

    const result = await getMonthlyMenu(2028, 1);

    expect(result).toEqual({ status: "unavailable" });
  });

  it("keys the cache per year/month, not shared across months", async () => {
    apiClient.GET.mockResolvedValueOnce({ response: { ok: true }, data: entries });
    await getMonthlyMenu(2027, 7);

    apiClient.GET.mockRejectedValueOnce(new Error("network down"));
    const result = await getMonthlyMenu(2027, 8);

    expect(result).toEqual({ status: "unavailable" });
    expect(monthlyMenuCacheKey(2027, 7)).not.toEqual(monthlyMenuCacheKey(2027, 8));
  });
});
