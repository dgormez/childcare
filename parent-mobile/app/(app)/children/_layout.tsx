import React from "react";
import { Stack } from "expo-router";
import { useTranslation } from "react-i18next";
import { useColors } from "../../../hooks/useColors";

/** Feature 030 (US5) — the "previous children" screen's own Stack, mirroring requests/_layout.tsx. */
export default function ChildrenLayout() {
  const { t } = useTranslation();
  const colors = useColors();

  return (
    <Stack screenOptions={{ headerStyle: { backgroundColor: colors.surface }, headerTintColor: colors.text }}>
      <Stack.Screen name="previous" options={{ title: t("previousChildren.title") }} />
    </Stack>
  );
}
