import React from "react";
import { Stack } from "expo-router";
import { useTranslation } from "react-i18next";
import { useColors } from "../../../hooks/useColors";

export default function FiscalAttestationsLayout() {
  const { t } = useTranslation();
  const colors = useColors();

  return (
    <Stack screenOptions={{ headerStyle: { backgroundColor: colors.surface }, headerTintColor: colors.text }}>
      <Stack.Screen name="index" options={{ title: t("fiscalAttestations.title") }} />
    </Stack>
  );
}
