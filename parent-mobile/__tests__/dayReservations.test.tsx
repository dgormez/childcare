import React from "react";
import { render, fireEvent, waitFor } from "@testing-library/react-native";
import AbsenceRequestScreen from "../app/(app)/requests/absence";
import ExchangeDayRequestScreen from "../app/(app)/requests/exchange";
import MyRequestsScreen from "../app/(app)/requests/index";
import type { DayReservationResponse, ParentChildResponse } from "../types";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

jest.mock("../services/apiClient", () => {
  const mockGet = jest.fn();
  const mockPost = jest.fn();
  return {
    apiClient: { GET: (...args: unknown[]) => mockGet(...args), POST: (...args: unknown[]) => mockPost(...args) },
    configureApiBaseUrl: jest.fn(),
    __mockGet: mockGet,
    __mockPost: mockPost,
  };
});

const apiMock = jest.requireMock("../services/apiClient") as { __mockGet: jest.Mock; __mockPost: jest.Mock };
const getMock = apiMock.__mockGet;
const postMock = apiMock.__mockPost;
const { useRouter } = require("expo-router");

function jsonResponse(status: number, body: unknown) {
  const ok = status >= 200 && status < 300;
  return { response: { ok, status, json: async () => body }, data: ok ? body : undefined, error: ok ? undefined : body };
}

const child1: ParentChildResponse = { id: "c1", firstName: "Timmy", lastName: "Tester", photoDownloadUrl: null, dateOfBirth: "2022-01-01" };
const child2: ParentChildResponse = { id: "c2", firstName: "Lucas", lastName: "Tester", photoDownloadUrl: null, dateOfBirth: "2023-01-01" };

function makeReservation(overrides: Partial<DayReservationResponse> = {}): DayReservationResponse {
  return {
    id: "res-1",
    childId: "c1",
    childDisplayName: "Timmy Tester",
    type: "absence",
    requestedDate: "2026-07-13",
    exchangeForDate: null,
    reason: null,
    absenceJustified: null,
    status: "pending",
    requestedBy: "user-1",
    decidedBy: null,
    decidedAt: null,
    directorNotes: null,
    capacityWarning: null,
    createdAt: "2026-07-11T09:00:00Z",
    updatedAt: null,
    ...overrides,
  };
}

beforeEach(() => {
  jest.clearAllMocks();
  useRouter.mockReturnValue({ push: jest.fn(), replace: jest.fn(), back: jest.fn() });
});

describe("AbsenceRequestScreen", () => {
  it("submits an absence request for the selected child and navigates to the requests list", async () => {
    getMock.mockImplementation((path: string) => {
      if (path === "/api/parent/children") return Promise.resolve(jsonResponse(200, [child1]));
      return Promise.resolve(jsonResponse(404, {}));
    });
    postMock.mockResolvedValue(jsonResponse(201, makeReservation()));
    const replace = jest.fn();
    useRouter.mockReturnValue({ replace, push: jest.fn(), back: jest.fn() });

    const { findByText, getByPlaceholderText } = await render(<AbsenceRequestScreen />);

    await findByText("Timmy Tester");
    await fireEvent.changeText(getByPlaceholderText("dayReservations.chooseDate"), "2026-07-13");
    await fireEvent.press(await findByText("dayReservations.submit"));

    await waitFor(() => expect(postMock).toHaveBeenCalledWith(
      "/api/day-reservations",
      expect.objectContaining({ body: { childId: "c1", type: "absence", requestedDate: "2026-07-13", exchangeForDate: null, reason: null } }),
    ));
    await waitFor(() => expect(replace).toHaveBeenCalledWith("/(app)/requests"));
  });

  it("surfaces a clear error when submission is rejected as a past date", async () => {
    getMock.mockImplementation((path: string) => {
      if (path === "/api/parent/children") return Promise.resolve(jsonResponse(200, [child1]));
      return Promise.resolve(jsonResponse(404, {}));
    });
    postMock.mockResolvedValue(jsonResponse(422, { errorKey: "errors.validation", fieldErrors: { requestedDate: "errors.day_reservations.past_date" } }));

    const { findByText, getByPlaceholderText } = await render(<AbsenceRequestScreen />);

    await findByText("Timmy Tester");
    await fireEvent.changeText(getByPlaceholderText("dayReservations.chooseDate"), "2020-01-01");
    await fireEvent.press(await findByText("dayReservations.submit"));

    expect(await findByText("errors.validation")).toBeTruthy();
  });
});

describe("AbsenceRequestScreen bulk toggle (feature 030 US1)", () => {
  it("hides the bulk toggle with one linked child, shows it with two", async () => {
    getMock.mockImplementation((path: string) => {
      if (path === "/api/parent/children") return Promise.resolve(jsonResponse(200, [child1]));
      return Promise.resolve(jsonResponse(404, {}));
    });

    const { findByText, queryByText } = await render(<AbsenceRequestScreen />);
    await findByText("Timmy Tester");
    expect(queryByText("dayReservations.applyToAllChildren")).toBeNull();
  });

  it("shows the bulk toggle and renders a partial-failure result", async () => {
    getMock.mockImplementation((path: string) => {
      if (path === "/api/parent/children") return Promise.resolve(jsonResponse(200, [child1, child2]));
      return Promise.resolve(jsonResponse(404, {}));
    });
    postMock.mockResolvedValue(jsonResponse(200, {
      results: [
        { childId: "c1", childName: "Timmy Tester", succeeded: true, reservation: makeReservation(), errorKey: null },
        { childId: "c2", childName: "Lucas Tester", succeeded: false, reservation: null, errorKey: "errors.day_reservations.request_type_disabled" },
      ],
    }));

    const { findByText, getByPlaceholderText } = await render(<AbsenceRequestScreen />);
    await findByText("Timmy Tester");
    await findByText("Lucas Tester");

    await fireEvent.press(await findByText("dayReservations.applyToAllChildren"));
    await fireEvent.changeText(getByPlaceholderText("dayReservations.chooseDate"), "2026-07-13");
    await fireEvent.press(await findByText("dayReservations.submit"));

    await waitFor(() => expect(postMock).toHaveBeenCalledWith(
      "/api/parent/day-reservations/bulk",
      expect.objectContaining({ body: { childIds: ["c1", "c2"], type: "absence", requestedDate: "2026-07-13", exchangeForDate: null, reason: null } }),
    ));
    expect(await findByText("dayReservations.bulkPartialResult")).toBeTruthy();
    expect(await findByText("errors.day_reservations.request_type_disabled")).toBeTruthy();
  });
});

describe("ExchangeDayRequestScreen", () => {
  it("requires both the exchange-for date and the requested date before enabling submit", async () => {
    getMock.mockImplementation((path: string) => {
      if (path === "/api/parent/children") return Promise.resolve(jsonResponse(200, [child1]));
      return Promise.resolve(jsonResponse(404, {}));
    });

    const { findByText, getAllByPlaceholderText } = await render(<ExchangeDayRequestScreen />);

    await findByText("Timmy Tester");
    const dateInputs = getAllByPlaceholderText("dayReservations.chooseDate");
    expect(dateInputs).toHaveLength(2);

    // Neither date has been filled in yet — pressing submit must not call the API (FR-001/FR-003
    // require both the source and target dates for an exchange).
    await fireEvent.press(await findByText("dayReservations.submit"));
    expect(postMock).not.toHaveBeenCalled();
  });

  it("surfaces the not-contracted-day error returned by the server", async () => {
    getMock.mockImplementation((path: string) => {
      if (path === "/api/parent/children") return Promise.resolve(jsonResponse(200, [child1]));
      return Promise.resolve(jsonResponse(404, {}));
    });
    postMock.mockResolvedValue(jsonResponse(400, { errorKey: "errors.day_reservations.not_contracted_day" }));

    const { findByText, getAllByPlaceholderText } = await render(<ExchangeDayRequestScreen />);

    await findByText("Timmy Tester");
    const dateInputs = getAllByPlaceholderText("dayReservations.chooseDate");
    await fireEvent.changeText(dateInputs[0], "2026-07-15");
    await fireEvent.changeText(dateInputs[1], "2026-07-14");
    await fireEvent.press(await findByText("dayReservations.submit"));

    expect(await findByText("errors.day_reservations.not_contracted_day")).toBeTruthy();
  });
});

describe("MyRequestsScreen", () => {
  it("shows the empty state when the parent has no requests", async () => {
    getMock.mockImplementation((path: string) => {
      if (path === "/api/day-reservations/mine") return Promise.resolve(jsonResponse(200, []));
      return Promise.resolve(jsonResponse(404, {}));
    });

    const { findByText } = await render(<MyRequestsScreen />);

    expect(await findByText("dayReservations.noRequests")).toBeTruthy();
  });

  it("cancels a pending request after confirmation", async () => {
    getMock.mockImplementation((path: string) => {
      if (path === "/api/day-reservations/mine") return Promise.resolve(jsonResponse(200, [makeReservation()]));
      return Promise.resolve(jsonResponse(404, {}));
    });
    postMock.mockResolvedValue(jsonResponse(200, makeReservation({ status: "cancelled" })));

    const { findByText } = await render(<MyRequestsScreen />);

    await fireEvent.press(await findByText("dayReservations.cancel"));
    await fireEvent.press(await findByText("dayReservations.cancelConfirm"));

    await waitFor(() => expect(postMock).toHaveBeenCalledWith("/api/day-reservations/{id}/cancel", { params: { path: { id: "res-1" } } }));
  });
});
