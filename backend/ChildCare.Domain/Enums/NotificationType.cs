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
}
