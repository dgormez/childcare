import "../global.css";
import "../i18n";
import * as Sentry from "@sentry/react-native";
import React, { useEffect, useState } from "react";
import { Stack, Redirect, usePathname } from "expo-router";
import { StatusBar } from "expo-status-bar";
import { View, ActivityIndicator } from "react-native";
import { useColorScheme } from "nativewind";
import Toast from "react-native-toast-message";

import { initDb } from "../services/localDb";
import { configureApiBaseUrl } from "../services/apiClient";
import { tryRestoreSession } from "../services/auth";
import { useStore } from "../store/useStore";
import { useColors } from "../hooks/useColors";

// ── Sentry crash reporting ────────────────────────────────────────────────────
// 1. Create a free project at https://sentry.io → React Native
// 2. Copy the DSN into your .env as EXPO_PUBLIC_SENTRY_DSN
// 3. Replace org/project slugs in app.config.js plugins
Sentry.init({
  dsn:               process.env.EXPO_PUBLIC_SENTRY_DSN ?? "",
  enabled:           !__DEV__,
  tracesSampleRate:  0.2,
  debug:             false,
});

const API_BASE_URL = process.env.EXPO_PUBLIC_API_BASE_URL ?? "";

// ── Root layout ───────────────────────────────────────────────────────────────

function RootLayout() {
  const { colorScheme } = useColorScheme();
  const [isReady, setIsReady] = useState(false);

  const { auth } = useStore();
  const pathname = usePathname();
  const colors = useColors();

  // One-time bootstrap: init local db, configure the API client, restore session.
  useEffect(() => {
    async function bootstrap() {
      initDb();
      configureApiBaseUrl(API_BASE_URL);
      await tryRestoreSession(API_BASE_URL);
      setIsReady(true);
    }
    bootstrap().catch(console.error);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  if (!isReady) {
    return (
      <View style={{ flex: 1, backgroundColor: colors.header, alignItems: "center", justifyContent: "center" }}>
        <ActivityIndicator size="large" color="#3b82f6" />
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

      {/* Routing: no session → login; a caregiver account is always director-provisioned,
          so there is no self-serve onboarding flow to redirect to. */}
      {(() => {
        const inAuthFlow = pathname.startsWith("/login");
        if (inAuthFlow) return null;
        if (!auth) return <Redirect href="/(auth)/login" />;
        return null;
      })()}

      <Toast />
    </>
  );
}

export default Sentry.wrap(RootLayout);
