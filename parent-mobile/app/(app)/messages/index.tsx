import React, { useCallback, useEffect, useState } from "react";
import { View, Text, FlatList, TouchableOpacity, RefreshControl, ActivityIndicator } from "react-native";
import { useRouter } from "expo-router";
import { useTranslation } from "react-i18next";
import dayjs from "dayjs";
import { Plus } from "lucide-react-native";
import { apiClient } from "../../../services/apiClient";
import { useColors } from "../../../hooks/useColors";
import { ScreenContainer } from "../../../components/ScreenContainer";
import type { MessageThreadSummaryResponse } from "../../../types";

export default function MessageThreadListScreen() {
  const { t } = useTranslation();
  const colors = useColors();
  const router = useRouter();

  const [threads, setThreads] = useState<MessageThreadSummaryResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState("");

  const load = useCallback(async () => {
    setError("");
    try {
      const result = await apiClient.GET("/api/parent/message-threads");
      if (!result.response.ok) throw new Error("failed");
      setThreads(result.data as unknown as MessageThreadSummaryResponse[]);
    } catch {
      setError(t("messages.loadFailed"));
    }
  }, [t]);

  useEffect(() => {
    setLoading(true);
    load().finally(() => setLoading(false));
    // Deliberately mount-only — see app/(app)/index.tsx's identical comment for why depending
    // on `load` (whose identity tracks `t`) would loop under a naive react-i18next test mock.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const onRefresh = async () => {
    setRefreshing(true);
    await load();
    setRefreshing(false);
  };

  if (loading) {
    return (
      <View style={{ flex: 1, backgroundColor: colors.background, alignItems: "center", justifyContent: "center" }}>
        <ActivityIndicator size="large" color={colors.primary} />
      </View>
    );
  }

  return (
    <ScreenContainer>
      <View className="flex-1 bg-background dark:bg-background-dark">
        <TouchableOpacity
          onPress={() => router.push("/(app)/messages/new")}
          className="flex-row items-center bg-primary dark:bg-primary-dark mx-4 mt-4 rounded-lg"
          style={{ minHeight: 48, paddingHorizontal: 16, justifyContent: "center" }}
        >
          <Plus color="#fff" size={20} strokeWidth={2} />
          <Text className="text-white font-semibold ml-2">{t("messages.newThread")}</Text>
        </TouchableOpacity>

        {!!error && <Text className="text-danger dark:text-danger-dark text-sm mx-4 mt-4">{error}</Text>}

        {threads.length === 0 && !error && (
          <View className="items-center" style={{ paddingVertical: 48 }}>
            <Text className="text-text-soft dark:text-text-soft-dark text-sm">{t("messages.empty")}</Text>
          </View>
        )}

        <FlatList
          data={threads}
          keyExtractor={(item) => item.id}
          contentContainerStyle={{ padding: 16 }}
          refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={colors.primary} />}
          renderItem={({ item }) => (
            <TouchableOpacity
              onPress={() => router.push(`/(app)/messages/${item.id}`)}
              className="bg-surface dark:bg-surface-dark rounded-xl px-4 mb-3"
              style={{ minHeight: 56, justifyContent: "center" }}
            >
              <View className="flex-row items-center justify-between">
                <View className="flex-row items-center flex-1">
                  {item.hasUnread && (
                    <View
                      className="bg-primary dark:bg-primary-dark"
                      style={{ width: 8, height: 8, borderRadius: 4, marginRight: 8 }}
                      testID="unread-dot"
                    />
                  )}
                  <Text
                    className={`text-text dark:text-text-dark text-base flex-1 ${item.hasUnread ? "font-bold" : "font-normal"}`}
                    numberOfLines={1}
                  >
                    {item.childName ? `${item.childName} — ${item.subject}` : item.subject}
                  </Text>
                </View>
                <Text className="text-text-soft dark:text-text-soft-dark text-xs ml-2">
                  {dayjs(item.lastActivityAt).format("MMM D")}
                </Text>
              </View>
            </TouchableOpacity>
          )}
        />
      </View>
    </ScreenContainer>
  );
}
