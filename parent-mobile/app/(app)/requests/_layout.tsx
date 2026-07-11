import React from "react";
import { Stack } from "expo-router";
import { useTranslation } from "react-i18next";
import { useColors } from "../../../hooks/useColors";

export default function RequestsLayout() {
  const { t } = useTranslation();
  const colors = useColors();

  return (
    <Stack screenOptions={{ headerStyle: { backgroundColor: colors.surface }, headerTintColor: colors.text }}>
      <Stack.Screen name="index" options={{ title: t("dayReservations.myRequestsTitle") }} />
      <Stack.Screen name="absence" options={{ title: t("dayReservations.absenceTitle"), presentation: "modal" }} />
      <Stack.Screen name="extra" options={{ title: t("dayReservations.extraTitle"), presentation: "modal" }} />
      <Stack.Screen name="exchange" options={{ title: t("dayReservations.exchangeTitle"), presentation: "modal" }} />
    </Stack>
  );
}
