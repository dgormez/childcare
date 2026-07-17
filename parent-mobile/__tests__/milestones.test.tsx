import React from "react";
import { render, fireEvent, waitFor } from "@testing-library/react-native";
import MilestonesScreen from "../app/(app)/milestones/index";
import type { DevelopmentalDomainResponse, ParentChildResponse } from "../types";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key, i18n: { language: "en" } }),
}));

jest.mock("../services/apiClient", () => ({
  apiClient: { GET: jest.fn() },
}));

jest.mock("../services/milestones", () => ({
  getMilestonePortfolio: jest.fn(),
  downloadMilestonePortfolioPdf: jest.fn(),
}));

jest.mock("expo-sharing", () => ({
  isAvailableAsync: jest.fn().mockResolvedValue(true),
  shareAsync: jest.fn().mockResolvedValue(undefined),
}));

const { apiClient } = jest.requireMock("../services/apiClient") as { apiClient: { GET: jest.Mock } };
const { getMilestonePortfolio, downloadMilestonePortfolioPdf } = jest.requireMock("../services/milestones") as {
  getMilestonePortfolio: jest.Mock;
  downloadMilestonePortfolioPdf: jest.Mock;
};
const Sharing = jest.requireMock("expo-sharing") as { isAvailableAsync: jest.Mock; shareAsync: jest.Mock };

function makeChild(overrides: Partial<ParentChildResponse> = {}): ParentChildResponse {
  return { id: "child-1", firstName: "Emma", lastName: "Peeters", photoDownloadUrl: null, dateOfBirth: "2023-01-01", ...overrides };
}

const domainWithMilestone: DevelopmentalDomainResponse = {
  id: "domain-1",
  code: "language",
  nameNl: "Taal",
  nameFr: "Langage",
  nameEn: "Language",
  sortOrder: 1,
  milestones: [
    {
      id: "milestone-1",
      ageFromMonths: 12,
      ageToMonths: 18,
      descriptionNl: "Nl",
      descriptionFr: "Fr",
      descriptionEn: "Says first words",
      sortOrder: 1,
      currentStatus: "achieved",
      isCurrentFocus: true,
    },
  ],
};

function okResponse(data: unknown) {
  return { response: { ok: true }, data };
}

beforeEach(() => {
  jest.clearAllMocks();
});

it("shows only the caller's own children's milestones, grouped by domain", async () => {
  apiClient.GET.mockResolvedValue(okResponse([makeChild()]));
  getMilestonePortfolio.mockResolvedValue({ status: "loaded", domains: [domainWithMilestone] });

  const { findByText } = await render(<MilestonesScreen />);

  expect(await findByText("Emma")).toBeTruthy();
  expect(await findByText("Language")).toBeTruthy();
  expect(await findByText("Says first words")).toBeTruthy();
});

it("shows a warm empty state when a child has no observations yet", async () => {
  apiClient.GET.mockResolvedValue(okResponse([makeChild()]));
  getMilestonePortfolio.mockResolvedValue({ status: "loaded", domains: [{ ...domainWithMilestone, milestones: [] }] });

  const { findByText } = await render(<MilestonesScreen />);
  expect(await findByText("milestones.emptyState")).toBeTruthy();
});

it("downloading a child's portfolio PDF opens the share sheet", async () => {
  apiClient.GET.mockResolvedValue(okResponse([makeChild()]));
  getMilestonePortfolio.mockResolvedValue({ status: "loaded", domains: [domainWithMilestone] });
  downloadMilestonePortfolioPdf.mockResolvedValue({ uri: "file:///cache/milestone-portfolios/child-1.pdf" });

  const { findByText } = await render(<MilestonesScreen />);
  fireEvent.press(await findByText("milestones.downloadPdf"));

  await waitFor(() => expect(downloadMilestonePortfolioPdf).toHaveBeenCalledWith("child-1"));
  await waitFor(() =>
    expect(Sharing.shareAsync).toHaveBeenCalledWith("file:///cache/milestone-portfolios/child-1.pdf", expect.any(Object))
  );
});

it("shows a download error message when the download fails", async () => {
  apiClient.GET.mockResolvedValue(okResponse([makeChild()]));
  getMilestonePortfolio.mockResolvedValue({ status: "loaded", domains: [domainWithMilestone] });
  downloadMilestonePortfolioPdf.mockRejectedValue(new Error("network"));

  const { findByText } = await render(<MilestonesScreen />);
  fireEvent.press(await findByText("milestones.downloadPdf"));

  expect(await findByText("milestones.downloadFailed")).toBeTruthy();
});
