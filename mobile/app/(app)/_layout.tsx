import React, { useEffect, useRef } from "react";
import { Stack, useRouter } from "expo-router";
import { View, Text, TouchableOpacity, AppState } from "react-native";
import { useTranslation } from "react-i18next";
import { useNetworkStatus } from "../../hooks/useNetworkStatus";
import { useSyncStatus } from "../../hooks/useSyncStatus";
import { useColors } from "../../hooks/useColors";
import { logout } from "../../services/auth";
import { syncPendingQueue } from "../../services/syncEngine";

export default function AppLayout() {
  const { t } = useTranslation();
  const colors = useColors();
  const { isConnected } = useNetworkStatus();
  const { pendingCount, isSyncing } = useSyncStatus();
  const router = useRouter();
  const wasConnected = useRef(isConnected);

  const handleLogout = async () => {
    await logout();
    router.replace("/(auth)/login");
  };

  // FR-012a: sync fires on exactly network-reconnect, app-foreground, and pull-to-refresh —
  // never on a timer.
  useEffect(() => {
    if (!wasConnected.current && isConnected) {
      syncPendingQueue();
    }
    wasConnected.current = isConnected;
  }, [isConnected]);

  useEffect(() => {
    const subscription = AppState.addEventListener("change", (state) => {
      if (state === "active") syncPendingQueue();
    });
    return () => subscription.remove();
  }, []);

  return (
    <View style={{ flex: 1 }}>
      {!isConnected && (
        <View className="bg-yellow-500" style={{ minHeight: 32, justifyContent: "center", paddingHorizontal: 16 }}>
          <Text className="text-white text-center font-medium">{t("offline.banner")}</Text>
        </View>
      )}
      {(isSyncing || pendingCount > 0) && (
        <View className="bg-blue-500" style={{ minHeight: 28, justifyContent: "center", paddingHorizontal: 16 }}>
          <Text className="text-white text-center text-xs font-medium">
            {isSyncing ? t("sync.syncing") : t(pendingCount === 1 ? "sync.pending" : "sync.pending_plural", { count: pendingCount })}
          </Text>
        </View>
      )}
      <Stack
        screenOptions={{
          headerStyle: { backgroundColor: colors.header },
          headerTintColor: colors.headerText,
          headerRight: () => (
            <TouchableOpacity
              onPress={handleLogout}
              style={{ minWidth: 48, minHeight: 48, alignItems: "center", justifyContent: "center" }}
            >
              <Text style={{ color: colors.headerText }}>{t("logout.confirm")}</Text>
            </TouchableOpacity>
          ),
        }}
      >
        <Stack.Screen name="index" options={{ title: t("groupView.title", { defaultValue: "Today" }) }} />
        <Stack.Screen name="child/[id]" options={{ title: t("child.medicalQuickAccess") }} />
      </Stack>
    </View>
  );
}
