import React from "react";
import { Stack } from "expo-router";
import { useTranslation } from "react-i18next";
import { useColors } from "../../../hooks/useColors";

export default function AnnouncementsLayout() {
  const { t } = useTranslation();
  const colors = useColors();

  return (
    <Stack screenOptions={{ headerStyle: { backgroundColor: colors.surface }, headerTintColor: colors.text }}>
      <Stack.Screen name="[id]" options={{ title: t("announcements.title") }} />
    </Stack>
  );
}
