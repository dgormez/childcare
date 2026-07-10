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

export interface DailySummaryResponse {
  napsCount:                 number;
  bottlesCount:              number;
  diaperChangesCount:        number;
  latestMood:                string | null;
  latestTemperatureCelsius:  number | null;
  medicationAdministered:    boolean;
  activities:                string[];
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

export type NotificationType = "newmessage" | "announcement" | "temperaturealert";

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
