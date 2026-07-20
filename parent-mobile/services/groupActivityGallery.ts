/**
 * groupActivityGallery.ts — parent-facing monthly photo gallery (feature 009b, User Story 3,
 * contracts/group-activities-api.md). Defaults to the current calendar month server-side when
 * no year/month is passed — this client never browses history (spec.md Assumptions).
 */
import { Directory, File, Paths } from "expo-file-system";
import { apiClient } from "./apiClient";
import type { GalleryResponse } from "../types";

export async function getGroupActivityGallery(): Promise<GalleryResponse> {
  const result = await apiClient.GET("/api/parent/group-activities/gallery");
  if (!result.response.ok) throw new Error("errors.gallery.load_failed");
  return result.data as unknown as GalleryResponse;
}

// 031-photo-lifecycle-governance FR-012/FR-013 — same two-step signed-URL download as
// downloadFiscalAttestationPdf: get the signed URL from the API, then download from that URL
// directly (no Authorization header — the signature itself authorizes the request). Always the
// full-resolution original, never the thumbnail shown in the grid.
// No explicit return-type annotation — see invoices.ts's downloadInvoicePdf for why.
export async function downloadGroupActivityPhotoOriginal(photoId: string) {
  const urlResult = await apiClient.GET("/api/parent/photos/{photoType}/{objectRef}/download", {
    params: { path: { photoType: "group-activity", objectRef: photoId } },
  });
  if (!urlResult.response.ok) throw new Error("errors.gallery.download_failed");
  const { downloadUrl } = urlResult.data as unknown as { downloadUrl: string };

  const destination = new Directory(Paths.cache, "group-activity-photos");
  destination.create({ intermediates: true, idempotent: true });
  return File.downloadFileAsync(downloadUrl, new File(destination, `${photoId}.jpg`), {
    idempotent: true,
  });
}
