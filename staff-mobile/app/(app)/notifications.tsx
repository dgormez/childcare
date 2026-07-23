import React, { useCallback, useEffect, useState } from "react";
import { View, Text, FlatList, TouchableOpacity, RefreshControl, ActivityIndicator } from "react-native";
import { useRouter } from "expo-router";
import { useTranslation } from "react-i18next";
import dayjs from "dayjs";
import { CalendarCheck, RefreshCw, ClipboardCheck } from "lucide-react-native";
import { apiClient } from "../../services/apiClient";
import { useColors } from "../../hooks/useColors";
import { ScreenContainer } from "../../components/ScreenContainer";
import type { NotificationResponse, NotificationType } from "../../types";

// research.md R6: a fresh, narrower union than parent-mobile/app/(app)/notifications.tsx's own
// ICON_BY_TYPE — only the three notification types a staff member can actually receive.
const ICON_BY_TYPE: Record<NotificationType, typeof CalendarCheck> = {
  schedulepublished: CalendarCheck,
  assignmentchanged: RefreshCw,
  leaverequestdecided: ClipboardCheck,
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
      const result = await apiClient.GET("/api/staff/notifications");
      if (!result.response.ok) throw new Error("failed");
      setNotifications(result.data as unknown as NotificationResponse[]);
    } catch {
      setError(t("notifications.loadFailed"));
    }
  }, [t]);

  useEffect(() => {
    setLoading(true);
    load().finally(() => setLoading(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const onRefresh = async () => {
    setRefreshing(true);
    await load();
    setRefreshing(false);
  };

  const handlePress = async (notification: NotificationResponse) => {
    // Optimistic — marking one notification read must never affect another's read state,
    // mirrors parent-mobile/app/(app)/notifications.tsx's identical precedent.
    setNotifications((prev) => prev.map((n) => (n.id === notification.id ? { ...n, readAt: n.readAt ?? new Date().toISOString() } : n)));
    apiClient.POST("/api/staff/notifications/{id}/read", { params: { path: { id: notification.id } } }).catch(() => {});

    if (notification.type === "schedulepublished" || notification.type === "assignmentchanged") {
      router.push("/(app)/schedule");
    } else if (notification.type === "leaverequestdecided") {
      router.push("/(app)/leave-requests");
    }
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
                    <Icon color={colors.text} size={20} strokeWidth={2} />
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
