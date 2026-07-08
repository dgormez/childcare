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
