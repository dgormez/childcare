import "../global.css";
import * as Sentry from "@sentry/react-native";
import React, { useEffect, useState } from "react";
import { Stack, Redirect, usePathname } from "expo-router";
import { StatusBar } from "expo-status-bar";
import { View, ActivityIndicator } from "react-native";
import { useColorScheme } from "nativewind";
import Toast from "react-native-toast-message";

import { initDb, getLocalHabits, getLocalCompletions, getLastSyncTime, getConfigValue, setConfigValue } from "../services/localDb";
import dayjs from "dayjs";
import { configureApi } from "../services/api";
import { tryRestoreSession } from "../services/auth";
import { registerForPushNotifications } from "../services/notifications";
import { useStore } from "../store/useStore";
import { useSync } from "../hooks/useSync";
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
  const [isReady,     setIsReady]     = useState(false);
  const [isOnboarded, setIsOnboarded] = useState(false);

  const { auth, setHabits, setCompletions, setLastSyncAt } = useStore();
  const pathname = usePathname();
  const { sync } = useSync();
  const colors = useColors();

  // One-time bootstrap: restore session, then mark ready.
  useEffect(() => {
    async function bootstrap() {
      initDb();
      configureApi(API_BASE_URL);

      const sessionRestored = await tryRestoreSession(API_BASE_URL);
      const onboarded = getConfigValue("onboardingComplete") === "true";

      // Existing users who had no onboarding should skip it
      if (sessionRestored && !onboarded) setConfigValue("onboardingComplete", "true");

      setIsOnboarded(sessionRestored || onboarded);
      setIsReady(true);
    }
    bootstrap().catch(console.error);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Load habits from SQLite + background sync whenever the logged-in user changes.
  // Runs on initial login, session restore, and after logout → login.
  useEffect(() => {
    if (!isReady || !auth) return;
    const today   = dayjs().format("YYYY-MM-DD");
    const weekAgo = dayjs().subtract(6, "day").format("YYYY-MM-DD");
    setHabits(getLocalHabits(auth.userId));
    setCompletions(getLocalCompletions(auth.userId, weekAgo, today));
    const lastSync = getLastSyncTime();
    if (lastSync) setLastSyncAt(lastSync);
    sync();
    registerForPushNotifications().catch(() => {});
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isReady, auth?.userId]);

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
        <Stack.Screen name="onboarding"   options={{ headerShown: false }} />
        <Stack.Screen name="oauthredirect" options={{ headerShown: false }} />
        <Stack.Screen name="(auth)"     options={{ headerShown: false }} />
        <Stack.Screen name="(tabs)"     options={{ headerShown: false }} />
        <Stack.Screen
          name="habit/add"
          options={{
            headerShown:  false,
            presentation: "modal",
          }}
        />
        <Stack.Screen
          name="habit/[id]"
          options={{
            headerShown:  false,
            presentation: "modal",
          }}
        />
      </Stack>

      {/* Routing: onboarding → auth → app */}
      {(() => {
        const inAuthFlow = ["/login", "/register", "/forgot-password", "/reset-password", "/verify-email", "/onboarding"].some(p => pathname.startsWith(p));
        if (inAuthFlow) return null;
        if (!auth && !isOnboarded) return <Redirect href="/onboarding" />;
        if (!auth &&  isOnboarded) return <Redirect href="/(auth)/login" />;
        return null;
      })()}

      <Toast />
    </>
  );
}

export default Sentry.wrap(RootLayout);
