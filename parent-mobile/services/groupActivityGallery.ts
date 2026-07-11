/**
 * groupActivityGallery.ts — parent-facing monthly photo gallery (feature 009b, User Story 3,
 * contracts/group-activities-api.md). Defaults to the current calendar month server-side when
 * no year/month is passed — this client never browses history (spec.md Assumptions).
 */
import { apiClient } from "./apiClient";
import type { GalleryResponse } from "../types";

export async function getGroupActivityGallery(): Promise<GalleryResponse> {
  const result = await apiClient.GET("/api/parent/group-activities/gallery");
  if (!result.response.ok) throw new Error("errors.gallery.load_failed");
  return result.data as unknown as GalleryResponse;
}
