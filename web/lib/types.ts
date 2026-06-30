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
  date:      string;
  createdAt: string;
}

export interface User {
  id:    string;
  email: string;
}

export interface AuthResponse {
  accessToken:  string;
  refreshToken: string;
  user:         User;
}

export interface SubscriptionStatus {
  status:           string;
  isActive:         boolean;
  currentPeriodEnd: string | null;
}
