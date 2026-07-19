import React, { useCallback, useEffect, useState } from "react";
import { View, Text, ScrollView, TouchableOpacity, Alert } from "react-native";
import { useLocalSearchParams } from "expo-router";
import { useTranslation } from "react-i18next";
import { Plus, AlertTriangle, Syringe, HeartPulse, Clock, Sparkles, Mail } from "lucide-react-native";
import { getCached } from "../../../services/readCache";
import { getPending } from "../../../services/offlineQueue";
import { listChildEvents, deleteChildEvent, updateChildEvent, resendDailyReportEmail } from "../../../services/childEvents";
import { getChildHealthSummary, type HealthSummaryLoadResult } from "../../../services/healthSummary";
import { fetchMilestonePortfolio } from "../../../services/milestones";
import { useColors } from "../../../hooks/useColors";
import { useNetworkStatus } from "../../../hooks/useNetworkStatus";
import { QuickActionSheet } from "../../../components/QuickActionSheet";
import { EventTimeline, type EventSyncStatus } from "../../../components/EventTimeline";
import { EditEventModal } from "../../../components/EditEventModal";
import { IncidentReportForm } from "../../../components/IncidentReportForm";
import { MilestoneEntrySheet } from "../../../components/milestones/MilestoneEntrySheet";
import { MilestoneTimeline, type MilestoneTimelineEntry } from "../../../components/milestones/MilestoneTimeline";
import type { ChildResponse, ChildEventResponse, IncidentReportResponse, MilestoneObservationResponse } from "../../../types";
import { CHILDREN_CACHE_KEY } from "../index";

/** Reconstructs a queued (not-yet-synced) child_event `create` row into a displayable event —
 * offline-recorded events must appear immediately (FR-012), before the server has ever seen
 * them or assigned recordedBy. */
function toOptimisticEvent(payload: Record<string, unknown>): ChildEventResponse {
  const now = new Date().toISOString();
  return {
    id: payload.id as string,
    childId: payload.childId as string,
    eventType: payload.eventType as ChildEventResponse["eventType"],
    occurredAt: payload.occurredAt as string,
    endedAt: (payload.endedAt as string | null) ?? null,
    payload: (payload.payload as Record<string, unknown>) ?? {},
    visibleToParent: (payload.visibleToParent as boolean) ?? true,
    recordedBy: [],
    administeredBy: (payload.administeredByStaffId as string | null) ?? null,
    createdAt: now,
    updatedAt: now,
  };
}

export default function ChildDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const { t, i18n } = useTranslation();
  const colors = useColors();
  const { isConnected } = useNetworkStatus();

  const children = getCached<ChildResponse[]>(CHILDREN_CACHE_KEY) ?? [];
  const child = children.find((c) => c.id === id);

  const [events, setEvents] = useState<ChildEventResponse[]>([]);
  const [syncStatusByEventId, setSyncStatusByEventId] = useState<Record<string, EventSyncStatus>>({});
  const [sheetVisible, setSheetVisible] = useState(false);
  const [editingEvent, setEditingEvent] = useState<ChildEventResponse | null>(null);

  // No device-token-accessible "list incident reports for this child" endpoint exists
  // (FR-018/contracts/incident-reports-api.md only exposes GET by id to a device) — this list
  // is session-local: reports filed this session, plus whatever is still queued offline.
  // Whether a synced report stays visible here after a screen reload isn't spec'd; a director's
  // Incidents screen (013b) is the durable, queryable record (US4 Acceptance Scenario 2).
  const [incidentReports, setIncidentReports] = useState<IncidentReportResponse[]>([]);
  const [incidentPendingIds, setIncidentPendingIds] = useState<Set<string>>(new Set());
  const [incidentFormVisible, setIncidentFormVisible] = useState(false);

  const [healthSummary, setHealthSummary] = useState<HealthSummaryLoadResult | null>(null);

  const [milestoneEntries, setMilestoneEntries] = useState<MilestoneTimelineEntry[]>([]);
  const [milestoneSheetVisible, setMilestoneSheetVisible] = useState(false);

  const [resendingDailyReport, setResendingDailyReport] = useState(false);

  const load = useCallback(async () => {
    if (!id) return;

    let serverEvents: ChildEventResponse[] = [];
    try {
      const page = await listChildEvents(id);
      serverEvents = page.items;
    } catch {
      // Offline or request failed — fall through to whatever is queued locally; there is no
      // read-cache for the timeline in this pass (spec.md scopes caching to the group view).
    }

    const pending = await getPending();
    const statusMap: Record<string, EventSyncStatus> = {};
    const optimisticCreates: ChildEventResponse[] = [];

    for (const row of pending) {
      if (row.entity_type !== "child_event") continue;
      let parsed: Record<string, unknown>;
      try {
        parsed = JSON.parse(row.payload);
      } catch {
        continue;
      }

      const status: EventSyncStatus = row.sync_error?.startsWith("rejected: ") ? "needs_review" : "pending";

      if (row.operation === "create" && parsed.childId === id) {
        const eventId = parsed.id as string;
        if (!serverEvents.some((e) => e.id === eventId)) {
          optimisticCreates.push(toOptimisticEvent(parsed));
        }
        statusMap[eventId] = status;
      } else if (row.operation === "update" || row.operation === "delete") {
        const match = row.endpoint.match(/\/api\/child-events\/([^/]+)$/);
        if (match) statusMap[match[1]] = status;
      }
    }

    const merged = [...optimisticCreates, ...serverEvents].sort(
      (a, b) => new Date(b.occurredAt).getTime() - new Date(a.occurredAt).getTime()
    );
    setEvents(merged);
    setSyncStatusByEventId(statusMap);

    // T058: a queued incident_report create clears from `pending` once synced — its badge just
    // stops rendering rather than the report disappearing (it's already in `incidentReports`
    // state from the optimistic add in handleIncidentSaved).
    const stillPendingIds = new Set(
      pending
        .filter((row) => {
          if (row.entity_type !== "incident_report" || row.operation !== "create") return false;
          try {
            return JSON.parse(row.payload).localId !== undefined;
          } catch {
            return false;
          }
        })
        .map((row) => JSON.parse(row.payload).localId as string)
    );
    setIncidentPendingIds(stillPendingIds);

    setHealthSummary(await getChildHealthSummary(id));

    try {
      const domains = await fetchMilestonePortfolio(id);
      const flattened: MilestoneTimelineEntry[] = domains
        .flatMap((domain) => domain.milestones)
        .flatMap((milestone) =>
          (milestone.history ?? []).map((observation) => ({
            observationId: observation.id,
            milestoneDescription: i18n.language.startsWith("fr")
              ? milestone.descriptionFr
              : i18n.language.startsWith("en")
                ? milestone.descriptionEn
                : milestone.descriptionNl,
            status: observation.status,
            observedAt: observation.observedAt,
            createdAt: observation.createdAt,
          }))
        )
        .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
        .slice(0, 20);
      setMilestoneEntries(flattened);
    } catch {
      // Offline or request failed — leave whatever was last loaded (no offline read-cache for
      // this feed yet, matching child_events' own timeline precedent above).
    }
  }, [id]);

  useEffect(() => {
    load();
  }, [load]);

  if (!child) {
    return (
      <View style={{ flex: 1, alignItems: "center", justifyContent: "center", backgroundColor: colors.background }}>
        <Text style={{ color: colors.textSoft }}>{t("groupView.empty")}</Text>
      </View>
    );
  }

  const inProgressSleepEventId = events.find((e) => e.eventType === "sleep" && !e.endedAt)?.id ?? null;

  const handleDelete = async (event: ChildEventResponse) => {
    await deleteChildEvent(event.id, isConnected);
    load();
  };

  // User Story 3 (spec.md): unaffected by digest-unsubscribe state, online-first (T056).
  const handleResendDailyReport = async () => {
    if (!child || resendingDailyReport) return;
    setResendingDailyReport(true);
    try {
      const sentCount = await resendDailyReportEmail(child.id);
      const sentMessageKey =
        sentCount === 0
          ? "child.dailyReportResend.sentMessageZero"
          : sentCount === 1
            ? "child.dailyReportResend.sentMessageOne"
            : "child.dailyReportResend.sentMessageMany";
      Alert.alert(t("child.dailyReportResend.sentTitle"), t(sentMessageKey, { count: sentCount }));
    } catch {
      Alert.alert(t("child.dailyReportResend.failedTitle"), t("child.dailyReportResend.failedMessage"));
    } finally {
      setResendingDailyReport(false);
    }
  };

  const handleIncidentSaved = (report: IncidentReportResponse) => {
    setIncidentReports((prev) => [report, ...prev]);
    setIncidentFormVisible(false);
    load();
  };

  // Optimistic prepend, mirroring handleIncidentSaved above — required so an offline recording
  // is visible immediately (matches EventTimeline's pending-sync pattern), since load()'s
  // portfolio refetch below silently no-ops while offline.
  const handleMilestoneSaved = (
    observation: MilestoneObservationResponse,
    milestoneDescription: string,
    isPending: boolean
  ) => {
    setMilestoneEntries((prev) =>
      [
        {
          observationId: observation.id,
          milestoneDescription,
          status: observation.status,
          observedAt: observation.observedAt,
          createdAt: observation.createdAt,
          pending: isPending,
        },
        ...prev,
      ].slice(0, 20)
    );
    load();
  };

  return (
    <View style={{ flex: 1, backgroundColor: colors.background }}>
      <ScrollView contentContainerStyle={{ padding: 16 }}>
        <Text className="text-text dark:text-text-dark text-2xl font-bold mb-1">
          {child.firstName} {child.lastName}
        </Text>
        <Text style={{ color: colors.textSoft, marginBottom: 16 }}>{t("child.medicalQuickAccess")}</Text>

        {!!child.allergiesDescription && (
          <View className="bg-danger-bg dark:bg-danger-bg-dark rounded-xl p-4 mb-3">
            <Text className="text-danger dark:text-danger-dark font-semibold mb-1">{t("child.allergyAlert")}</Text>
            <Text className="text-danger dark:text-danger-dark">{child.allergiesDescription}</Text>
            {!!child.allergySeverity && (
              <Text className="text-danger dark:text-danger-dark mt-1 opacity-80">{child.allergySeverity}</Text>
            )}
          </View>
        )}

        {!!child.medicalConditions && (
          <View className="bg-surface dark:bg-surface-dark rounded-xl p-4 mb-3">
            <Text className="text-text dark:text-text-dark font-semibold mb-1">
              {t("child.medicalConditions")}
            </Text>
            <Text className="text-text-soft dark:text-text-soft-dark">{child.medicalConditions}</Text>
          </View>
        )}

        {!!child.dietaryRestrictions && (
          <View className="bg-surface dark:bg-surface-dark rounded-xl p-4 mb-3">
            <Text className="text-text dark:text-text-dark font-semibold mb-1">
              {t("child.dietaryRestrictions")}
            </Text>
            <Text className="text-text-soft dark:text-text-soft-dark">{child.dietaryRestrictions}</Text>
          </View>
        )}

        {(!!child.gpName || !!child.gpPhone) && (
          <View className="bg-surface dark:bg-surface-dark rounded-xl p-4 mb-3">
            <Text className="text-text dark:text-text-dark font-semibold mb-1">{t("child.gpTitle")}</Text>
            {!!child.gpName && <Text className="text-text-soft dark:text-text-soft-dark">{child.gpName}</Text>}
            {!!child.gpPhone && <Text className="text-text-soft dark:text-text-soft-dark">{child.gpPhone}</Text>}
          </View>
        )}

        {(!!child.pediatricianName || !!child.pediatricianPhone) && (
          <View className="bg-surface dark:bg-surface-dark rounded-xl p-4 mb-3">
            <Text className="text-text dark:text-text-dark font-semibold mb-1">{t("child.pediatricianTitle")}</Text>
            {!!child.pediatricianName && <Text className="text-text-soft dark:text-text-soft-dark">{child.pediatricianName}</Text>}
            {!!child.pediatricianPhone && <Text className="text-text-soft dark:text-text-soft-dark">{child.pediatricianPhone}</Text>}
          </View>
        )}

        {healthSummary?.status === "unavailable" && (
          <View className="bg-surface dark:bg-surface-dark rounded-xl p-4 mb-3">
            <Text className="text-text-soft dark:text-text-soft-dark">{t("child.healthSummary.unavailable")}</Text>
          </View>
        )}

        {healthSummary?.status === "loaded" &&
          healthSummary.summary.activeHealthRecords.length === 0 &&
          healthSummary.summary.dueSoonVaccines.length === 0 && (
            <View className="bg-surface dark:bg-surface-dark rounded-xl p-4 mb-3 items-center">
              <HeartPulse size={20} strokeWidth={2} color={colors.textSoft} />
              <Text className="text-text-soft dark:text-text-soft-dark mt-2">{t("child.healthSummary.empty")}</Text>
            </View>
        )}

        {healthSummary?.status === "loaded" && healthSummary.summary.dueSoonVaccines.map((flag) => (
          <View
            key={flag.vaccineName}
            className={`flex-row items-center rounded-xl p-4 mb-3 ${flag.isOverdue ? "bg-danger-bg dark:bg-danger-bg-dark" : "bg-warning dark:bg-warning-dark"}`}
          >
            {flag.isOverdue ? (
              <AlertTriangle size={20} strokeWidth={2} color={colors.danger} />
            ) : (
              <Clock size={20} strokeWidth={2} color={colors.warningFg} />
            )}
            <View className="ml-2 flex-1">
              <Text className={flag.isOverdue ? "text-danger dark:text-danger-dark font-semibold" : "text-warning-fg font-semibold"}>
                {flag.vaccineName}
              </Text>
              <Text className={flag.isOverdue ? "text-danger dark:text-danger-dark opacity-80" : "text-warning-fg opacity-80"}>
                {t(flag.isOverdue ? "child.healthSummary.overdue" : "child.healthSummary.dueSoon", { date: flag.nextDueDate })}
              </Text>
            </View>
          </View>
        ))}

        {healthSummary?.status === "loaded" && healthSummary.summary.activeHealthRecords.map((record) => (
          <View key={record.id} className="bg-surface dark:bg-surface-dark rounded-xl p-4 mb-3">
            <View className="flex-row items-center mb-1">
              <Syringe size={16} strokeWidth={2} color={colors.textSoft} />
              <Text className="text-text dark:text-text-dark font-semibold ml-2">{record.title}</Text>
            </View>
            <Text className="text-text-soft dark:text-text-soft-dark">{record.description}</Text>
          </View>
        ))}

        <TouchableOpacity
          onPress={() => setIncidentFormVisible(true)}
          style={{ minHeight: 48 }}
          className="flex-row items-center rounded-xl bg-danger-bg dark:bg-danger-bg-dark px-4 mb-3 active:opacity-60"
        >
          <AlertTriangle size={20} strokeWidth={2} color={colors.danger} />
          <Text className="text-danger dark:text-danger-dark font-semibold ml-2">
            {t("incidentReports.entryPoint")}
          </Text>
        </TouchableOpacity>

        {incidentReports.map((report) => (
          <View key={report.id} className="bg-surface dark:bg-surface-dark rounded-xl p-4 mb-3">
            <Text className="text-text dark:text-text-dark font-semibold mb-1">
              {t(`incidentReports.injuryTypes.${report.injuryType}`)}
            </Text>
            <Text className="text-text-soft dark:text-text-soft-dark">{report.description}</Text>
            {incidentPendingIds.has(report.id) && (
              <View className="self-start rounded-full px-2 py-0.5 mt-2 bg-info dark:bg-info-dark">
                <Text className="text-xs font-medium text-white">{t("incidentReports.pendingSync")}</Text>
              </View>
            )}
          </View>
        ))}

        <EventTimeline
          events={events}
          syncStatusByEventId={syncStatusByEventId}
          onEdit={setEditingEvent}
          onDelete={handleDelete}
        />

        <TouchableOpacity
          onPress={() => setMilestoneSheetVisible(true)}
          style={{ minHeight: 48 }}
          className="flex-row items-center rounded-xl bg-surface-soft dark:bg-surface-soft-dark px-4 mt-3 mb-3 active:opacity-60"
        >
          <Sparkles size={20} strokeWidth={2} color={colors.primaryHover} />
          <Text className="text-text dark:text-text-dark font-semibold ml-2">{t("milestones.entryPoint")}</Text>
        </TouchableOpacity>

        <MilestoneTimeline entries={milestoneEntries} />

        <TouchableOpacity
          onPress={handleResendDailyReport}
          disabled={resendingDailyReport}
          style={{ minHeight: 48 }}
          className="flex-row items-center rounded-xl bg-surface-soft dark:bg-surface-soft-dark px-4 mt-3 active:opacity-60 disabled:opacity-50"
        >
          <Mail size={20} strokeWidth={2} color={colors.primaryHover} />
          <Text className="text-text dark:text-text-dark font-semibold ml-2">
            {t("child.dailyReportResend.action")}
          </Text>
        </TouchableOpacity>
      </ScrollView>

      <IncidentReportForm
        visible={incidentFormVisible}
        childId={child.id}
        isConnected={isConnected}
        onClose={() => setIncidentFormVisible(false)}
        onSaved={handleIncidentSaved}
      />

      <TouchableOpacity
        onPress={() => setSheetVisible(true)}
        style={{ position: "absolute", right: 20, bottom: 20, width: 64, height: 64, borderRadius: 32 }}
        className="items-center justify-center bg-primary dark:bg-primary-dark active:opacity-60"
      >
        <Plus size={24} strokeWidth={2} color="#FFFFFF" />
      </TouchableOpacity>

      <QuickActionSheet
        visible={sheetVisible}
        childId={child.id}
        inProgressSleepEventId={inProgressSleepEventId}
        onClose={() => setSheetVisible(false)}
        onEventRecorded={load}
      />

      <EditEventModal
        event={editingEvent}
        isConnected={isConnected}
        onClose={() => setEditingEvent(null)}
        onSaved={load}
      />

      <MilestoneEntrySheet
        visible={milestoneSheetVisible}
        childId={child.id}
        isConnected={isConnected}
        onClose={() => setMilestoneSheetVisible(false)}
        onSaved={handleMilestoneSaved}
      />
    </View>
  );
}
