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
import { fileIncidentReport } from "../../services/incidentReports";

const offlineQueueMock = jest.requireMock("../../services/offlineQueue") as { __mockEnqueue: jest.Mock };

const baseInput = {
  childId: "child-1",
  occurredAt: "2026-01-01T10:00:00.000Z",
  description: "Scraped knee on the playground.",
  injuryType: "scrape",
  doctorCalled: false,
  parentNotified: false,
};

beforeEach(() => {
  jest.clearAllMocks();
});

describe("fileIncidentReport", () => {
  it("online: posts directly and returns the server response with reportedBy resolved server-side", async () => {
    const serverResponse = {
      id: "report-1", childId: "child-1", locationId: "loc-1", reportedBy: ["staff-1"],
      description: baseInput.description, injuryType: "scrape",
    };
    (apiClient.POST as jest.Mock).mockResolvedValue({ response: { ok: true }, data: serverResponse });

    const result = await fileIncidentReport(baseInput, true);

    expect(apiClient.POST).toHaveBeenCalledWith(
      "/api/incident-reports",
      expect.objectContaining({ body: expect.objectContaining({ childId: "child-1", description: baseInput.description }) })
    );
    expect(result).toEqual(serverResponse);
    expect(offlineQueueMock.__mockEnqueue).not.toHaveBeenCalled();
  });

  // FR-014: submitting the incident form while offline queues to offline_queue
  // (entity_type = "incident_report"), shows immediately with a pending-sync indicator, and
  // surfaces no error.
  it("offline: queues the report and returns an optimistic reconstruction with no error", async () => {
    const result = await fileIncidentReport(baseInput, false);

    expect(offlineQueueMock.__mockEnqueue).toHaveBeenCalledWith(
      expect.objectContaining({ entityType: "incident_report", operation: "create", httpMethod: "POST" })
    );
    expect(result.childId).toBe("child-1");
    expect(result.description).toBe(baseInput.description);
    expect(result.reportedBy).toEqual([]);
  });

  it("offline: a same-session re-file gets a distinct local id (no accidental id collision)", async () => {
    const first = await fileIncidentReport(baseInput, false);
    const second = await fileIncidentReport(baseInput, false);
    expect(first.id).not.toBe(second.id);
  });

  // spec.md Edge Cases: an incident occurred while offline and filed after the fact — occurredAt
  // may be backdated relative to createdAt, and both remain independently retrievable once synced.
  it("offline: a backdated occurredAt is preserved distinctly from the optimistic createdAt", async () => {
    const backdated = "2026-01-01T04:00:00.000Z";
    const result = await fileIncidentReport({ ...baseInput, occurredAt: backdated }, false);

    expect(result.occurredAt).toBe(backdated);
    expect(new Date(result.createdAt).getTime()).toBeGreaterThan(new Date(backdated).getTime());
  });

  it("online: a non-network failure (e.g. 5xx) throws rather than silently queuing (FR-014)", async () => {
    (apiClient.POST as jest.Mock).mockResolvedValue({
      response: { ok: false }, error: { errorKey: "errors.unexpected" },
    });

    await expect(fileIncidentReport(baseInput, true)).rejects.toThrow("errors.unexpected");
    expect(offlineQueueMock.__mockEnqueue).not.toHaveBeenCalled();
  });
});
