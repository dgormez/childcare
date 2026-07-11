import React, { useCallback, useEffect, useState } from "react";
import { View, Text, FlatList, TouchableOpacity, Image, RefreshControl, ActivityIndicator } from "react-native";
import { useRouter } from "expo-router";
import { useTranslation } from "react-i18next";
import Toast from "react-native-toast-message";
import { AlertTriangle, Thermometer, ChevronRight, Plus, ListChecks, X, Check } from "lucide-react-native";
import { apiClient } from "../../services/apiClient";
import { getCached, setCached } from "../../services/readCache";
import { syncPendingQueue } from "../../services/syncEngine";
import { checkIn, checkOut, getBkrRatio, getTodayAttendanceByChildId, todayDateString } from "../../services/attendance";
import { getGroupTimeline } from "../../services/groupActivities";
import { getPendingPhotoCountForActivity } from "../../services/photoUploadQueue";
import { useColors } from "../../hooks/useColors";
import { useNetworkStatus } from "../../hooks/useNetworkStatus";
import { useStore } from "../../store/useStore";
import { BkrIndicator } from "../../components/BkrIndicator";
import { AbsenceDialog } from "../../components/AbsenceDialog";
import { AddGroupActivitySheet } from "../../components/AddGroupActivitySheet";
import { GroupTimeline } from "../../components/GroupTimeline";
import { QuickActionSheet } from "../../components/QuickActionSheet";
import type { AttendanceRecordResponse, BkrRatioResponse, ChildResponse, GroupResponse, GroupTimelineEntryResponse } from "../../types";

const BKR_POLL_INTERVAL_MS = 15_000; // FR-008a: refresh at least every 15 seconds

// spec.md Edge Cases/SC-005 — enforced client-side here and independently server-side
// (RecordChildEventBatchCommandValidator).
const MAX_BATCH_SELECTION = 30;

export const CHILDREN_CACHE_KEY = "children:today";

export function calculateAge(dateOfBirth: string): number {
  const dob = new Date(dateOfBirth);
  const now = new Date();
  let age = now.getFullYear() - dob.getFullYear();
  const hadBirthdayThisYear =
    now.getMonth() > dob.getMonth() || (now.getMonth() === dob.getMonth() && now.getDate() >= dob.getDate());
  if (!hadBirthdayThisYear) age -= 1;
  return age;
}

async function fetchChildren(): Promise<ChildResponse[]> {
  const groupsResult = await apiClient.GET("/api/groups");
  if (!groupsResult.response.ok) throw new Error("group_view_load_failed");
  // openapi-fetch already parses the body into result.data — result.response.json() would
  // throw ("body already read") since the stream is already consumed.
  const groups = groupsResult.data as unknown as GroupResponse[];

  const perGroup = await Promise.all(
    groups.map(async (group) => {
      const childrenResult = await apiClient.GET("/api/children", { params: { query: { groupId: group.id } } });
      if (!childrenResult.response.ok) return [];
      return childrenResult.data as unknown as ChildResponse[];
    })
  );

  // A child cannot hold two simultaneous active group assignments (feature 006), so
  // de-duplication shouldn't be needed in practice — kept defensively regardless.
  const seen = new Set<string>();
  const flattened: ChildResponse[] = [];
  for (const list of perGroup) {
    for (const child of list) {
      if (!seen.has(child.id)) {
        seen.add(child.id);
        flattened.push(child);
      }
    }
  }
  return flattened;
}

export default function GroupViewScreen() {
  const { t } = useTranslation();
  const router = useRouter();
  const colors = useColors();
  const { isConnected } = useNetworkStatus();
  const { device } = useStore();

  const [children, setChildren] = useState<ChildResponse[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [attendanceByChildId, setAttendanceByChildId] = useState<Record<string, AttendanceRecordResponse>>({});
  const [bkr, setBkr] = useState<BkrRatioResponse | null>(null);
  const [absenceTarget, setAbsenceTarget] = useState<ChildResponse | null>(null);
  const [tab, setTab] = useState<"children" | "timeline">("children");
  const [timelineEntries, setTimelineEntries] = useState<GroupTimelineEntryResponse[]>([]);
  const [addActivityVisible, setAddActivityVisible] = useState(false);

  // Feature 009c — research.md R7: multi-select is a mode on this existing roster, not a
  // separate screen/picker.
  const [multiSelectMode, setMultiSelectMode] = useState(false);
  const [selectedChildIds, setSelectedChildIds] = useState<Set<string>>(new Set());
  const [batchSheetVisible, setBatchSheetVisible] = useState(false);

  const loadTimeline = useCallback(async () => {
    if (!device) return;
    try {
      const result = await getGroupTimeline(device.groupId);
      setTimelineEntries(result.entries);
    } catch {
      // Feature 009b: the timeline is a secondary tab on this screen — a failed fetch just
      // leaves the previously-shown entries in place, same degrade-gracefully approach as
      // the child roster/attendance fetches above.
    }
  }, [device]);

  const refreshBkr = useCallback(async () => {
    if (!device) return;
    try {
      setBkr(await getBkrRatio(device.locationId));
    } catch {
      // BKR is a live display-only indicator (FR-009) — a failed refresh just leaves the
      // previous value showing rather than disrupting the screen.
    }
  }, [device]);

  const load = useCallback(async (isRefresh = false) => {
    if (isRefresh) setRefreshing(true); else setLoading(true);
    if (isRefresh) await syncPendingQueue(); // FR-012a: pull-to-refresh is one of the three sync triggers
    try {
      const fresh = await fetchChildren();
      setChildren(fresh);
      setCached(CHILDREN_CACHE_KEY, fresh);
    } catch {
      const cached = getCached<ChildResponse[]>(CHILDREN_CACHE_KEY);
      if (cached) setChildren(cached);
      else if (!isRefresh) setChildren([]);
      // else: refresh failed with nothing cached — leave the currently-shown list as-is
    } finally {
      if (isRefresh) setRefreshing(false); else setLoading(false);
    }
    try {
      setAttendanceByChildId(await getTodayAttendanceByChildId());
    } catch {
      // Offline or request failed — the group view still renders, just without today's
      // present/absent state until the next successful load.
    }
    await refreshBkr();
    await loadTimeline();
  }, [refreshBkr, loadTimeline]);

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // FR-008a: refresh from the server at least every 15 seconds, so a change made by staff on a
  // different device/tablet in the same room becomes visible promptly.
  useEffect(() => {
    const interval = setInterval(refreshBkr, BKR_POLL_INTERVAL_MS);
    return () => clearInterval(interval);
  }, [refreshBkr]);

  const handleCardPress = useCallback(
    async (child: ChildResponse) => {
      const existing = attendanceByChildId[child.id];
      const today = todayDateString();
      try {
        if (existing?.status === "present" && !existing.checkOutAt) {
          const updated = await checkOut(child.id, today, isConnected);
          if (updated) setAttendanceByChildId((prev) => ({ ...prev, [child.id]: updated }));
        } else {
          const updated = await checkIn(child.id, today, isConnected);
          setAttendanceByChildId((prev) => ({ ...prev, [child.id]: updated }));
        }
      } finally {
        // FR-008a: reflect a locally-taken action within 5 seconds — an immediate refresh
        // rather than waiting for the next 15-second poll.
        refreshBkr();
      }
    },
    [attendanceByChildId, isConnected, refreshBkr]
  );

  const handleAbsenceSaved = useCallback(async () => {
    try {
      setAttendanceByChildId(await getTodayAttendanceByChildId());
    } catch {
      // Leave the previous state showing if the refetch fails.
    }
    refreshBkr();
  }, [refreshBkr]);

  const isPresent = useCallback(
    (childId: string) => {
      const attendance = attendanceByChildId[childId];
      return attendance?.status === "present" && !attendance.checkOutAt;
    },
    [attendanceByChildId]
  );

  const enterMultiSelect = useCallback(() => {
    setMultiSelectMode(true);
    setSelectedChildIds(new Set());
  }, []);

  const exitMultiSelect = useCallback(() => {
    setMultiSelectMode(false);
    setSelectedChildIds(new Set());
  }, []);

  const toggleChildSelection = useCallback(
    (childId: string) => {
      setSelectedChildIds((prev) => {
        const next = new Set(prev);
        if (next.has(childId)) {
          next.delete(childId);
          return next;
        }
        if (next.size >= MAX_BATCH_SELECTION) {
          Toast.show({ type: "info", text1: t("groupView.multiSelect.maxReached") });
          return prev;
        }
        next.add(childId);
        return next;
      });
    },
    [t]
  );

  const selectAllPresent = useCallback(() => {
    const presentIds = (children ?? []).filter((c) => isPresent(c.id)).map((c) => c.id);
    if (presentIds.length > MAX_BATCH_SELECTION) {
      Toast.show({ type: "info", text1: t("groupView.multiSelect.maxReached") });
    }
    setSelectedChildIds(new Set(presentIds.slice(0, MAX_BATCH_SELECTION)));
  }, [children, isPresent, t]);

  const handleBatchRecorded = useCallback(
    (result: { createdCount: number; failedCount: number }) => {
      setBatchSheetVisible(false);
      exitMultiSelect();
      if (result.failedCount === 0) {
        Toast.show({ type: "success", text1: t("childEvents.batch.success", { count: result.createdCount }) });
      }
      // FR-008a-style immediate refresh, matching handleCardPress's own pattern.
      refreshBkr();
    },
    [exitMultiSelect, refreshBkr, t]
  );

  if (loading) {
    return (
      <View style={{ flex: 1, alignItems: "center", justifyContent: "center", backgroundColor: colors.background }}>
        <ActivityIndicator size="large" />
      </View>
    );
  }

  return (
    <View style={{ flex: 1, backgroundColor: colors.background }}>
      {bkr && (
        <View style={{ paddingHorizontal: 16, paddingTop: 16 }}>
          <BkrIndicator bkr={bkr} />
        </View>
      )}
      <View className="flex-row items-center" style={{ paddingHorizontal: 16, paddingTop: 12, gap: 8 }}>
        {(["children", "timeline"] as const).map((value) => (
          <TouchableOpacity
            key={value}
            onPress={() => setTab(value)}
            style={{ minHeight: 48, flex: 1 }}
            className={`items-center justify-center rounded-lg ${tab === value ? "bg-primary-soft dark:bg-primary-soft-dark" : "bg-surface-soft dark:bg-surface-soft-dark"}`}
          >
            <Text className="text-text dark:text-text-dark font-medium">
              {t(value === "children" ? "groupView.tabChildren" : "groupActivities.tabTimeline")}
            </Text>
          </TouchableOpacity>
        ))}
        {tab === "children" && (
          <TouchableOpacity
            onPress={multiSelectMode ? exitMultiSelect : enterMultiSelect}
            accessibilityLabel={t(multiSelectMode ? "groupView.multiSelect.cancel" : "groupView.multiSelect.enter")}
            style={{ minWidth: 48, minHeight: 48, alignItems: "center", justifyContent: "center" }}
            className="rounded-lg bg-surface-soft dark:bg-surface-soft-dark"
          >
            {multiSelectMode ? (
              <X size={20} strokeWidth={2} color={colors.textSoft} />
            ) : (
              <ListChecks size={20} strokeWidth={2} color={colors.text} />
            )}
          </TouchableOpacity>
        )}
      </View>
      {multiSelectMode && (
        <View className="flex-row items-center justify-between" style={{ paddingHorizontal: 16, paddingTop: 12 }}>
          <Text className="text-text-soft dark:text-text-soft-dark">
            {t("groupView.multiSelect.selectedCount", { count: selectedChildIds.size })}
          </Text>
          <TouchableOpacity onPress={selectAllPresent} style={{ minHeight: 48, justifyContent: "center" }}>
            <Text className="text-primary-hover dark:text-primary-hover-dark font-medium">
              {t("groupView.multiSelect.selectAll")}
            </Text>
          </TouchableOpacity>
        </View>
      )}
      {tab === "timeline" && (
        <FlatList
          testID="group-timeline-list"
          style={{ backgroundColor: colors.background }}
          data={[{ key: "timeline" }]}
          keyExtractor={(i) => i.key}
          refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => load(true)} />}
          contentContainerStyle={{ padding: 16, flexGrow: 1 }}
          renderItem={() => (
            <GroupTimeline entries={timelineEntries} getPendingPhotoCount={getPendingPhotoCountForActivity} />
          )}
        />
      )}
      {tab === "children" && (
      <FlatList
        testID="group-view-list"
        style={{ backgroundColor: colors.background }}
        data={children ?? []}
        keyExtractor={(c) => c.id}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => load(true)} />}
        contentContainerStyle={{ padding: 16, flexGrow: 1 }}
        ListEmptyComponent={
          <View style={{ flex: 1, alignItems: "center", justifyContent: "center", paddingTop: 64 }}>
            <Text style={{ color: colors.textSoft }}>{t("groupView.empty")}</Text>
          </View>
        }
        renderItem={({ item }) => {
          const attendance = attendanceByChildId[item.id];
          const present = attendance?.status === "present" && !attendance.checkOutAt;
          const absent = attendance?.status === "absent";
          const selected = multiSelectMode && selectedChildIds.has(item.id);
          const selectable = multiSelectMode && present;

          return (
            // A plain View wraps the row (not a TouchableOpacity) — EventTimeline.tsx's existing
            // pattern for a row with more than one tappable region, since nesting a TouchableOpacity
            // inside another doesn't reliably scope touch handling to just the inner one.
            <View
              className={`flex-row items-center rounded-xl mb-3 ${
                selected
                  ? "bg-primary-soft dark:bg-primary-soft-dark border-2 border-primary dark:border-primary-dark"
                  : present
                    ? "bg-success-bg dark:bg-success-bg-dark border-2 border-success dark:border-success-dark"
                    : absent
                      ? "bg-surface-soft dark:bg-surface-soft-dark border-2 border-border dark:border-border-dark opacity-60"
                      : "bg-surface-soft dark:bg-surface-soft-dark"
              } ${multiSelectMode && !selectable ? "opacity-40" : ""}`}
            >
              <TouchableOpacity
                // FR-001/FR-017: a single tap toggles check-in/check-out — the primary,
                // highest-frequency action on this screen. Absence (a separate, deliberate
                // action, FR-005/FR-017) is reached via long-press, never this same tap.
                // Feature 009c (research.md R7): while multi-select mode is active, the same tap
                // toggles selection instead — check-in/out and absence-marking are suspended for
                // the duration of the mode, not a second concurrent meaning for the same tap.
                onPress={multiSelectMode ? () => selectable && toggleChildSelection(item.id) : () => handleCardPress(item)}
                onLongPress={multiSelectMode ? undefined : () => setAbsenceTarget(item)}
                disabled={multiSelectMode && !selectable}
                style={{ minHeight: 48, flex: 1 }}
                className="flex-row items-center p-4"
              >
                {item.photoDownloadUrl ? (
                  <Image
                    source={{ uri: item.photoDownloadUrl }}
                    style={{ width: 48, height: 48, borderRadius: 24, marginRight: 12 }}
                  />
                ) : (
                  <View style={{ width: 48, height: 48, borderRadius: 24, marginRight: 12, backgroundColor: colors.border }} />
                )}
                <View style={{ flex: 1 }}>
                  <Text className="text-text dark:text-text-dark font-semibold text-base">
                    {item.firstName} {item.lastName}
                  </Text>
                  <Text style={{ color: colors.textSoft }}>
                    {absent ? t("attendance.status.absent") : calculateAge(item.dateOfBirth)}
                  </Text>
                </View>
                {!!item.allergiesDescription && (
                  <View accessibilityLabel={t("child.allergyAlert")} style={{ marginLeft: 8 }}>
                    <AlertTriangle size={20} strokeWidth={2} color={colors.danger} />
                  </View>
                )}
                {/* Fever alert slot — inactive placeholder; feature 009 added temperature tracking
                    and the daily-summary query this would read from, but wiring a per-child "fever
                    today" badge into this list is a separate UI task not yet scoped. */}
                {!multiSelectMode && (
                  <View accessibilityLabel={t("child.feverAlert")} style={{ marginLeft: 4, opacity: 0.2 }}>
                    <Thermometer size={20} strokeWidth={2} color={colors.textSoft} />
                  </View>
                )}
                {multiSelectMode && selectable && (
                  <View
                    accessibilityLabel={selected ? t("groupView.multiSelect.selectAll") : undefined}
                    style={{
                      width: 24, height: 24, borderRadius: 12, marginLeft: 8,
                      alignItems: "center", justifyContent: "center",
                      backgroundColor: selected ? colors.primary : "transparent",
                      borderWidth: selected ? 0 : 2, borderColor: colors.border,
                    }}
                  >
                    {selected && <Check size={16} strokeWidth={2.5} color="white" />}
                  </View>
                )}
              </TouchableOpacity>
              {/* Secondary affordance — event-logging/timeline navigation moved here since the
                  card's own tap is now check-in/out (FR-001/FR-017). Hidden during multi-select
                  mode, which repurposes the card's tap target for selection instead. */}
              {!multiSelectMode && (
                <TouchableOpacity
                  onPress={() => router.push(`/(app)/child/${item.id}`)}
                  accessibilityLabel={t("groupView.viewDetail")}
                  style={{ minWidth: 48, minHeight: 48, alignItems: "center", justifyContent: "center", marginRight: 8 }}
                >
                  <ChevronRight size={20} strokeWidth={2} color={colors.textSoft} />
                </TouchableOpacity>
              )}
            </View>
          );
        }}
      />
      )}
      {multiSelectMode ? (
        selectedChildIds.size > 0 && (
          <TouchableOpacity
            onPress={() => setBatchSheetVisible(true)}
            style={{ position: "absolute", left: 16, right: 16, bottom: 16, minHeight: 56 }}
            className="flex-row items-center justify-center rounded-xl bg-primary dark:bg-primary-dark active:opacity-80"
          >
            <Text className="text-white font-semibold text-base">
              {t("groupView.multiSelect.logEvent", { count: selectedChildIds.size })}
            </Text>
          </TouchableOpacity>
        )
      ) : (
        <TouchableOpacity
          onPress={() => setAddActivityVisible(true)}
          accessibilityLabel={t("groupActivities.addTitle")}
          style={{ position: "absolute", right: 16, bottom: 16, minWidth: 56, minHeight: 56 }}
          className="items-center justify-center rounded-full bg-primary dark:bg-primary-dark active:opacity-80"
        >
          <Plus size={24} strokeWidth={2.5} color="white" />
        </TouchableOpacity>
      )}
      <QuickActionSheet
        visible={batchSheetVisible}
        childId=""
        batchChildren={(children ?? []).filter((c) => selectedChildIds.has(c.id))}
        inProgressSleepEventId={null}
        onClose={() => setBatchSheetVisible(false)}
        onEventRecorded={() => {}}
        onBatchRecorded={handleBatchRecorded}
      />
      <AbsenceDialog
        child={absenceTarget}
        isConnected={isConnected}
        onClose={() => setAbsenceTarget(null)}
        onSaved={handleAbsenceSaved}
      />
      <AddGroupActivitySheet
        visible={addActivityVisible}
        onClose={() => setAddActivityVisible(false)}
        onActivityRecorded={(activity) => {
          setTimelineEntries((prev) => [
            ...prev,
            { kind: "group_activity", occurredAt: activity.occurredAt, childEvent: null, groupActivity: activity },
          ]);
          setTab("timeline");
        }}
      />
    </View>
  );
}
