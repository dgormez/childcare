import React, { useCallback, useEffect, useState } from "react";
import { View, Text, ScrollView, TouchableOpacity } from "react-native";
import { useLocalSearchParams } from "expo-router";
import { useTranslation } from "react-i18next";
import { Plus } from "lucide-react-native";
import { getCached } from "../../../services/readCache";
import { getPending } from "../../../services/offlineQueue";
import { listChildEvents, deleteChildEvent, updateChildEvent } from "../../../services/childEvents";
import { useColors } from "../../../hooks/useColors";
import { useNetworkStatus } from "../../../hooks/useNetworkStatus";
import { QuickActionSheet } from "../../../components/QuickActionSheet";
import { EventTimeline, type EventSyncStatus } from "../../../components/EventTimeline";
import { EditEventModal } from "../../../components/EditEventModal";
import type { ChildResponse, ChildEventResponse } from "../../../types";
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
  const { t } = useTranslation();
  const colors = useColors();
  const { isConnected } = useNetworkStatus();

  const children = getCached<ChildResponse[]>(CHILDREN_CACHE_KEY) ?? [];
  const child = children.find((c) => c.id === id);

  const [events, setEvents] = useState<ChildEventResponse[]>([]);
  const [syncStatusByEventId, setSyncStatusByEventId] = useState<Record<string, EventSyncStatus>>({});
  const [sheetVisible, setSheetVisible] = useState(false);
  const [editingEvent, setEditingEvent] = useState<ChildEventResponse | null>(null);

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

        <EventTimeline
          events={events}
          syncStatusByEventId={syncStatusByEventId}
          onEdit={setEditingEvent}
          onDelete={handleDelete}
        />
      </ScrollView>

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
    </View>
  );
}
