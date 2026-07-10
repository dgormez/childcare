import React, { useEffect, useState } from "react";
import {
  View, Text, TextInput, TouchableOpacity,
  KeyboardAvoidingView, ScrollView, ActivityIndicator, Platform,
} from "react-native";
import { useRouter } from "expo-router";
import { useTranslation } from "react-i18next";
import Constants from "expo-constants";
import * as WebBrowser from "expo-web-browser";
import * as Google from "expo-auth-session/providers/google";
import * as AppleAuthentication from "expo-apple-authentication";
import { login, loginWithApple, loginWithGoogle } from "../../services/auth";
import { ThemedModal } from "../../components/ThemedModal";
import { useColors } from "../../hooks/useColors";
import { useBreakpoint } from "../../hooks/useBreakpoint";

const API_BASE_URL = process.env.EXPO_PUBLIC_API_BASE_URL ?? "";

const GOOGLE_IOS_CLIENT_ID = (Constants.expoConfig?.extra?.googleIosClientId as string) ?? "";
const GOOGLE_WEB_CLIENT_ID = (Constants.expoConfig?.extra?.googleWebClientId as string) ?? "";
// The constitution's parent-app auth stack requires password + Google + Apple, but real OAuth
// credentials don't exist in this dev environment — these placeholders (same convention as
// mobile/app.config.js's "YOUR_EAS_PROJECT_ID") let the button render, correctly wired to the
// real backend contract, but disable itself rather than attempt a request that can never
// succeed against a fake client id.
const GOOGLE_CONFIGURED = GOOGLE_IOS_CLIENT_ID !== "YOUR_GOOGLE_IOS_CLIENT_ID"
  && GOOGLE_WEB_CLIENT_ID !== "YOUR_GOOGLE_WEB_CLIENT_ID";

// Required once per app lifetime so the OAuth browser popup can close itself on completion.
WebBrowser.maybeCompleteAuthSession();

export default function LoginScreen() {
  const router = useRouter();
  const colors = useColors();
  const { isWide } = useBreakpoint();
  const { t } = useTranslation();

  const [organisationSlug, setOrganisationSlug] = useState("");
  const [email,            setEmail]            = useState("");
  const [password,         setPassword]         = useState("");
  const [loading,          setLoading]          = useState(false);
  const [error,            setError]            = useState("");
  const [showPassword,     setShowPassword]     = useState(false);
  const [appleAvailable,   setAppleAvailable]   = useState(false);

  useEffect(() => {
    if (Platform.OS === "ios") {
      AppleAuthentication.isAvailableAsync().then(setAppleAvailable).catch(() => setAppleAvailable(false));
    }
  }, []);

  const [, googleResponse, promptGoogleSignIn] = Google.useAuthRequest({
    iosClientId: GOOGLE_IOS_CLIENT_ID,
    webClientId: GOOGLE_WEB_CLIENT_ID,
  });

  useEffect(() => {
    if (googleResponse?.type !== "success") return;
    const idToken = googleResponse.authentication?.idToken
      ?? (googleResponse.params as { id_token?: string } | undefined)?.id_token;
    if (idToken) handleGoogleSignIn(idToken);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [googleResponse]);

  const reportError = (e: unknown) => {
    const errorKey = (e as Error).message;
    if (errorKey === "NETWORK_ERROR") {
      setError(t("login.offlineFirstLogin"));
    } else {
      setError(t(`errors.${errorKey.replace("errors.", "")}`, { defaultValue: t("errors.auth.invalid_credentials") }));
    }
  };

  const handleLogin = async () => {
    setError("");
    setLoading(true);
    try {
      await login(API_BASE_URL, organisationSlug.trim(), email.trim(), password);
      router.replace("/(app)");
    } catch (e: unknown) {
      reportError(e);
    } finally {
      setLoading(false);
    }
  };

  const handleGoogleSignIn = async (idToken: string) => {
    setError("");
    setLoading(true);
    try {
      await loginWithGoogle(API_BASE_URL, organisationSlug.trim(), idToken);
      router.replace("/(app)");
    } catch (e: unknown) {
      reportError(e);
    } finally {
      setLoading(false);
    }
  };

  const handleAppleSignIn = async () => {
    setError("");
    try {
      const credential = await AppleAuthentication.signInAsync({
        requestedScopes: [
          AppleAuthentication.AppleAuthenticationScope.FULL_NAME,
          AppleAuthentication.AppleAuthenticationScope.EMAIL,
        ],
      });
      if (!credential.identityToken) return;
      setLoading(true);
      await loginWithApple(API_BASE_URL, organisationSlug.trim(), credential.identityToken, credential.email ?? undefined);
      router.replace("/(app)");
    } catch (e: unknown) {
      if ((e as { code?: string }).code === "ERR_REQUEST_CANCELED") return;
      reportError(e);
    } finally {
      setLoading(false);
    }
  };

  const canSubmit =
    organisationSlug.trim().length > 0 && email.trim().length > 0 && password.length > 0 && !loading;
  const canUseOAuth = organisationSlug.trim().length > 0 && !loading;

  return (
    <KeyboardAvoidingView behavior="padding" className="flex-1 bg-background dark:bg-background-dark">
      <ScrollView
        contentContainerStyle={{ flexGrow: 1 }}
        keyboardShouldPersistTaps="handled"
      >
        <View className="flex-1 px-6 justify-center pb-8" style={isWide ? { alignSelf: "center", width: "100%", maxWidth: 480 } : undefined}>

          <Text className="text-text dark:text-text-dark text-3xl font-bold text-center mb-8">
            {t("login.title")}
          </Text>

          <ThemedModal
            config={error ? {
              title:   t("login.title"),
              message: error,
              buttons: [{ label: t("common.ok"), style: "default", onPress: () => setError("") }],
            } : null}
            onDismiss={() => setError("")}
          />

          <Text className="text-text-soft dark:text-text-soft-dark text-sm font-medium mb-1">{t("login.organisationSlug")}</Text>
          <TextInput
            value={organisationSlug}
            onChangeText={setOrganisationSlug}
            placeholder="acme-kdv"
            placeholderTextColor={colors.placeholder}
            autoCapitalize="none"
            returnKeyType="next"
            className="bg-surface-soft dark:bg-surface-soft-dark text-text dark:text-text-dark rounded-lg px-4 py-4 mb-4"
          />

          <Text className="text-text-soft dark:text-text-soft-dark text-sm font-medium mb-1">{t("login.email")}</Text>
          <TextInput
            value={email}
            onChangeText={setEmail}
            placeholder="you@example.com"
            placeholderTextColor={colors.placeholder}
            autoCapitalize="none"
            keyboardType="email-address"
            returnKeyType="next"
            className="bg-surface-soft dark:bg-surface-soft-dark text-text dark:text-text-dark rounded-lg px-4 py-4 mb-4"
          />

          <Text className="text-text-soft dark:text-text-soft-dark text-sm font-medium mb-1">{t("login.password")}</Text>
          <View className="flex-row items-center bg-surface-soft dark:bg-surface-soft-dark rounded-lg mb-6">
            <TextInput
              value={password}
              onChangeText={setPassword}
              placeholder="••••••••"
              placeholderTextColor={colors.placeholder}
              secureTextEntry={!showPassword}
              returnKeyType="done"
              onSubmitEditing={handleLogin}
              className="flex-1 text-text dark:text-text-dark px-4 py-4"
            />
            <TouchableOpacity
              onPress={() => setShowPassword((v) => !v)}
              style={{ minWidth: 48, minHeight: 48, alignItems: "center", justifyContent: "center" }}
            >
              <Text className="text-text-soft dark:text-text-soft-dark text-sm font-medium">
                {showPassword ? t("login.hidePassword") : t("login.showPassword")}
              </Text>
            </TouchableOpacity>
          </View>

          <TouchableOpacity
            onPress={handleLogin}
            disabled={!canSubmit}
            className={`rounded-lg py-4 items-center ${canSubmit ? "bg-primary dark:bg-primary-dark" : "bg-border dark:bg-border-dark"}`}
          >
            {loading
              ? <ActivityIndicator color="#fff" />
              : <Text className="text-white text-lg font-bold">{t("login.submit")}</Text>
            }
          </TouchableOpacity>

          <View className="flex-row items-center my-6">
            <View className="flex-1 h-px bg-border dark:bg-border-dark" />
            <Text className="text-text-soft dark:text-text-soft-dark text-xs mx-3">{t("login.orDivider")}</Text>
            <View className="flex-1 h-px bg-border dark:bg-border-dark" />
          </View>

          <TouchableOpacity
            onPress={() => promptGoogleSignIn()}
            disabled={!canUseOAuth || !GOOGLE_CONFIGURED}
            className="rounded-lg py-4 items-center border border-border dark:border-border-dark mb-3"
            style={{ opacity: GOOGLE_CONFIGURED ? 1 : 0.5 }}
          >
            <Text className="text-text dark:text-text-dark text-base font-semibold">{t("login.continueWithGoogle")}</Text>
          </TouchableOpacity>

          {Platform.OS === "ios" && (
            <TouchableOpacity
              onPress={handleAppleSignIn}
              disabled={!canUseOAuth || !appleAvailable}
              className="rounded-lg py-4 items-center border border-border dark:border-border-dark"
              style={{ opacity: appleAvailable ? 1 : 0.5 }}
            >
              <Text className="text-text dark:text-text-dark text-base font-semibold">{t("login.continueWithApple")}</Text>
            </TouchableOpacity>
          )}

        </View>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}
