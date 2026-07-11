// ── Auth ──────────────────────────────────────────────────────────────────────
export interface AuthState {
  userId:               string;
  email:                string;
  role:                 string;
  organisationSlug:     string; // needed to resend on silent refresh (research.md R3)
  accessToken:          string; // in-memory only; refreshed automatically
  staffProfileId?:      string; // populated from GET /api/staff/me after login (display only)
  eligibleLocationIds?: string[];
}

// ── Kiosk mode device (feature 008a) ────────────────────────────────────────────
export interface DeviceState {
  deviceId:     string;
  locationId:   string;
  groupId:      string;
  locationName: string;
  groupName:    string;
}

// ── API responses ─────────────────────────────────────────────────────────────
export interface AuthResponse {
  accessToken:  string;
  refreshToken: string;
  user: {
    id:            string;
    email:         string;
    emailVerified: boolean;
    role:          string;
  };
}

export interface StaffMeResponse {
  staffProfileId:      string;
  firstName:           string;
  lastName:            string;
  role:                string;
  eligibleLocationIds: string[];
}

export interface GroupResponse {
  id:         string;
  name:       string;
  locationId: string;
}

export interface LocationResponse {
  id:   string;
  name: string;
}

export interface ChildResponse {
  id:                   string;
  firstName:            string;
  lastName:             string;
  dateOfBirth:          string;
  photoDownloadUrl:     string | null;
  allergiesDescription: string | null;
  allergySeverity:      string | null;
  medicalConditions:    string | null;
  dietaryRestrictions:  string | null;
  deactivatedAt:        string | null;
}

// ── Kiosk mode room shift register (feature 008a) ───────────────────────────────
export interface RoomRosterCard {
  staffProfileId: string;
  firstName:      string;
  photoUrl:       string | null;
  checkedIn:      boolean;
  checkedInAt:    string | null;
}

export interface PairDeviceResponse {
  deviceId:     string;
  deviceToken:  string;
  tokenVersion: number;
}

// ── Child events (feature 009, extended by 009a: growth_check rename + custom type) ────────────
export type ChildEventType =
  | "sleep" | "temperature" | "medication" | "feeding_bottle" | "feeding_solid"
  | "diaper" | "mood" | "activity" | "note" | "weight" | "growth_check" | "custom";

export interface ChildEventResponse {
  id:              string;
  childId:         string;
  eventType:       ChildEventType;
  occurredAt:      string;
  endedAt:         string | null;
  payload:         Record<string, unknown>;
  visibleToParent: boolean;
  recordedBy:      string[];
  administeredBy:  string | null;
  createdAt:       string;
  updatedAt:       string;
}

export interface PagedChildEventsResponse {
  items:      ChildEventResponse[];
  nextCursor: string | null;
}

// ── Multi-child events (feature 009c) ───────────────────────────────────────────────────────
// The eight event types that make sense as a shared group event (spec.md FR-001/FR-002) —
// temperature/medication/weight/growth_check need per-child values and stay single-child.
export type BatchEligibleChildEventType =
  | "sleep" | "diaper" | "feeding_bottle" | "feeding_solid" | "mood" | "activity" | "note" | "custom";

export type ChildEventBatchFailureReason = "child_not_found" | "not_present";

export interface ChildEventBatchCreatedItem {
  childId: string;
  eventId: string;
}

export interface ChildEventBatchErrorItem {
  childId: string;
  reason:  ChildEventBatchFailureReason;
}

export interface ChildEventBatchResponse {
  created: ChildEventBatchCreatedItem[];
  errors:  ChildEventBatchErrorItem[];
}

export interface DailySummaryResponse {
  napsCount:                number;
  bottlesCount:              number;
  diaperChangesCount:        number;
  latestMood:                string | null;
  latestTemperatureCelsius:  number | null;
  medicationAdministered:    boolean;
}

// ── Attendance (feature 010) ─────────────────────────────────────────────────────
export type AttendanceStatus = "present" | "absent" | "closure";

export interface AttendanceRecordResponse {
  id:                     string;
  childId:                string;
  locationId:             string;
  date:                   string;
  status:                 AttendanceStatus;
  checkInAt:              string | null;
  checkOutAt:             string | null;
  plannedDurationMinutes: number | null;
  absenceJustified:       boolean | null;
  absenceReason:          string | null;
  recordedBy:             string[];
  createdAt:              string;
  updatedAt:              string;
}

export interface PagedAttendanceResponse {
  items:      AttendanceRecordResponse[];
  nextCursor: string | null;
}

// "green" | "amber" | "red" — FR-007e's precise threshold comparison, never a UI-computed value.
export interface BkrRatioResponse {
  presentCount:        number;
  qualifiedStaffCount: number;
  isNapTime:           boolean;
  threshold:           number;
  status:              "green" | "amber" | "red";
}

// ── Group activities (feature 009b) ──────────────────────────────────────────────
export type GroupActivityType = "outdoor" | "creative" | "music" | "story" | "celebration" | "other";

export interface GroupActivityPhotoResponse {
  id:                    string;
  downloadUrl:           string | null;
  thumbnailDownloadUrl:  string | null;
  caption:               string | null;
  uploadedAt:            string;
}

export interface GroupActivityResponse {
  id:          string;
  groupId:     string;
  activityType: GroupActivityType;
  title:       string;
  description: string | null;
  occurredAt:  string;
  recordedBy:  string[];
  photos:      GroupActivityPhotoResponse[];
  createdAt:   string;
}

export interface GroupTimelineEntryResponse {
  kind:          "child_event" | "group_activity";
  occurredAt:    string;
  childEvent:    ChildEventResponse | null;
  groupActivity: GroupActivityResponse | null;
}

export interface GroupTimelineResponse {
  entries: GroupTimelineEntryResponse[];
}
