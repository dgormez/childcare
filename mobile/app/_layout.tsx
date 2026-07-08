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
import { tryRestoreDeviceState } from "../services/deviceAuth";
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

  const { auth, device } = useStore();
  const pathname = usePathname();
  const colors = useColors();

  // One-time bootstrap: init local db, configure the API client, restore device-pairing state
  // (feature 008a) or fall back to a user session. A paired tablet's device token is checked
  // first and takes priority — once paired, the director's own login session is irrelevant to
  // this tablet's daily operation (spec User Story 1).
  useEffect(() => {
    async function bootstrap() {
      initDb();
      configureApiBaseUrl(API_BASE_URL);
      const paired = await tryRestoreDeviceState();
      if (!paired) await tryRestoreSession(API_BASE_URL);
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
        <Stack.Screen name="(room-setup)" options={{ headerShown: false }} />
        <Stack.Screen name="(room)" options={{ headerShown: false }} />
        <Stack.Screen name="(app)" options={{ headerShown: false }} />
      </Stack>

      {/* Routing (feature 008a): a paired tablet always belongs in (room), regardless of any
          lingering user session. Otherwise, an authenticated director (mid-pairing, just past
          login) belongs in (room-setup). No credential at all → login. (room-setup)'s and
          (room)'s own index screens both resolve to pathname "/" (route-group folders don't
          appear in the URL), so this can't distinguish between them by pathname — it redirects
          unconditionally from state instead, which is idempotent once already on the right
          screen. */}
      {(() => {
        const inAuthFlow = pathname.startsWith("/login");
        if (inAuthFlow) return null;

        if (device) return <Redirect href="/(room)" />;
        if (!auth) return <Redirect href="/(auth)/login" />;
        return <Redirect href="/(room-setup)" />;
      })()}

      <Toast />
    </>
  );
}

export default Sentry.wrap(RootLayout);
