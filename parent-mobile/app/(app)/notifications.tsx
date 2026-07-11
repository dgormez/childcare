import React, { useCallback, useEffect, useState } from "react";
import { View, Text, FlatList, TouchableOpacity, RefreshControl, ActivityIndicator } from "react-native";
import { useRouter } from "expo-router";
import { useTranslation } from "react-i18next";
import dayjs from "dayjs";
import { MessageCircle, Megaphone, Thermometer, Inbox } from "lucide-react-native";
import { apiClient } from "../../services/apiClient";
import { useColors } from "../../hooks/useColors";
import { ScreenContainer } from "../../components/ScreenContainer";
import type { NotificationResponse, NotificationType } from "../../types";

const ICON_BY_TYPE: Record<NotificationType, typeof MessageCircle> = {
  newmessage: MessageCircle,
  announcement: Megaphone,
  temperaturealert: Thermometer,
  dayreservationdecided: Inbox,
};

export default function NotificationsScreen() {
  const { t } = useTranslation();
  const colors = useColors();
  const router = useRouter();

  const [notifications, setNotifications] = useState<NotificationResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState("");

  const load = useCallback(async () => {
    setError("");
    try {
      const result = await apiClient.GET("/api/parent/notifications");
      if (!result.response.ok) throw new Error("failed");
      setNotifications(result.data as unknown as NotificationResponse[]);
    } catch {
      setError(t("notifications.loadFailed"));
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

  const handlePress = async (notification: NotificationResponse) => {
    // Optimistic — marking one notification read must never affect another's read state
    // (FR-011), so this only ever touches the tapped row's own entry.
    setNotifications((prev) => prev.map((n) => (n.id === notification.id ? { ...n, readAt: n.readAt ?? new Date().toISOString() } : n)));
    apiClient.POST("/api/parent/notifications/{id}/read", { params: { path: { id: notification.id } } }).catch(() => {});

    if (notification.type === "newmessage") {
      router.push(`/(app)/messages/${notification.sourceId}`);
    } else if (notification.type === "announcement") {
      router.push(`/(app)/announcements/${notification.sourceId}`);
    } else if (notification.type === "dayreservationdecided") {
      router.push("/(app)/requests");
    }
    // temperaturealert: no navigation target makes sense (the underlying child-event has no
    // parent-facing detail screen) — marking read is the whole interaction.
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
        {!!error && <Text className="text-danger dark:text-danger-dark text-sm mx-4 mt-4">{error}</Text>}

        {notifications.length === 0 && !error && (
          <View className="items-center" style={{ paddingVertical: 48 }}>
            <Text className="text-text-soft dark:text-text-soft-dark text-sm">{t("notifications.empty")}</Text>
          </View>
        )}

        <FlatList
          data={notifications}
          keyExtractor={(item) => item.id}
          contentContainerStyle={{ padding: 16 }}
          refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={colors.primary} />}
          renderItem={({ item }) => {
            const Icon = ICON_BY_TYPE[item.type];
            const isUnread = !item.readAt;
            const iconColor = item.type === "temperaturealert" ? colors.danger : colors.text;
            let args: Record<string, unknown> = {};
            try { args = JSON.parse(item.argumentsJson || "{}"); } catch { /* malformed payload — render title/body as-is */ }

            return (
              <TouchableOpacity
                onPress={() => handlePress(item)}
                className="bg-surface dark:bg-surface-dark rounded-xl px-4 mb-3"
                style={{ minHeight: 56, justifyContent: "center", paddingVertical: 12 }}
              >
                <View className="flex-row items-start">
                  <View style={{ width: 32, alignItems: "center", marginTop: 4 }}>
                    <Icon color={iconColor} size={20} strokeWidth={2} />
                  </View>
                  <View className="flex-1 ml-2">
                    <View className="flex-row items-center">
                      {isUnread && (
                        <View
                          className="bg-primary dark:bg-primary-dark"
                          style={{ width: 8, height: 8, borderRadius: 4, marginRight: 8 }}
                          testID="unread-dot"
                        />
                      )}
                      <Text className={`text-text dark:text-text-dark text-base flex-1 ${isUnread ? "font-bold" : "font-normal"}`}>
                        {t(item.titleKey)}
                      </Text>
                    </View>
                    <Text className="text-text-soft dark:text-text-soft-dark text-sm mt-1">
                      {t(item.bodyKey, args)}
                    </Text>
                    <Text className="text-text-soft dark:text-text-soft-dark text-xs mt-1">
                      {dayjs(item.createdAt).format("MMM D, HH:mm")}
                    </Text>
                  </View>
                </View>
              </TouchableOpacity>
            );
          }}
        />
      </View>
    </ScreenContainer>
  );
}
