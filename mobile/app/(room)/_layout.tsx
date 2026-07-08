import React, { useState } from "react";
import { Stack, useRouter } from "expo-router";
import { View, Text, TouchableOpacity } from "react-native";
import { useTranslation } from "react-i18next";
import { useStore } from "../../store/useStore";
import { useNetworkStatus } from "../../hooks/useNetworkStatus";
import { useColors } from "../../hooks/useColors";
import { exitRoomMode } from "../../services/deviceAuth";
import { PinKeypad } from "../../components/PinKeypad";
import { ThemedModal } from "../../components/ThemedModal";

/**
 * The kiosk shell (feature 008a) — replaces (app)/_layout.tsx as the daily entry point for a
 * paired tablet. No per-caregiver logout — the device token is the tablet's permanent identity;
 * the only way out is the director-override PIN (FR-005), reachable but not accidental.
 */
export default function RoomLayout() {
  const { t } = useTranslation();
  const colors = useColors();
  const router = useRouter();
  const { device } = useStore();
  const { isConnected } = useNetworkStatus();
  const [showExit, setShowExit] = useState(false);
  const [exitError, setExitError] = useState("");

  return (
    <View style={{ flex: 1 }}>
      {!isConnected && (
        <View className="bg-warning dark:bg-warning-dark" style={{ minHeight: 32, justifyContent: "center", paddingHorizontal: 16 }}>
          <Text className="text-warning-fg dark:text-warning-fg-dark text-center font-medium">{t("offline.banner")}</Text>
        </View>
      )}

      <ThemedModal
        config={exitError ? {
          title: t("roomHome.exitRoomMode"),
          message: exitError,
          buttons: [{ label: "OK", style: "default", onPress: () => setExitError("") }],
        } : null}
        onDismiss={() => setExitError("")}
      />

      <Stack
        screenOptions={{
          headerStyle: { backgroundColor: colors.surface },
          headerTintColor: colors.text,
          headerTitle: device ? `${device.locationName} · ${device.groupName}` : "",
          headerRight: () => (
            <TouchableOpacity
              onPress={() => setShowExit(true)}
              style={{ minWidth: 48, minHeight: 48, alignItems: "center", justifyContent: "center" }}
            >
              <Text style={{ color: colors.textSoft, fontSize: 12 }}>{t("roomHome.exitRoomMode")}</Text>
            </TouchableOpacity>
          ),
        }}
      >
        <Stack.Screen name="index" options={{ title: "" }} />
      </Stack>

      {showExit && (
        <View style={{ position: "absolute", top: 0, left: 0, right: 0, bottom: 0 }}>
          <PinKeypad
            name={null}
            pinLength={6}
            onCancel={() => setShowExit(false)}
            onSuccess={() => {
              setShowExit(false);
              router.replace("/(auth)/login");
            }}
            onSubmit={async (pin) => {
              try {
                await exitRoomMode(pin);
                return { ok: true };
              } catch (e: unknown) {
                const errorKey = (e as Error).message;
                return { ok: false, errorKey };
              }
            }}
          />
        </View>
      )}
    </View>
  );
}
