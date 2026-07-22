namespace ChildCare.Domain.Enums;

public enum NotificationType
{
    NewMessage,
    Announcement,
    TemperatureAlert,
    DayReservationDecided,
    MealPreferenceRequestDecided,
    InvoiceSent,
    PaymentReminder,
    InvoicePaid,
    FiscalAttestationGenerated,
    EnrollmentSubmitted,

    // Feature 027 — research.md R6.
    SchedulePublished,
    AssignmentChanged,
    LeaveRequestDecided,

    // Feature 027 deviation from research.md R6 (flagged in the implementation report):
    // R6 scoped the new NotificationType additions to the three staff-facing types above, but
    // contracts/staff-app-api.md's ReportSickCommand side effect ("notifies the director") needs
    // a director-facing type distinct from all three — none of them fit a director recipient.
    // Mirrors EnrollmentSubmitted's exact shape (Notification row only, no push — directors have
    // no push-token channel, EnrollmentNotificationService's own documented reasoning).
    StaffSickReported,
}
