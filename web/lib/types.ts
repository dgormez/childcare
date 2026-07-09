/**
 * Response body types the generated OpenAPI client can't provide (backend's Minimal API routes
 * don't declare `.Produces<T>()`, so only request bodies are typed — mirrors
 * mobile/types/index.ts's identical situation and comment in mobile/services/apiClient.ts).
 */

export interface AuthenticatedUser {
  id: string;
  email: string;
  emailVerified: boolean;
  role: string;
  name: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  user: AuthenticatedUser;
}

export interface OrganisationResponse {
  name: string;
}

export interface StaffResponse {
  id: string;
  tenantUserId: string;
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  role: string;
  qualificationLevel: string | null;
  photoDownloadUrl: string | null;
  eligibleLocationIds: string[];
  deactivatedAt: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface LocationResponse {
  id: string;
  name: string;
  address: string;
  phone: string;
  email: string;
  maxCapacity: number;
  naamLocatie: string | null;
  dossiernummer: string | null;
  verantwoordelijke: string | null;
  flexPermission: boolean;
  boPermission: boolean;
  deactivatedAt: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface DeviceSummaryResponse {
  id: string;
  locationId: string;
  locationName: string;
  groupId: string;
  groupName: string;
  pairedByTenantUserId: string;
  pairedByName: string;
  pairedAt: string;
  revokedAt: string | null;
}

export interface ApiErrorBody {
  errorKey: string;
  fieldErrors?: Record<string, string>;
}

// ── Attendance (feature 010) ─────────────────────────────────────────────────────
export type AttendanceStatus = "present" | "absent" | "closure";

export interface AttendanceRecordResponse {
  id: string;
  childId: string;
  locationId: string;
  date: string;
  status: AttendanceStatus;
  checkInAt: string | null;
  checkOutAt: string | null;
  plannedDurationMinutes: number | null;
  absenceJustified: boolean | null;
  absenceReason: string | null;
  recordedBy: string[];
  createdAt: string;
  updatedAt: string;
}

export interface PagedAttendanceResponse {
  items: AttendanceRecordResponse[];
  nextCursor: string | null;
}

// ── Closure calendar (feature 011) ─────────────────────────────────────────────
export type ClosureType = "holiday" | "training" | "extraordinary";
export type ClosureStatus = "draft" | "published" | "cancelled";

export interface ClosureDeliverySummaryResponse {
  sent: number;
  failed: number;
  messageCount: number;
}

export interface ClosureDayResponse {
  id: string;
  locationId: string;
  date: string;
  label: string;
  closureType: ClosureType;
  notifyParents: boolean;
  status: ClosureStatus;
  notificationSentAt: string | null;
  publishedAt: string | null;
  cancelledAt: string | null;
  deliverySummary: ClosureDeliverySummaryResponse;
  createdAt: string;
  updatedAt: string;
}

export interface ClosureNotificationSummaryResponse {
  recipients: number;
  pushSent: number;
  pushFailed: number;
  messagesCreated: number;
}

export interface PublishClosureDayResponse {
  closure: ClosureDayResponse;
  attendanceRecordsCreated: number;
  attendanceRecordsUpdated: number;
  requiresAttendanceConfirmation: boolean;
  notificationSummary: ClosureNotificationSummaryResponse;
}

export interface CancelClosureDayResponse {
  closure: ClosureDayResponse;
  attendanceRecordsReleased: number;
  attendanceRecordsPreserved: number;
  notificationSummary: ClosureNotificationSummaryResponse;
}
