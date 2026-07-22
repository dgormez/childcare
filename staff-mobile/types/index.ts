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
// whole codebase — see parent-mobile/services/apiClient.ts's identical doc-comment), so response
// bodies are never trustworthy from the generated `paths` types. These hand-typed interfaces
// mirror the backend's actual Contracts/Responses/*.cs records
// (specs/027-staff-app/contracts/staff-app-api.md) — request/query typing is what the generated
// services/generated/api-types.ts actually gets used for.
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

// ── Staff schedule (feature 012, extended by feature 027) ───────────────────────
export type AbsenceReason = "sick" | "leave" | "holiday";
export type StaffScheduleStatus = "scheduled" | "confirmed" | "absent" | "covered";

export interface StaffScheduleResponse {
  id:             string;
  staffProfileId: string;
  locationId:     string;
  groupId:        string | null;
  date:           string;
  startTime:      string;
  endTime:        string;
  status:         StaffScheduleStatus;
  absenceReason:  AbsenceReason | null;
  coverStaffId:   string | null;
  notes:          string | null;
  isPublished:    boolean;
  createdAt:      string;
  updatedAt:      string;
}

// ── Staff leave requests (feature 027) ───────────────────────────────────────────
export type StaffLeaveRequestType = "sick" | "annual" | "other";
export type StaffLeaveRequestStatus = "pending" | "approved" | "rejected";

export interface StaffLeaveRequestResponse {
  id:             string;
  staffProfileId: string;
  type:           StaffLeaveRequestType;
  dateFrom:       string;
  dateTo:         string;
  notes:          string | null;
  status:         StaffLeaveRequestStatus;
  decidedBy:      string | null;
  decidedAt:      string | null;
  createdAt:      string;
}

// ── Notifications (feature 014a, extended by feature 027 — research.md R6) ──────
// Deliberately a narrower union than parent-mobile/types/index.ts's own NotificationType — a
// staff member never receives a parent-facing type like InvoiceSent (research.md R6).
export type NotificationType = "schedulepublished" | "assignmentchanged" | "leaverequestdecided";

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
