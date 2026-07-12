import React from "react";
import { render, fireEvent, waitFor, act } from "@testing-library/react-native";
import { IncidentReportForm } from "../../components/IncidentReportForm";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

jest.mock("../../services/incidentReports", () => ({ fileIncidentReport: jest.fn() }));

const { fileIncidentReport } = jest.requireMock("../../services/incidentReports") as { fileIncidentReport: jest.Mock };

beforeEach(() => {
  jest.clearAllMocks();
});

it("submitting with description + injuryType calls the API and notifies onSaved", async () => {
  const report = { id: "r1", childId: "child-1", description: "Fell off the swing", injuryType: "fall" };
  fileIncidentReport.mockResolvedValue(report);
  const onSaved = jest.fn();

  const { getByPlaceholderText, getByText } = await render(
    <IncidentReportForm visible childId="child-1" isConnected onClose={jest.fn()} onSaved={onSaved} />
  );

  await act(async () =>
    fireEvent.changeText(getByPlaceholderText("incidentReports.descriptionPlaceholder"), "Fell off the swing")
  );
  await act(async () => fireEvent.press(getByText("incidentReports.injuryTypes.fall")));
  await act(async () => fireEvent.press(getByText("incidentReports.submit")));

  await waitFor(() => expect(fileIncidentReport).toHaveBeenCalledWith(
    expect.objectContaining({ childId: "child-1", description: "Fell off the swing", injuryType: "fall" }),
    true
  ));
  expect(onSaved).toHaveBeenCalledWith(report);
});

it("submitting with a missing required field shows a validation error and does not call the API", async () => {
  const { getByText } = await render(
    <IncidentReportForm visible childId="child-1" isConnected onClose={jest.fn()} onSaved={jest.fn()} />
  );

  await act(async () => fireEvent.press(getByText("incidentReports.submit")));

  expect(getByText("incidentReports.validation.descriptionRequired")).toBeTruthy();
  expect(fileIncidentReport).not.toHaveBeenCalled();
});

it("submitting with description but no injuryType selected shows the injuryType validation error", async () => {
  const { getByPlaceholderText, getByText } = await render(
    <IncidentReportForm visible childId="child-1" isConnected onClose={jest.fn()} onSaved={jest.fn()} />
  );

  await act(async () =>
    fireEvent.changeText(getByPlaceholderText("incidentReports.descriptionPlaceholder"), "Bumped head")
  );
  await act(async () => fireEvent.press(getByText("incidentReports.submit")));

  expect(getByText("incidentReports.validation.injuryTypeRequired")).toBeTruthy();
  expect(fileIncidentReport).not.toHaveBeenCalled();
});

// T059: the pending-sync badge itself is rendered by the parent screen (child/[id].tsx), driven
// by the offline queue — this proves the form's offline path never surfaces an error to the
// caregiver, the precondition for that badge appearing instead of an error message.
it("offline: submitting succeeds without surfacing an error, same as the online path", async () => {
  const optimistic = { id: "local-1", childId: "child-1", description: "Bumped head", injuryType: "bump" };
  fileIncidentReport.mockResolvedValue(optimistic);
  const onSaved = jest.fn();

  const { getByPlaceholderText, getByText, queryByText } = await render(
    <IncidentReportForm visible childId="child-1" isConnected={false} onClose={jest.fn()} onSaved={onSaved} />
  );

  await act(async () =>
    fireEvent.changeText(getByPlaceholderText("incidentReports.descriptionPlaceholder"), "Bumped head")
  );
  await act(async () => fireEvent.press(getByText("incidentReports.injuryTypes.bump")));
  await act(async () => fireEvent.press(getByText("incidentReports.submit")));

  await waitFor(() => expect(onSaved).toHaveBeenCalledWith(optimistic));
  expect(queryByText("incidentReports.submitError")).toBeNull();
});
