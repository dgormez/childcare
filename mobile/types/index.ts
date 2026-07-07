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
