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
  isPlatformAdmin: boolean;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  user: AuthenticatedUser;
}

export interface OrganisationResponse {
  name: string;
  kboNumber: string | null;
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

export type ReservationRequestMode = "disabled" | "informational" | "approval";

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
  reservationAbsencesMode: ReservationRequestMode;
  reservationExtrasMode: ReservationRequestMode;
  reservationSwapsMode: ReservationRequestMode;
  reservationNoticeHours: number;
  requiresCaregiverPin: boolean;
  menuVariantPriorityOrder: string[];
  menuVariantsWithPublishedContent: string[];
  erkenningsnummer: string | null;
  bankAccountNumber: string | null;
  invoiceDueDays: number;
  paymentRemindersEnabled: boolean;
  paymentReminderDelayDays: number;
  paymentReminderCadenceDays: number;
  // Feature 030 — contracts/family-siblings-api.md.
  siblingDiscountPct: number;
  familyInvoiceBundlingEnabled: boolean;
  // Feature 021 — contracts/021-qr-checkin/qr-checkin-api.md.
  qrCheckInEnabled: boolean;
}

// Feature 014a — contracts/014a-invoice-payments-plus/payments-api.md.
export interface PaymentConnectionResponse {
  status: "connected" | "disconnected";
  providerAccountLabel: string | null;
  connectedAt: string | null;
}

export interface PaymentAuthorizationUrlResponse {
  authorizationUrl: string;
}

export interface InvoiceExtraChargeResponse {
  label: string;
  amountCents: number;
}

export interface InvoiceLineItemsResponse {
  presentDays: number;
  unjustifiedAbsentDays: number;
  dailyRateCents: number;
  closureDaysExcluded: number;
  daysMin5u: number;
  daysMin11u: number;
  extraCharges: InvoiceExtraChargeResponse[];
}

export type InvoiceStatus = "draft" | "sent" | "paid";

export interface InvoiceResponse {
  id: string;
  childId: string;
  childName: string;
  contractId: string;
  locationId: string;
  locationName: string;
  periodMonth: string;
  status: InvoiceStatus;
  isOverdue: boolean;
  subtotalCents: number;
  totalCents: number;
  lineItems: InvoiceLineItemsResponse;
  ogmReference: string;
  dueDate: string | null;
  sentAt: string | null;
  paidAt: string | null;
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
  // Feature 013f FR-014 — present only on errors.location.reservation_settings.pending_requests_warning.
  pendingCounts?: Partial<Record<"absence" | "extra" | "exchange", number>>;
  // Feature 013j FR-014 — present only on errors.location.menu_variant_settings.removing_published_warning.
  variants?: string[];
}

// ── Attendance (feature 010) ─────────────────────────────────────────────────────
export type AttendanceStatus = "present" | "absent" | "closure";

export interface AttendanceRecordResponse {
  id: string;
  childId: string;
  locationId: string;
  date: string;
  status: AttendanceStatus;
  checkInAt: string | null;
  checkOutAt: string | null;
  plannedDurationMinutes: number | null;
  absenceJustified: boolean | null;
  absenceReason: string | null;
  recordedBy: string[];
  createdAt: string;
  updatedAt: string;
}

export interface PagedAttendanceResponse {
  items: AttendanceRecordResponse[];
  nextCursor: string | null;
}

// ── Closure calendar (feature 011) ─────────────────────────────────────────────
export type ClosureType = "holiday" | "training" | "extraordinary";
export type ClosureStatus = "draft" | "published" | "cancelled";

export interface ClosureDeliverySummaryResponse {
  sent: number;
  failed: number;
  messageCount: number;
}

export interface ClosureDayResponse {
  id: string;
  locationId: string;
  date: string;
  label: string;
  closureType: ClosureType;
  notifyParents: boolean;
  status: ClosureStatus;
  notificationSentAt: string | null;
  publishedAt: string | null;
  cancelledAt: string | null;
  deliverySummary: ClosureDeliverySummaryResponse;
  createdAt: string;
  updatedAt: string;
}

export interface ClosureNotificationSummaryResponse {
  recipients: number;
  pushSent: number;
  pushFailed: number;
  messagesCreated: number;
}

export interface PublishClosureDayResponse {
  closure: ClosureDayResponse;
  attendanceRecordsCreated: number;
  attendanceRecordsUpdated: number;
  requiresAttendanceConfirmation: boolean;
  notificationSummary: ClosureNotificationSummaryResponse;
}

export interface CancelClosureDayResponse {
  closure: ClosureDayResponse;
  attendanceRecordsReleased: number;
  attendanceRecordsPreserved: number;
  notificationSummary: ClosureNotificationSummaryResponse;
}

export interface GroupResponse {
  id: string;
  name: string;
  locationId: string;
  capacity: number | null;
}

// ── Staff scheduling (feature 012) ──────────────────────────────────────────────
export type AbsenceReason = "sick" | "leave" | "holiday";

export interface StaffScheduleResponse {
  id: string;
  staffProfileId: string;
  locationId: string;
  groupId: string | null;
  date: string;
  startTime: string;
  endTime: string;
  isAbsent: boolean;
  absenceReason: AbsenceReason | null;
  createdAt: string;
  updatedAt: string;
}

export interface CopyWeekSkippedEntryResponse {
  date: string;
  staffProfileId: string;
  reason: "closure_day" | "existing_entry";
}

export interface CopyWeekResponse {
  copiedCount: number;
  skipped: CopyWeekSkippedEntryResponse[];
}

export interface ProjectedOnDutyResponse {
  projectedOnDutyCount: number;
  staffProfileIds: string[];
}

export type WaitingListStatus = "waiting" | "offered" | "enrolled" | "withdrawn";

export interface WaitingListEntryResponse {
  id: string;
  childFirstName: string;
  childLastName: string;
  dateOfBirth: string;
  contactName: string;
  contactEmail: string | null;
  contactPhone: string | null;
  locationId: string;
  requestedStartDate: string | null;
  priority: number;
  status: WaitingListStatus;
  notes: string | null;
  childId: string | null;
  isDuplicate: boolean;
  registeredAt: string;
  updatedAt: string | null;
}

export interface OccupancyDayResponse {
  date: string;
  freeCapacity: number | null;
  closed: boolean;
}

export type DayReservationType = "absence" | "extra" | "exchange";
export type DayReservationStatus = "pending" | "approved" | "rejected" | "cancelled";

export interface DayReservationResponse {
  id: string;
  childId: string;
  childDisplayName: string;
  type: DayReservationType;
  requestedDate: string;
  exchangeForDate: string | null;
  reason: string | null;
  absenceJustified: boolean | null;
  status: DayReservationStatus;
  requestedBy: string;
  decidedBy: string | null;
  decidedAt: string | null;
  directorNotes: string | null;
  capacityWarning: boolean | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface ChildResponse {
  id: string;
  firstName: string;
  lastName: string;
  dateOfBirth: string;
  photoDownloadUrl: string | null;
  gender: string | null;
  nationality: string | null;
  allergiesDescription: string | null;
  allergySeverity: string | null;
  medicalConditions: string | null;
  dietaryRestrictions: string | null;
  gpName: string | null;
  gpPhone: string | null;
  pediatricianName: string | null;
  pediatricianPhone: string | null;
  healthInsuranceNumber: string | null;
  kindcode: string | null;
  deactivatedAt: string | null;
  createdAt: string;
  updatedAt: string;
  // Feature 022 — null for Staff/device-token callers regardless of actual verification state
  // (FR-015; backend/ChildCare.Application/Children/ChildMapper.cs).
  idVerifiedAt: string | null;
  idVerifiedByEmail: string | null;
  idDocumentType: IdDocumentType | null;
  idDocumentNote: string | null;
  firstIdVerifiedAt: string | null;
  firstIdVerifiedByEmail: string | null;
  nrnLast4: string | null;
}

export type IdDocumentType = "birth_certificate" | "kids_id" | "eid" | "passport" | "other";

// 031-photo-lifecycle-governance — POST /api/children/{id}/purge-photos.
export interface PurgePhotosResponse {
  deletedObjectPaths: string[];
  failedObjectPaths: string[];
  preservedGroupPhotoCount: number;
}

// Feature 013 — parent communication.

export interface ContactResponse {
  id: string;
  firstName: string;
  lastName: string;
  phone: string;
  email: string | null;
  locale: string;
  // Feature 022 — every contact-reading route is DirectorOnly, so unlike ChildResponse these
  // are never null due to caller role (only null when genuinely unverified).
  idVerifiedAt: string | null;
  idVerifiedByEmail: string | null;
  idDocumentType: IdDocumentType | null;
  idDocumentNote: string | null;
  firstIdVerifiedAt: string | null;
  firstIdVerifiedByEmail: string | null;
}

// Feature 030 (US4) — contracts/family-siblings-api.md.
export type ContactRelationship = "Mother" | "Father" | "Guardian" | "EmergencyContact" | "AuthorisedPickup" | "FosterParent" | "Other";

export interface ChildContactResponse {
  contactId: string;
  firstName: string;
  lastName: string;
  phone: string;
  email: string | null;
  locale: string;
  relationship: ContactRelationship;
  canPickup: boolean;
  isPrimary: boolean;
  idVerifiedAt: string | null;
  idVerifiedByEmail: string | null;
  idDocumentType: IdDocumentType | null;
  idDocumentNote: string | null;
  firstIdVerifiedAt: string | null;
  firstIdVerifiedByEmail: string | null;
}

export interface MessageResponse {
  id: string;
  threadId: string;
  senderId: string;
  senderName: string;
  body: string;
  sentAt: string;
  readAt: string | null;
}

export interface MessageThreadResponse {
  id: string;
  subject: string;
  childId: string | null;
  childName: string | null;
  createdAt: string;
  lastActivityAt: string;
  hasUnread: boolean;
  messages: MessageResponse[];
}

export interface MessageThreadSummaryResponse {
  id: string;
  subject: string;
  childId: string | null;
  childName: string | null;
  lastActivityAt: string;
  hasUnread: boolean;
  unreadFromParentCount: number;
}

export interface AnnouncementResponse {
  id: string;
  locationId: string;
  groupId: string | null;
  subject: string;
  body: string;
  sentByTenantUserId: string;
  sentAt: string;
  recipientCount: number;
}

export interface ParentInvitationResponse {
  invitationId: string;
  contactId: string;
  email: string;
  expiresAt: string;
}

// ── Group activities (feature 009b) ──────────────────────────────────────────────
export type GroupActivityType = "outdoor" | "creative" | "music" | "story" | "celebration" | "other";

export interface GroupActivityPhotoResponse {
  id: string;
  downloadUrl: string | null;
  thumbnailDownloadUrl: string | null;
  caption: string | null;
  uploadedAt: string;
}

export interface GroupActivityResponse {
  id: string;
  groupId: string;
  activityType: GroupActivityType;
  title: string;
  description: string | null;
  occurredAt: string;
  recordedBy: string[];
  photos: GroupActivityPhotoResponse[];
  createdAt: string;
}

// Minimal shape this feature's merged timeline needs to render — feature 009 owns the full
// ChildEventResponse contract; no other web screen has needed it before this one.
export interface ChildEventResponse {
  id: string;
  childId: string;
  eventType: string;
  occurredAt: string;
  payload: Record<string, unknown>;
}

export interface GroupTimelineEntryResponse {
  kind: "child_event" | "group_activity";
  occurredAt: string;
  childEvent: ChildEventResponse | null;
  groupActivity: GroupActivityResponse | null;
}

export interface GroupTimelineResponse {
  entries: GroupTimelineEntryResponse[];
}

// ── Incident reports (feature 013b) ─────────────────────────────────────────────
export interface IncidentReportResponse {
  id:                string;
  childId:           string;
  locationId:        string;
  occurredAt:        string;
  locationDetail:    string | null;
  description:       string;
  injuryType:        string;
  firstAidGiven:     string | null;
  doctorCalled:      boolean;
  doctorNotes:       string | null;
  parentNotified:    boolean;
  parentNotifiedAt:  string | null;
  parentNotifiedHow: string | null;
  reportedBy:        string[];
  witnesses:         string | null;
  followUp:          string | null;
  reviewedAt:        string | null;
  createdAt:         string;
  updatedAt:         string | null;
}

export interface PagedIncidentReportsResponse {
  items:      IncidentReportResponse[];
  page:       number;
  pageSize:   number;
  totalCount: number;
}

// ── Vaccine & health records (feature 013c) ────────────────────────────────────
export interface VaccineRecordResponse {
  id:                    string;
  childId:               string;
  vaccineName:           string;
  vaccineTypeId:         string | null;
  attachmentDownloadUrl: string | null;
  doseNumber:            number | null;
  administeredOn:        string;
  nextDueDate:           string | null;
  administeredBy:        string | null;
  notes:                 string | null;
  recordedBy:            string | null;
  createdAt:             string;
  updatedAt:             string;
}

// ── Vaccine catalog & custom entries (feature 013g) ─────────────────────────────
export type VaccineCategory = "basisvaccinatieschema" | "aanbevolen_niet_gratis";

export interface VaccineTypeResponse {
  id:       string;
  name:     string;
  category: VaccineCategory | null;
  sortOrder: number;
}

export interface CustomVaccineEntryResponse {
  id:   string;
  name: string;
}

// ── Platform-admin vaccine catalog management (feature 013h) ───────────────────
export interface PlatformAdminVaccineTypeResponse {
  id:                 string;
  name:               string;
  category:           VaccineCategory | null;
  sortOrder:          number;
  isActive:           boolean;
  deactivatedByEmail: string | null;
  deactivatedAt:      string | null;
}

export interface VaccinationsDueSoonResponse {
  childId:      string;
  childName:    string;
  locationId:   string;
  vaccineName:  string;
  nextDueDate:  string;
  isOverdue:    boolean;
}

export type HealthRecordType = "allergy" | "chronic_condition" | "medication_standing" | "doctor_note" | "other";

export interface HealthRecordResponse {
  id:                    string;
  childId:               string;
  recordType:            HealthRecordType;
  title:                 string;
  description:           string;
  validFrom:             string | null;
  validUntil:            string | null;
  isExpired:             boolean;
  attachmentDownloadUrl: string | null;
  recordedBy:            string | null;
  createdAt:             string;
  updatedAt:             string | null;
}

export type MealTexture = "pureed" | "mixed" | "pieces" | "normal";
export type MealPortionSize = "small" | "normal" | "large";
export type AllergySeverityWireValue = "severe" | "mild_moderate" | "none";

export interface MealListChildEntry {
  childId:              string;
  firstName:            string;
  lastName:             string;
  texture:               MealTexture;
  dietaryType:            string[];
  portionSize:            MealPortionSize;
  additionalNotes:        string | null;
  hasPreference:          boolean;
  allergySeverity:        AllergySeverityWireValue;
  hasStandingMedication:  boolean;
}

export interface MealListGroupEntry {
  groupId:   string;
  groupName: string;
  children:  MealListChildEntry[];
}

export interface MealListResponse {
  date:     string;
  groups:   MealListGroupEntry[];
  expected: { children: MealListChildEntry[] } | null;
}

export interface MealPreferenceResponse {
  childId:         string;
  texture:          MealTexture;
  dietaryType:      string[];
  portionSize:      MealPortionSize;
  additionalNotes:  string | null;
  updatedBy:        string | null;
  updatedAt:        string | null;
}

// Feature 013e — contracts/monthly-menu-api.md.
export interface MonthlyMenuDayEntry {
  date:       string;
  soup:       string | null;
  mainCourse: string | null;
  dessert:    string | null;
  notes:      string | null;
}

export interface MonthlyMenuResponse {
  exists:      boolean;
  variant:     string | null;
  isPublished: boolean;
  publishedAt: string | null;
  days:        MonthlyMenuDayEntry[];
}

export interface MonthlyMenuPublishStateResponse {
  isPublished: boolean;
  publishedAt: string | null;
}

export interface MealPreferenceChangeRequestResponse {
  id:                  string;
  childId:             string;
  childName:           string;
  requestedByName:     string;
  newTexture:          MealTexture | null;
  newDietaryType:      string[] | null;
  notes:               string | null;
  status:              "pending" | "approved" | "rejected";
  createdAt:           string;
  decidedAt:           string | null;
  decisionNotes:       string | null;
  activeHealthRecords: { id: string; recordType: string; title: string; validFrom: string | null; validUntil: string | null }[];
}

export type FiscalAttestationStatus = "generated" | "notYetGenerated" | "failed";

export interface FiscalAttestationPeriodResponse {
  periodStart:    string;
  periodEnd:      string;
  days:           number;
  amountCents:    number;
  dailyRateCents: number | null;
}

export interface FiscalAttestationResponse {
  id:               string | null;
  childId:          string;
  childName:        string;
  locationId:       string;
  locationName:     string;
  taxYear:          number;
  totalAmountCents: number | null;
  status:           FiscalAttestationStatus;
  periods:          FiscalAttestationPeriodResponse[] | null;
  generatedAt:      string | null;
}

export interface GenerateFiscalAttestationsResultItem {
  childId:    string;
  locationId: string;
  status:     "generated" | "alreadyExists" | "noPaidInvoices" | "failed";
}

export interface GenerateFiscalAttestationsResponse {
  taxYear: number;
  results: GenerateFiscalAttestationsResultItem[];
}

export interface FiscalAttestationDownloadUrlResponse {
  downloadUrl: string;
  expiresAt:   string;
}

// ── Developmental Milestones (feature 016) ──────────────────────────────────────────────────
export type MilestoneObservationStatus = "emerging" | "achieved" | "not_yet";

export interface MilestoneObservationResponse {
  id:         string;
  status:     MilestoneObservationStatus;
  observedAt: string;
  notes:      string | null;
  createdAt:  string;
}

export interface DevelopmentalMilestoneResponse {
  id:             string;
  ageFromMonths:  number;
  ageToMonths:    number;
  descriptionNl:  string;
  descriptionFr:  string;
  descriptionEn:  string;
  sortOrder:      number;
  currentStatus:  MilestoneObservationStatus | null;
  isCurrentFocus: boolean;
  history:        MilestoneObservationResponse[] | null;
}

export interface DevelopmentalDomainResponse {
  id:         string;
  code:       string;
  nameNl:     string;
  nameFr:     string;
  nameEn:     string;
  sortOrder:  number;
  milestones: DevelopmentalMilestoneResponse[];
}

// ── Management Reporting (feature 018) ──────────────────────────────────────────────────────
export type OccupancyStatus = "green" | "amber" | "red";

export interface OccupancyDayResponse {
  date:         string;
  freeCapacity: number | null;
  closed:       boolean;
}

export interface OccupancyGroupSummaryResponse {
  groupId:      string;
  groupName:    string;
  presentCount: number;
  capacity:     number | null;
  status:       OccupancyStatus | null;
}

export interface OccupancyLocationSummaryResponse {
  locationId:   string;
  locationName: string;
  presentCount: number;
  capacity:     number;
  status:       OccupancyStatus;
  groups:       OccupancyGroupSummaryResponse[];
  weekAhead:    OccupancyDayResponse[];
}

export interface OccupancySummaryResponse {
  asOf:      string;
  locations: OccupancyLocationSummaryResponse[];
}

export interface BkrGroupRatioResponse {
  groupId:            string;
  locationId:         string;
  presentCount:       number;
  qualifiedStaffCount: number;
  isNapTime:          boolean;
  threshold:          number;
  status:             OccupancyStatus;
}

export interface BkrRatioOverviewResponse {
  asOf:   string;
  groups: BkrGroupRatioResponse[];
}

export interface BkrBreachResponse {
  groupId:    string;
  locationId: string;
  startedAt:  string;
  endedAt:    string | null;
}

export interface BkrBreachHistoryResponse {
  from:     string;
  to:       string;
  breaches: BkrBreachResponse[];
}

export interface AttendanceSummaryRowResponse {
  childId:               string;
  childName:             string;
  groupId:               string | null;
  locationId:            string;
  presentDays:           number;
  absentJustifiedDays:   number;
  absentUnjustifiedDays: number;
  closureDays:           number;
}

export interface AttendanceSummaryTotalResponse {
  id:                    string;
  presentDays:           number;
  absentJustifiedDays:   number;
  absentUnjustifiedDays: number;
  closureDays:           number;
}

export interface AttendanceSummaryResponse {
  month:          string;
  children:       AttendanceSummaryRowResponse[];
  groupTotals:    AttendanceSummaryTotalResponse[];
  locationTotals: AttendanceSummaryTotalResponse[];
}

export interface OverdueInvoiceResponse {
  invoiceId:   string;
  childName:   string;
  dueDate:     string;
  daysOverdue: number;
  totalCents:  number;
}

export interface InvoiceStatusOverviewResponse {
  month:                 string;
  paidCount:             number;
  paidTotalCents:        number;
  outstandingCount:      number;
  outstandingTotalCents: number;
  overdueCount:          number;
  overdueTotalCents:     number;
  totalInvoicedCents:    number;
  overdueInvoices:       OverdueInvoiceResponse[];
}

export type DataCompletenessFlagType =
  | "missing_pickup_contact"
  | "overdue_vaccine"
  | "missing_qualification"
  | "missing_pin"
  | "missing_identity_verification";

export interface DataCompletenessFlagResponse {
  type:        DataCompletenessFlagType;
  subjectType: "child" | "staff";
  subjectId:   string;
  subjectName: string;
  detail:      string | null;
}

export interface DataCompletenessResponse {
  flags: DataCompletenessFlagResponse[];
}
