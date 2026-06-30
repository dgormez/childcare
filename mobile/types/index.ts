// ── Habit ─────────────────────────────────────────────────────────────────────
export interface Habit {
  id:        string;
  userId:    string;
  name:      string;
  color:     string;
  icon:      string;
  createdAt: string;
}

export interface HabitCompletion {
  id:        string;
  habitId:   string;
  userId:    string;
  date:      string; // "YYYY-MM-DD"
  createdAt: string;
}

// ── Auth ──────────────────────────────────────────────────────────────────────
export interface AuthState {
  userId:      string;
  email:       string;
  accessToken: string; // in-memory only; refreshed automatically
}

// ── API responses ─────────────────────────────────────────────────────────────
export interface AuthResponse {
  accessToken:  string;
  refreshToken: string;
  user: {
    id:            string;
    email:         string;
    emailVerified: boolean;
  };
}
