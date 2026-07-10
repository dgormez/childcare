import React, { useState } from "react";
import { View, Text, TouchableOpacity } from "react-native";
import { useRouter } from "expo-router";
import { useTranslation } from "react-i18next";
import { LogOut } from "lucide-react-native";
import { useColors } from "../../hooks/useColors";
import { useStore } from "../../store/useStore";
import { logout } from "../../services/auth";
import { ThemedModal } from "../../components/ThemedModal";

export default function SettingsScreen() {
  const { t } = useTranslation();
  const colors = useColors();
  const router = useRouter();
  const email = useStore((s) => s.auth?.email);
  const [confirming, setConfirming] = useState(false);

  const doLogout = async () => {
    setConfirming(false);
    await logout();
    router.replace("/(auth)/login");
  };

  return (
    <View className="flex-1 bg-background dark:bg-background-dark" style={{ padding: 16 }}>
      <ThemedModal
        config={confirming ? {
          title: t("settings.confirmSignOutTitle"),
          message: t("settings.confirmSignOutMessage"),
          buttons: [
            { label: t("common.cancel"), style: "cancel", onPress: () => setConfirming(false) },
            { label: t("settings.signOut"), style: "destructive", onPress: doLogout },
          ],
        } : null}
        onDismiss={() => setConfirming(false)}
      />

      {!!email && (
        <Text className="text-text-soft dark:text-text-soft-dark text-sm mb-6">
          {t("settings.signedInAs", { email })}
        </Text>
      )}

      <TouchableOpacity
        onPress={() => setConfirming(true)}
        className="flex-row items-center bg-surface dark:bg-surface-dark rounded-lg px-4"
        style={{ minHeight: 56 }}
      >
        <LogOut color={colors.danger} size={20} strokeWidth={2} />
        <Text className="text-danger dark:text-danger-dark text-base font-semibold ml-3">{t("settings.signOut")}</Text>
      </TouchableOpacity>
    </View>
  );
}
