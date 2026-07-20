import React from "react";
import { Stack } from "expo-router";
import { useColors } from "../../../hooks/useColors";

export default function QrCheckInLayout() {
  const colors = useColors();

  return (
    <Stack screenOptions={{ headerStyle: { backgroundColor: colors.surface }, headerTintColor: colors.text }}>
      <Stack.Screen name="[childId]" options={{ title: "" }} />
    </Stack>
  );
}
