/**
 * attendance.ts — QR check-in code issuance (feature 021, contracts/021-qr-checkin/
 * qr-checkin-api.md). No offline path: a code is a live, server-signed artifact that cannot be
 * meaningfully pre-generated (research.md R6) — callers gate this behind useIsOffline, mirroring
 * groupActivityGallery.ts's download-action pattern (031).
 */
import { apiClient } from "./apiClient";
import type { IssueCheckInCodeResponse } from "../types";

export async function requestQrCode(childId: string): Promise<IssueCheckInCodeResponse> {
  const result = await apiClient.POST("/api/parent/attendance/qr-code", {
    body: { childId },
  });
  if (!result.response.ok) throw new Error("errors.qrCheckIn.issue_failed");
  return result.data as unknown as IssueCheckInCodeResponse;
}
