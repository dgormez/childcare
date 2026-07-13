import { getChildHealthSummary, healthSummaryCacheKey } from "../../services/healthSummary";
import type { ChildHealthSummaryResponse } from "../../types";

jest.mock("../../services/apiClient", () => ({
  apiClient: { GET: jest.fn() },
}));

jest.mock("../../services/readCache", () => ({
  getCached: jest.fn(),
  setCached: jest.fn(),
}));

const { apiClient } = require("../../services/apiClient");
const { getCached, setCached } = require("../../services/readCache");

const summary: ChildHealthSummaryResponse = {
  childId: "child-1",
  activeHealthRecords: [],
  dueSoonVaccines: [{ vaccineName: "DTP", nextDueDate: "2026-07-20", isOverdue: false }],
};

beforeEach(() => {
  jest.clearAllMocks();
});

describe("getChildHealthSummary (feature 013c, US4/AC4)", () => {
  it("fetches from the network and caches the result on success", async () => {
    apiClient.GET.mockResolvedValue({ response: { ok: true }, data: summary });

    const result = await getChildHealthSummary("child-1");

    expect(result).toEqual({ status: "loaded", summary });
    expect(setCached).toHaveBeenCalledWith(healthSummaryCacheKey("child-1"), summary);
  });

  it("falls back to the cached value when the network request fails (offline, spec.md Edge Cases)", async () => {
    apiClient.GET.mockRejectedValue(new Error("network down"));
    getCached.mockReturnValue(summary);

    const result = await getChildHealthSummary("child-1");

    expect(result).toEqual({ status: "loaded", summary });
    expect(getCached).toHaveBeenCalledWith(healthSummaryCacheKey("child-1"));
  });

  it("falls back to the cached value when the response is not ok", async () => {
    apiClient.GET.mockResolvedValue({ response: { ok: false }, data: undefined });
    getCached.mockReturnValue(summary);

    const result = await getChildHealthSummary("child-1");

    expect(result).toEqual({ status: "loaded", summary });
  });

  it("returns 'unavailable' when the network fails and nothing is cached", async () => {
    apiClient.GET.mockRejectedValue(new Error("network down"));
    getCached.mockReturnValue(null);

    const result = await getChildHealthSummary("child-1");

    expect(result).toEqual({ status: "unavailable" });
  });
});
