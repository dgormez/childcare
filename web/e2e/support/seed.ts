/**
 * Seeds real data into the backend via its HTTP API — no direct DB access, so this only relies
 * on contracts the app itself already exposes. Each call creates a brand-new organisation
 * (unique slug/email per call) so tests never collide or need a reset step between runs.
 */
const API_BASE_URL = process.env.E2E_API_BASE_URL ?? "http://localhost:5001";
const SUPERADMIN_KEY = process.env.E2E_SUPERADMIN_API_KEY;

export interface SeededDirector {
  organisationSlug: string;
  organisationName: string;
  email: string;
  password: string;
  /** Short-lived (15 min) — good enough for one test's API seeding, not for UI auth. */
  accessToken: string;
}

export interface SeededCaregiver {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
}

export interface SeededLocation {
  id: string;
  name: string;
}

export interface SeededGroup {
  id: string;
  name: string;
}

export interface SeededChild {
  id: string;
  firstName: string;
  lastName: string;
}

export interface SeededParent {
  organisationSlug: string;
  email: string;
  password: string;
  accessToken: string;
}

async function asJson<T>(res: Response): Promise<T> {
  if (!res.ok) {
    throw new Error(`Seed request failed: ${res.status} ${res.url}\n${await res.text()}`);
  }
  return (await res.json()) as T;
}

function authedJson(accessToken: string, body?: unknown): RequestInit {
  return {
    method: body === undefined ? "GET" : "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${accessToken}`,
    },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  };
}

/** A fresh org + director account, ready to log in through the UI. */
export async function seedDirector(): Promise<SeededDirector> {
  if (!SUPERADMIN_KEY) {
    throw new Error(
      "E2E_SUPERADMIN_API_KEY is not set. Copy web/.env.e2e.example to web/.env.e2e and fill " +
        "in the value from backend/ChildCare.Api/appsettings.Development.json (SuperAdmin:ApiKey).",
    );
  }

  const stamp = `${Date.now()}-${Math.floor(Math.random() * 1e6)}`;
  const email = `e2e-director-${stamp}@example.com`;
  const password = "E2ePassw0rd!1";

  const invitation = await asJson<{ token: string }>(
    await fetch(`${API_BASE_URL}/api/admin/invitations`, {
      method: "POST",
      headers: { "Content-Type": "application/json", "X-Superadmin-Key": SUPERADMIN_KEY },
      body: JSON.stringify({ email }),
    }),
  );

  const registration = await asJson<{
    accessToken: string;
    organisation: { slug: string; name: string };
  }>(
    await fetch(`${API_BASE_URL}/api/organisations/register`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        invitationToken: invitation.token,
        organisationName: `E2E Org ${stamp}`,
        directorName: "E2E Director",
        email,
        password,
      }),
    }),
  );

  return {
    organisationSlug: registration.organisation.slug,
    organisationName: registration.organisation.name,
    email,
    password,
    accessToken: registration.accessToken,
  };
}

/** A caregiver (Staff role) profile under `director`'s org — created the same way a director
 * would via POST /api/staff, since the web UI has no "Add Staff" form to drive instead (see
 * KNOWN_GAPS.md). Invitation is left unaccepted; tests here only need the profile to exist. */
export async function seedCaregiver(director: SeededDirector): Promise<SeededCaregiver> {
  const stamp = `${Date.now()}-${Math.floor(Math.random() * 1e6)}`;
  const email = `e2e-caregiver-${stamp}@example.com`;

  const staff = await asJson<{ id: string; firstName: string; lastName: string }>(
    await fetch(
      `${API_BASE_URL}/api/staff`,
      authedJson(director.accessToken, {
        firstName: "Casey",
        lastName: `Caregiver${stamp}`,
        email,
        phone: "+32 470 00 00 00",
        qualificationLevel: "QualifiedCaregiver",
        role: "Staff",
        existingTenantUserId: null,
      }),
    ),
  );

  return { id: staff.id, firstName: staff.firstName, lastName: staff.lastName, email };
}

/** A location under `director`'s org — created via POST /api/locations since the web UI has no
 * "Add Location" form (see KNOWN_GAPS.md). */
export async function seedLocation(director: SeededDirector): Promise<SeededLocation> {
  const stamp = `${Date.now()}-${Math.floor(Math.random() * 1e6)}`;

  const location = await asJson<{ id: string; name: string }>(
    await fetch(
      `${API_BASE_URL}/api/locations`,
      authedJson(director.accessToken, {
        name: `E2E Location ${stamp}`,
        address: "1 Test Street",
        phone: "+32 470 00 00 01",
        email: `e2e-location-${stamp}@example.com`,
        maxCapacity: 20,
      }),
    ),
  );

  return { id: location.id, name: location.name };
}

/** Marks a caregiver eligible to work at a location (PUT /api/staff/{id}/locations/{locationId})
 * — required before they show up in the scheduling grid for that location. */
export async function assignStaffLocation(director: SeededDirector, staffId: string, locationId: string): Promise<void> {
  const res = await fetch(`${API_BASE_URL}/api/staff/${staffId}/locations/${locationId}`, {
    method: "PUT",
    headers: { Authorization: `Bearer ${director.accessToken}` },
  });
  if (!res.ok) {
    throw new Error(`Seed request failed: ${res.status} ${res.url}\n${await res.text()}`);
  }
}

/** A group under a location — via POST /api/groups since the web UI's Groups screen has no
 * standalone create-a-group-for-a-location flow reachable without existing data either. */
export async function seedGroup(director: SeededDirector, locationId: string): Promise<SeededGroup> {
  const stamp = `${Date.now()}-${Math.floor(Math.random() * 1e6)}`;
  const group = await asJson<{ id: string; name: string }>(
    await fetch(`${API_BASE_URL}/api/groups`, authedJson(director.accessToken, { name: `E2E Group ${stamp}`, locationId })),
  );
  return { id: group.id, name: group.name };
}

/** A child under `director`'s org — via POST /api/children, since the web Children screen is a
 * "coming soon" placeholder (see KNOWN_GAPS.md). Only the fields the API requires are set. */
export async function seedChild(director: SeededDirector): Promise<SeededChild> {
  const stamp = `${Date.now()}-${Math.floor(Math.random() * 1e6)}`;
  const child = await asJson<{ id: string; firstName: string; lastName: string }>(
    await fetch(
      `${API_BASE_URL}/api/children`,
      authedJson(director.accessToken, {
        firstName: "Robin",
        lastName: `Child${stamp}`,
        dateOfBirth: "2022-05-01",
        gender: null,
        nationality: null,
        allergiesDescription: null,
        allergySeverity: null,
        medicalConditions: null,
        dietaryRestrictions: null,
        gpName: null,
        gpPhone: null,
        healthInsuranceNumber: null,
        kindcode: null,
      }),
    ),
  );
  return { id: child.id, firstName: child.firstName, lastName: child.lastName };
}

/** Pairs a kiosk device to a location/group (POST /api/devices/pair) and returns the device's
 * own bearer token — attendance check-in/out is device-authenticated only, a director JWT
 * can't call it directly (see KNOWN_GAPS.md), so seeding a real attendance record means acting
 * as a paired device, same as the caregiver kiosk app would. */
export async function pairDevice(director: SeededDirector, locationId: string, groupId: string): Promise<string> {
  const pairing = await asJson<{ deviceToken: string }>(
    await fetch(
      `${API_BASE_URL}/api/devices/pair`,
      authedJson(director.accessToken, { locationId, groupId, directorOverridePin: "123456" }),
    ),
  );
  return pairing.deviceToken;
}

/** Checks a child in for `date` (default today) using a paired device's token, producing a real
 * attendance record for the director's Attendance screen to display/correct. */
export async function deviceCheckIn(deviceToken: string, childId: string, date?: string): Promise<void> {
  const res = await fetch(`${API_BASE_URL}/api/attendance/check-in`, {
    method: "POST",
    headers: { "Content-Type": "application/json", Authorization: `Bearer ${deviceToken}` },
    body: JSON.stringify({ childId, date: date ?? new Date().toISOString().slice(0, 10) }),
  });
  if (!res.ok) {
    throw new Error(`Seed request failed: ${res.status} ${res.url}\n${await res.text()}`);
  }
}

/** A group activity logged via a paired device (creating one is DeviceAuthenticated-only, same
 * as attendance check-in — the web Groups screen is a read/delete timeline, never a composer). */
export async function seedGroupActivity(
  deviceToken: string,
  title: string,
  activityType: "Outdoor" | "Creative" | "Music" | "Story" | "Celebration" | "Other" = "Outdoor",
): Promise<void> {
  const res = await fetch(`${API_BASE_URL}/api/group-activities`, {
    method: "POST",
    headers: { "Content-Type": "application/json", Authorization: `Bearer ${deviceToken}` },
    body: JSON.stringify({ id: null, activityType, title, description: null, occurredAt: new Date().toISOString() }),
  });
  if (!res.ok) {
    throw new Error(`Seed request failed: ${res.status} ${res.url}\n${await res.text()}`);
  }
}

export interface SeededContact {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
}

/** A contact linked to `childId` with pickup permission — eligible for a parent invitation
 * (InviteParentDialog's server-side check) without going all the way through accept+login. */
export async function seedContactWithPickup(director: SeededDirector, childId: string): Promise<SeededContact> {
  const stamp = `${Date.now()}-${Math.floor(Math.random() * 1e6)}`;
  const email = `e2e-parent-${stamp}@example.com`;

  const contact = await asJson<{ id: string; firstName: string; lastName: string }>(
    await fetch(
      `${API_BASE_URL}/api/contacts`,
      authedJson(director.accessToken, { firstName: "Parent", lastName: `E2E${stamp}`, phone: "+32 470 00 00 02", email, locale: "en" }),
    ),
  );

  await asJson(
    await fetch(
      `${API_BASE_URL}/api/children/${childId}/contacts`,
      authedJson(director.accessToken, { contactId: contact.id, relationship: "Guardian", canPickup: true, isPrimary: true }),
    ),
  );

  return { id: contact.id, firstName: contact.firstName, lastName: contact.lastName, email };
}

/**
 * A parent account linked to `childId`, ready to log in / call parent-scoped endpoints — chains
 * contact creation → link-to-child → invitation → accept → login, since that's the only path to
 * a parent account (no self-registration, FR-009). The invitation step uses
 * /api/e2e-support/parent-invitations (Development-only, see E2ESupportEndpoints.cs) rather than
 * the real /api/parent-invitations, since the real one never returns the raw token — it's
 * hashed at rest and only ever leaves the process via the invitation email (see
 * ParentInvitationResult.cs's Token field doc comment).
 */
export async function seedParent(director: SeededDirector, childId: string): Promise<SeededParent> {
  const password = "E2ePassw0rd!1";
  const contact = await seedContactWithPickup(director, childId);

  const invitation = await asJson<{ token: string }>(
    await fetch(`${API_BASE_URL}/api/e2e-support/parent-invitations`, authedJson(director.accessToken, { contactId: contact.id })),
  );

  const acceptRes = await fetch(`${API_BASE_URL}/api/parent-invitations/accept`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ organisationSlug: director.organisationSlug, token: invitation.token, password }),
  });
  if (!acceptRes.ok) {
    throw new Error(`Seed request failed: ${acceptRes.status} ${acceptRes.url}\n${await acceptRes.text()}`);
  }

  const login = await asJson<{ accessToken: string }>(
    await fetch(`${API_BASE_URL}/api/auth/login`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ organisationSlug: director.organisationSlug, email: contact.email, password }),
    }),
  );

  return { organisationSlug: director.organisationSlug, email: contact.email, password, accessToken: login.accessToken };
}

/** A message thread started by `parent` about `childId` (only parents can start threads —
 * DirectorOnly can only reply). */
export async function seedMessageThread(parent: SeededParent, childId: string, subject: string, body: string): Promise<void> {
  await asJson(
    await fetch(`${API_BASE_URL}/api/parent/message-threads`, authedJson(parent.accessToken, { childId, subject, body })),
  );
}

/** An active Mon–Fri contract for `childId` at `locationId` — approving an absence/extra
 * request needs to resolve which location it applies to via the child's active contract
 * (ApproveDayReservationCommand.cs), so day-reservation approval tests need this seeded first. */
export async function seedActiveContract(director: SeededDirector, childId: string, locationId: string): Promise<void> {
  // No JsonStringEnumConverter is registered API-wide, so DayOfWeek serializes as its .NET
  // int value (Sunday=0 .. Saturday=6), not the enum name — 1..5 is Mon..Fri.
  const contractedDays = [1, 2, 3, 4, 5].map((weekday) => ({
    weekday,
    startTime: "08:00:00",
    endTime: "17:00:00",
  }));

  const contract = await asJson<{ id: string }>(
    await fetch(
      `${API_BASE_URL}/api/children/${childId}/contracts`,
      authedJson(director.accessToken, {
        locationId,
        startDate: new Date().toISOString().slice(0, 10),
        endDate: null,
        contractedDays,
        dailyRateCents: 5000,
        consent: null,
      }),
    ),
  );

  await asJson(await fetch(`${API_BASE_URL}/api/contracts/${contract.id}/activate`, authedJson(director.accessToken, {})));
}

/** A pending day-reservation request submitted by `parent` (only parents can submit — DirectorOnly
 * can only approve/reject). Defaults a fresh location's ReservationAbsencesMode ("Approval") means
 * an "absence" request lands as pending, ready for the director Requests screen to act on. */
export async function submitDayReservation(
  parent: SeededParent,
  childId: string,
  requestedDate: string,
  type: "Absence" | "Extra" | "Exchange" = "Absence",
): Promise<void> {
  await asJson(
    await fetch(
      `${API_BASE_URL}/api/day-reservations`,
      authedJson(parent.accessToken, { childId, type, requestedDate, exchangeForDate: null, reason: "E2E seeded request" }),
    ),
  );
}
