import React from "react";
import { Stack, useRouter } from "expo-router";
import { View, Text, TouchableOpacity } from "react-native";
import { useTranslation } from "react-i18next";
import { useNetworkStatus } from "../../hooks/useNetworkStatus";
import { useColors } from "../../hooks/useColors";
import { logout } from "../../services/auth";

export default function AppLayout() {
  const { t } = useTranslation();
  const colors = useColors();
  const { isConnected } = useNetworkStatus();
  const router = useRouter();

  const handleLogout = async () => {
    await logout();
    router.replace("/(auth)/login");
  };

  return (
    <View style={{ flex: 1 }}>
      {!isConnected && (
        <View className="bg-yellow-500" style={{ minHeight: 32, justifyContent: "center", paddingHorizontal: 16 }}>
          <Text className="text-white text-center font-medium">{t("offline.banner")}</Text>
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
