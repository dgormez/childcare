import "../global.css";
import "../i18n";
import React, { useEffect, useState } from "react";
import { Stack, Redirect, usePathname } from "expo-router";
import { StatusBar } from "expo-status-bar";
import { View, ActivityIndicator } from "react-native";
import { useColorScheme } from "nativewind";
import Toast from "react-native-toast-message";

import { configureApiBaseUrl } from "../services/apiClient";
import { tryRestoreSession } from "../services/auth";
import { useStore } from "../store/useStore";
import { useColors } from "../hooks/useColors";

const API_BASE_URL = process.env.EXPO_PUBLIC_API_BASE_URL ?? "";

export default function RootLayout() {
  const { colorScheme } = useColorScheme();
  const [isReady, setIsReady] = useState(false);

  const { auth } = useStore();
  const pathname = usePathname();
  const colors = useColors();

  useEffect(() => {
    async function bootstrap() {
      configureApiBaseUrl(API_BASE_URL);
      await tryRestoreSession(API_BASE_URL);
      setIsReady(true);
    }
    bootstrap().catch(console.error);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  if (!isReady) {
    return (
      <View style={{ flex: 1, backgroundColor: colors.background, alignItems: "center", justifyContent: "center" }}>
        <ActivityIndicator size="large" color={colors.primary} />
      </View>
    );
  }

  return (
    <>
      <StatusBar style={colorScheme === "dark" ? "light" : "dark"} />
      <Stack screenOptions={{ headerShown: false }}>
        <Stack.Screen name="(auth)" options={{ headerShown: false }} />
        <Stack.Screen name="(app)" options={{ headerShown: false }} />
      </Stack>

      {/* The parent-invitation deep link (childcareparent://parent-invitation?token=...&org=...)
          must stay reachable even while unauthenticated, so it's excluded from the auth redirect
          below the same way /login is. */}
      {(() => {
        const inAuthFlow = pathname.startsWith("/login") || pathname.startsWith("/parent-invitation");
        if (!auth) return inAuthFlow ? null : <Redirect href="/(auth)/login" />;
        return inAuthFlow ? <Redirect href="/(app)" /> : null;
      })()}

      <Toast />
    </>
  );
}
