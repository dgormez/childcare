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

export interface DailySummaryResponse {
  napsCount:                number;
  bottlesCount:              number;
  diaperChangesCount:        number;
  latestMood:                string | null;
  latestTemperatureCelsius:  number | null;
  medicationAdministered:    boolean;
}
