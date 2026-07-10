import React, { useEffect, useState } from "react";
import { View, Text, ScrollView, ActivityIndicator } from "react-native";
import { useLocalSearchParams } from "expo-router";
import { useTranslation } from "react-i18next";
import dayjs from "dayjs";
import { Megaphone } from "lucide-react-native";
import { apiClient } from "../../../services/apiClient";
import { useColors } from "../../../hooks/useColors";
import type { ParentAnnouncementResponse } from "../../../types";

// FR-009: read-only by omission — there is no reply endpoint, and this screen renders no
// compose/reply UI of any kind (unlike app/(app)/messages/[id].tsx).
export default function AnnouncementScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const { t } = useTranslation();
  const colors = useColors();

  const [announcement, setAnnouncement] = useState<ParentAnnouncementResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    async function load() {
      setError("");
      try {
        const result = await apiClient.GET("/api/parent/announcements/{id}", { params: { path: { id } } });
        if (!result.response.ok) throw new Error("failed");
        setAnnouncement(result.data as unknown as ParentAnnouncementResponse);
      } catch {
        setError(t("announcements.loadFailed"));
      } finally {
        setLoading(false);
      }
    }
    load();
    // `t` deliberately excluded — see app/(app)/index.tsx's comment for why depending on a
    // react-i18next value here would loop under a naive test mock that doesn't memoize it.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  if (loading) {
    return (
      <View style={{ flex: 1, backgroundColor: colors.background, alignItems: "center", justifyContent: "center" }}>
        <ActivityIndicator size="large" color={colors.primary} />
      </View>
    );
  }

  if (error || !announcement) {
    return (
      <View className="flex-1 bg-background dark:bg-background-dark items-center justify-center px-6">
        <Text className="text-danger dark:text-danger-dark text-sm text-center">{error}</Text>
      </View>
    );
  }

  return (
    <ScrollView className="flex-1 bg-background dark:bg-background-dark" contentContainerStyle={{ padding: 16 }}>
      <View className="flex-row items-center mb-3">
        <Megaphone color={colors.accentDirector} size={24} strokeWidth={2} />
        <Text className="text-text dark:text-text-dark text-xl font-bold ml-2 flex-1">{announcement.subject}</Text>
      </View>
      <Text className="text-text-soft dark:text-text-soft-dark text-xs mb-4">
        {dayjs(announcement.sentAt).format("MMM D, YYYY HH:mm")}
      </Text>
      <Text className="text-text dark:text-text-dark text-base leading-6 mb-6">{announcement.body}</Text>
      <Text className="text-text-soft dark:text-text-soft-dark text-xs italic">{t("announcements.readOnly")}</Text>
    </ScrollView>
  );
}
