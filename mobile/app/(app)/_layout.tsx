import React, { useEffect, useRef, useState } from "react";
import { Stack, useRouter } from "expo-router";
import { View, Text, TouchableOpacity, AppState } from "react-native";
import { useTranslation } from "react-i18next";
import { useNetworkStatus } from "../../hooks/useNetworkStatus";
import { useSyncStatus } from "../../hooks/useSyncStatus";
import { useColors } from "../../hooks/useColors";
import { logout } from "../../services/auth";
import { syncPendingQueue } from "../../services/syncEngine";
import { uploadPendingPhotos } from "../../services/photoUploadQueue";
import { ThemedModal } from "../../components/ThemedModal";

export default function AppLayout() {
  const { t } = useTranslation();
  const colors = useColors();
  const { isConnected } = useNetworkStatus();
  const { pendingCount, isSyncing } = useSyncStatus();
  const router = useRouter();
  const wasConnected = useRef(isConnected);
  const [confirmingLogout, setConfirmingLogout] = useState(false);

  const doLogout = async () => {
    setConfirmingLogout(false);
    await logout();
    router.replace("/(auth)/login");
  };

  // Edge Cases: unsent queued actions are lost on logout — make the caregiver aware first
  // when anything is still pending, rather than silently discarding it.
  const handleLogout = () => {
    if (pendingCount > 0) setConfirmingLogout(true);
    else doLogout();
  };

  // FR-012a: sync fires on exactly network-reconnect, app-foreground, and pull-to-refresh —
  // never on a timer.
  useEffect(() => {
    if (!wasConnected.current && isConnected) {
      syncPendingQueue();
      uploadPendingPhotos();
    }
    wasConnected.current = isConnected;
  }, [isConnected]);

  useEffect(() => {
    const subscription = AppState.addEventListener("change", (state) => {
      if (state === "active") {
        syncPendingQueue();
        uploadPendingPhotos();
      }
    });
    return () => subscription.remove();
  }, []);

  return (
    <View style={{ flex: 1 }}>
      <ThemedModal
        config={confirmingLogout ? {
          title:   t("logout.confirmTitle"),
          message: t("logout.confirmPendingMessage"),
          buttons: [
            { label: t("logout.cancel"), style: "cancel", onPress: () => setConfirmingLogout(false) },
            { label: t("logout.confirm"), style: "destructive", onPress: doLogout },
          ],
        } : null}
        onDismiss={() => setConfirmingLogout(false)}
      />
      {!isConnected && (
        <View className="bg-warning dark:bg-warning-dark" style={{ minHeight: 32, justifyContent: "center", paddingHorizontal: 16 }}>
          <Text className="text-warning-fg dark:text-warning-fg-dark text-center font-medium">{t("offline.banner")}</Text>
        </View>
      )}
      {(isSyncing || pendingCount > 0) && (
        <View className="bg-info dark:bg-info-dark" style={{ minHeight: 28, justifyContent: "center", paddingHorizontal: 16 }}>
          <Text className="text-white text-center text-xs font-medium">
            {isSyncing ? t("sync.syncing") : t(pendingCount === 1 ? "sync.pending" : "sync.pending_plural", { count: pendingCount })}
          </Text>
        </View>
      )}
      <Stack
        screenOptions={{
          headerStyle: { backgroundColor: colors.surface },
          headerTintColor: colors.text,
          headerRight: () => (
            <TouchableOpacity
              onPress={handleLogout}
              style={{ minWidth: 48, minHeight: 48, alignItems: "center", justifyContent: "center" }}
            >
              <Text style={{ color: colors.text }}>{t("logout.confirm")}</Text>
            </TouchableOpacity>
          ),
        }}
      >
        <Stack.Screen name="index" options={{ title: t("groupView.title") }} />
        <Stack.Screen name="child/[id]" options={{ title: t("child.medicalQuickAccess") }} />
      </Stack>
    </View>
  );
}
