import React, { useCallback, useEffect, useState } from "react";
import { View, Text, FlatList, TouchableOpacity, Image, RefreshControl, ActivityIndicator } from "react-native";
import { useRouter } from "expo-router";
import { useTranslation } from "react-i18next";
import { apiClient } from "../../services/apiClient";
import { getCached, setCached } from "../../services/readCache";
import { syncPendingQueue } from "../../services/syncEngine";
import { useColors } from "../../hooks/useColors";
import type { ChildResponse, GroupResponse } from "../../types";

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
  const groups = (await groupsResult.response.json()) as GroupResponse[];

  const perGroup = await Promise.all(
    groups.map(async (group) => {
      const childrenResult = await apiClient.GET("/api/children", { params: { query: { groupId: group.id } } });
      if (!childrenResult.response.ok) return [];
      return (await childrenResult.response.json()) as ChildResponse[];
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

  const [children, setChildren] = useState<ChildResponse[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);

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
  }, []);

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  if (loading) {
    return (
      <View style={{ flex: 1, alignItems: "center", justifyContent: "center", backgroundColor: colors.background }}>
        <ActivityIndicator size="large" />
      </View>
    );
  }

  return (
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
      renderItem={({ item }) => (
        <TouchableOpacity
          onPress={() => router.push(`/(app)/child/${item.id}`)}
          style={{ minHeight: 48 }}
          className="flex-row items-center bg-surface-soft dark:bg-surface-soft-dark rounded-xl p-4 mb-3"
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
            <Text style={{ color: colors.textSoft }}>{calculateAge(item.dateOfBirth)}</Text>
          </View>
          {!!item.allergiesDescription && (
            <Text accessibilityLabel={t("child.allergyAlert")} style={{ fontSize: 20, marginLeft: 8 }}>
              ⚠️
            </Text>
          )}
          {/* Fever alert slot — always inactive until a temperature/health-check feature exists. */}
          <Text accessibilityLabel={t("child.feverAlert")} style={{ fontSize: 20, marginLeft: 4, opacity: 0.2 }}>
            🌡️
          </Text>
        </TouchableOpacity>
      )}
    />
  );
}
