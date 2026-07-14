// ── Auth ──────────────────────────────────────────────────────────────────────
export interface AuthState {
  userId:           string;
  email:            string;
  role:             string;
  organisationSlug: string; // needed to resend on silent refresh
  accessToken:      string; // in-memory only; refreshed automatically
}

// ── API responses ─────────────────────────────────────────────────────────────
// The backend never declares `.Produces<T>()` on its Minimal API routes (confirmed across the
// whole codebase, not specific to this feature — see mobile/services/apiClient.ts's own
// doc-comment), so response bodies are never trustworthy from the generated `paths` types.
// These hand-typed interfaces mirror the backend's actual Contracts/Responses/*.cs records
// (specs/013-parent-communication/contracts/api.md) — request/query typing is what the
// generated api-types.ts actually gets used for.
export interface AuthResponse {
  accessToken:  string;
  refreshToken: string;
  user: {
    id:            string;
    email:         string;
    emailVerified: boolean;
    role:          string;
    name:          string;
  };
}

export interface ParentChildResponse {
  id:               string;
  firstName:        string;
  lastName:         string;
  photoDownloadUrl: string | null;
  dateOfBirth:      string;
}

// ── Group activities (feature 009b) ──────────────────────────────────────────────
export type GroupActivityType = "outdoor" | "creative" | "music" | "story" | "celebration" | "other";

export interface GroupActivityPhotoResponse {
  id:                   string;
  downloadUrl:          string | null;
  thumbnailDownloadUrl: string | null;
  caption:              string | null;
  uploadedAt:           string;
}

export interface GroupActivitySummaryItem {
  id:           string;
  activityType: GroupActivityType;
  title:        string;
  description:  string | null;
  occurredAt:   string;
  photos:       GroupActivityPhotoResponse[];
}

export interface GalleryItemResponse {
  activityId: string;
  groupId:    string;
  photo:      GroupActivityPhotoResponse;
  occurredAt: string;
}

export interface GalleryResponse {
  items:       GalleryItemResponse[];
  hasConsent:  boolean;
}

export interface DailySummaryResponse {
  napsCount:                 number;
  bottlesCount:              number;
  diaperChangesCount:        number;
  latestMood:                string | null;
  latestTemperatureCelsius:  number | null;
  medicationAdministered:    boolean;
  activities:                string[];
  // Feature 009b: distinct from `activities` above (feature 013's per-child descriptions) —
  // group-level moments, consent-filtered photos (research.md R5/R6).
  groupActivities:           GroupActivitySummaryItem[];
}

export interface MessageResponse {
  id:       string;
  threadId: string;
  senderId: string;
  senderName: string;
  body:     string;
  sentAt:   string;
  readAt:   string | null;
}

export interface MessageThreadResponse {
  id:              string;
  subject:         string;
  childId:         string | null;
  childName:       string | null;
  createdAt:       string;
  lastActivityAt:  string;
  hasUnread:       boolean;
  messages:        MessageResponse[];
}

export interface MessageThreadSummaryResponse {
  id:             string;
  subject:        string;
  childId:        string | null;
  childName:      string | null;
  lastActivityAt: string;
  hasUnread:      boolean;
}

export interface ParentAnnouncementResponse {
  id:      string;
  subject: string;
  body:    string;
  sentAt:  string;
  readAt:  string | null;
}

export type NotificationType = "newmessage" | "announcement" | "temperaturealert" | "dayreservationdecided" | "mealpreferencerequestdecided";

export interface NotificationResponse {
  id:            string;
  type:          NotificationType;
  sourceId:      string;
  titleKey:      string;
  bodyKey:       string;
  argumentsJson: string;
  createdAt:     string;
  readAt:        string | null;
}

export interface ParentInvitationErrorResponse {
  errorKey: string;
}

// ── Day reservations (feature 013a) ──────────────────────────────────────────────
export type DayReservationType = "absence" | "extra" | "exchange";
export type DayReservationStatus = "pending" | "approved" | "rejected" | "cancelled";

export interface DayReservationResponse {
  id:                string;
  childId:           string;
  childDisplayName:  string;
  type:              DayReservationType;
  requestedDate:     string;
  exchangeForDate:   string | null;
  reason:            string | null;
  absenceJustified:  boolean | null;
  status:            DayReservationStatus;
  requestedBy:       string;
  decidedBy:         string | null;
  decidedAt:         string | null;
  directorNotes:     string | null;
  capacityWarning:   boolean | null;
  createdAt:         string;
  updatedAt:         string | null;
}

// ── Reservation settings (feature 013f) ──────────────────────────────────────────
export type ReservationRequestMode = "disabled" | "informational" | "approval";

export interface ReservationAvailabilityResponse {
  absence:     ReservationRequestMode;
  extra:       ReservationRequestMode;
  exchange:    ReservationRequestMode;
  noticeHours: number;
}

// ── Monthly menu (feature 013e) ──────────────────────────────────────────────────
export interface MonthlyMenuDayEntry {
  date:       string;
  soup:       string | null;
  mainCourse: string | null;
  dessert:    string | null;
  notes:      string | null;
}

export interface ParentMonthlyMenuEntry {
  locationId:   string;
  locationName: string;
  isPublished:  boolean;
  days:         MonthlyMenuDayEntry[];
  closureDates: string[];
}

export type MealTexture = "pureed" | "mixed" | "pieces" | "normal";

export interface ParentMealPreferenceResponse {
  texture:          MealTexture | null;
  dietaryType:      string[];
  hasPendingRequest: boolean;
}

export interface MealPreferenceChangeRequestResponse {
  id:       string;
  childId:  string;
  status:   "pending" | "approved" | "rejected";
  newTexture: MealTexture | null;
  newDietaryType: string[] | null;
  notes:    string | null;
  createdAt: string;
}
