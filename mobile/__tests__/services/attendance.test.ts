jest.mock("../../services/apiClient", () => ({
  apiClient: { POST: jest.fn(), GET: jest.fn() },
}));

jest.mock("../../services/offlineQueue", () => {
  const mockEnqueue = jest.fn().mockResolvedValue("queued-id");
  return {
    enqueue: (...args: unknown[]) => mockEnqueue(...args),
    __mockEnqueue: mockEnqueue,
  };
});

jest.mock("../../services/syncEngine", () => ({ registerSyncHandler: jest.fn() }));

import { apiClient } from "../../services/apiClient";
import { checkIn, checkOut, markAbsent, getBkrRatio, todayDateString } from "../../services/attendance";
import { useStore } from "../../store/useStore";

const offlineQueueMock = jest.requireMock("../../services/offlineQueue") as { __mockEnqueue: jest.Mock };

beforeEach(() => {
  jest.clearAllMocks();
  useStore.setState({
    device: { deviceId: "device-1", locationId: "loc-1", groupId: "group-1", locationName: "Room A", groupName: "Group A" },
  });
});

describe("checkIn", () => {
  it("online: posts directly and returns the server response", async () => {
    (apiClient.POST as jest.Mock).mockResolvedValue({ response: { ok: true }, data: { id: "a1", childId: "c1", status: "present" } });

    const result = await checkIn("c1", "2026-01-05", true);

    expect(apiClient.POST).toHaveBeenCalledWith("/api/attendance/check-in", { body: { childId: "c1", date: "2026-01-05" } });
    expect(result).toEqual({ id: "a1", childId: "c1", status: "present" });
    expect(offlineQueueMock.__mockEnqueue).not.toHaveBeenCalled();
  });

  it("offline: enqueues a create and returns an optimistic present reconstruction", async () => {
    const result = await checkIn("c1", "2026-01-05", false);

    expect(offlineQueueMock.__mockEnqueue).toHaveBeenCalledWith(
      expect.objectContaining({ entityType: "attendance_record", operation: "create", endpoint: "/api/attendance/check-in", httpMethod: "POST" })
    );
    expect(result.childId).toBe("c1");
    expect(result.status).toBe("present");
    expect(result.checkInAt).toBeTruthy();
  });
});

describe("checkOut", () => {
  it("online: posts directly", async () => {
    (apiClient.POST as jest.Mock).mockResolvedValue({ response: { ok: true }, data: { id: "a1", childId: "c1", status: "present" } });

    await checkOut("c1", "2026-01-05", true);

    expect(apiClient.POST).toHaveBeenCalledWith("/api/attendance/check-out", { body: { childId: "c1", date: "2026-01-05" } });
    expect(offlineQueueMock.__mockEnqueue).not.toHaveBeenCalled();
  });

  // research.md R9 (corrected from the original plan): no onBeforeEnqueue merge with a pending
  // check-in — check-in and check-out are separate endpoints/shapes, so a check-out simply
  // enqueues its own update and relies on the sync engine's existing FIFO ordering to run after
  // the check-in it depends on.
  it("offline: always enqueues its own update, regardless of a pending check-in", async () => {
    const result = await checkOut("c1", "2026-01-05", false);

    expect(offlineQueueMock.__mockEnqueue).toHaveBeenCalledWith(
      expect.objectContaining({ entityType: "attendance_record", operation: "update", endpoint: "/api/attendance/check-out", httpMethod: "POST" })
    );
    expect(result).toBeNull();
  });
});

describe("markAbsent", () => {
  it("online: posts with the device's own locationId from useStore", async () => {
    (apiClient.POST as jest.Mock).mockResolvedValue({ response: { ok: true }, data: { id: "a1", childId: "c1", status: "absent" } });

    await markAbsent("c1", "2026-01-05", true, "Sick", true);

    expect(apiClient.POST).toHaveBeenCalledWith("/api/attendance/absence", {
      body: { childId: "c1", locationId: "loc-1", groupId: "group-1", date: "2026-01-05", absenceJustified: true, absenceReason: "Sick" },
    });
  });

  it("offline: enqueues a create with justified/reason", async () => {
    const result = await markAbsent("c1", "2026-01-05", false, null, false);

    expect(offlineQueueMock.__mockEnqueue).toHaveBeenCalledWith(
      expect.objectContaining({ entityType: "attendance_record", operation: "create", endpoint: "/api/attendance/absence" })
    );
    expect(result.status).toBe("absent");
    expect(result.absenceJustified).toBe(false);
  });
});

describe("getBkrRatio", () => {
  it("returns the parsed BKR response", async () => {
    (apiClient.GET as jest.Mock).mockResolvedValue({
      response: { ok: true },
      data: { presentCount: 5, qualifiedStaffCount: 1, isNapTime: false, threshold: 8, status: "green" },
    });

    const result = await getBkrRatio("loc-1");

    expect(apiClient.GET).toHaveBeenCalledWith("/api/attendance/bkr", { params: { query: { locationId: "loc-1" } } });
    expect(result.status).toBe("green");
  });
});

describe("todayDateString", () => {
  it("returns a yyyy-MM-dd formatted date", () => {
    expect(todayDateString()).toMatch(/^\d{4}-\d{2}-\d{2}$/);
  });
});
