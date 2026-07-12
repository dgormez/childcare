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

export type ReservationRequestMode = "disabled" | "informational" | "approval";

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
  reservationAbsencesMode: ReservationRequestMode;
  reservationExtrasMode: ReservationRequestMode;
  reservationSwapsMode: ReservationRequestMode;
  reservationNoticeHours: number;
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
  // Feature 013f FR-014 — present only on errors.location.reservation_settings.pending_requests_warning.
  pendingCounts?: Partial<Record<"absence" | "extra" | "exchange", number>>;
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

export interface GroupResponse {
  id: string;
  name: string;
  locationId: string;
}

// ── Staff scheduling (feature 012) ──────────────────────────────────────────────
export type AbsenceReason = "sick" | "leave" | "holiday";

export interface StaffScheduleResponse {
  id: string;
  staffProfileId: string;
  locationId: string;
  groupId: string | null;
  date: string;
  startTime: string;
  endTime: string;
  isAbsent: boolean;
  absenceReason: AbsenceReason | null;
  createdAt: string;
  updatedAt: string;
}

export interface CopyWeekSkippedEntryResponse {
  date: string;
  staffProfileId: string;
  reason: "closure_day" | "existing_entry";
}

export interface CopyWeekResponse {
  copiedCount: number;
  skipped: CopyWeekSkippedEntryResponse[];
}

export interface ProjectedOnDutyResponse {
  projectedOnDutyCount: number;
  staffProfileIds: string[];
}

export type WaitingListStatus = "waiting" | "offered" | "enrolled" | "withdrawn";

export interface WaitingListEntryResponse {
  id: string;
  childFirstName: string;
  childLastName: string;
  dateOfBirth: string;
  contactName: string;
  contactEmail: string | null;
  contactPhone: string | null;
  locationId: string;
  requestedStartDate: string | null;
  priority: number;
  status: WaitingListStatus;
  notes: string | null;
  childId: string | null;
  isDuplicate: boolean;
  registeredAt: string;
  updatedAt: string | null;
}

export interface OccupancyDayResponse {
  date: string;
  freeCapacity: number | null;
  closed: boolean;
}

export type DayReservationType = "absence" | "extra" | "exchange";
export type DayReservationStatus = "pending" | "approved" | "rejected" | "cancelled";

export interface DayReservationResponse {
  id: string;
  childId: string;
  childDisplayName: string;
  type: DayReservationType;
  requestedDate: string;
  exchangeForDate: string | null;
  reason: string | null;
  absenceJustified: boolean | null;
  status: DayReservationStatus;
  requestedBy: string;
  decidedBy: string | null;
  decidedAt: string | null;
  directorNotes: string | null;
  capacityWarning: boolean | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface ChildResponse {
  id: string;
  firstName: string;
  lastName: string;
  dateOfBirth: string;
  deactivatedAt: string | null;
}

// Feature 013 — parent communication.

export interface ContactResponse {
  id: string;
  firstName: string;
  lastName: string;
  phone: string;
  email: string | null;
  locale: string;
}

export interface MessageResponse {
  id: string;
  threadId: string;
  senderId: string;
  senderName: string;
  body: string;
  sentAt: string;
  readAt: string | null;
}

export interface MessageThreadResponse {
  id: string;
  subject: string;
  childId: string | null;
  childName: string | null;
  createdAt: string;
  lastActivityAt: string;
  hasUnread: boolean;
  messages: MessageResponse[];
}

export interface MessageThreadSummaryResponse {
  id: string;
  subject: string;
  childId: string | null;
  childName: string | null;
  lastActivityAt: string;
  hasUnread: boolean;
  unreadFromParentCount: number;
}

export interface AnnouncementResponse {
  id: string;
  locationId: string;
  groupId: string | null;
  subject: string;
  body: string;
  sentByTenantUserId: string;
  sentAt: string;
  recipientCount: number;
}

export interface ParentInvitationResponse {
  invitationId: string;
  contactId: string;
  email: string;
  expiresAt: string;
}

// ── Group activities (feature 009b) ──────────────────────────────────────────────
export type GroupActivityType = "outdoor" | "creative" | "music" | "story" | "celebration" | "other";

export interface GroupActivityPhotoResponse {
  id: string;
  downloadUrl: string | null;
  thumbnailDownloadUrl: string | null;
  caption: string | null;
  uploadedAt: string;
}

export interface GroupActivityResponse {
  id: string;
  groupId: string;
  activityType: GroupActivityType;
  title: string;
  description: string | null;
  occurredAt: string;
  recordedBy: string[];
  photos: GroupActivityPhotoResponse[];
  createdAt: string;
}

// Minimal shape this feature's merged timeline needs to render — feature 009 owns the full
// ChildEventResponse contract; no other web screen has needed it before this one.
export interface ChildEventResponse {
  id: string;
  childId: string;
  eventType: string;
  occurredAt: string;
  payload: Record<string, unknown>;
}

export interface GroupTimelineEntryResponse {
  kind: "child_event" | "group_activity";
  occurredAt: string;
  childEvent: ChildEventResponse | null;
  groupActivity: GroupActivityResponse | null;
}

export interface GroupTimelineResponse {
  entries: GroupTimelineEntryResponse[];
}
