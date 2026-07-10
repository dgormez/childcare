// Deviation from tasks.md T041, which names this file `accept-invitation.tsx`: the invite email
// links to `childcareparent://parent-invitation?token=...&org=...` — hardcoded by the backend's
// ParentLinkBuilder (backend/ChildCare.Application/ParentInvitations/ParentLinkBuilder.cs) as
// the fallback when `App:ParentInviteBaseUrl` isn't configured. Expo Router derives the deep
// link's route from this file's path (route-group segments like `(auth)` are stripped), so the
// screen that actually receives the link must be named `parent-invitation.tsx` here, matching
// the backend's URL exactly — not `accept-invitation.tsx`. Do not rename this back to match
// tasks.md; that would silently break the real invite link.
import React, { useEffect, useState } from "react";
import { View, Text, TextInput, TouchableOpacity, KeyboardAvoidingView, ScrollView, ActivityIndicator, Platform } from "react-native";
import { useLocalSearchParams, useRouter } from "expo-router";
import { useTranslation } from "react-i18next";
import Constants from "expo-constants";
import * as WebBrowser from "expo-web-browser";
import * as Google from "expo-auth-session/providers/google";
import * as AppleAuthentication from "expo-apple-authentication";
import { apiClient } from "../../services/apiClient";
import { loginWithApple, loginWithGoogle } from "../../services/auth";
import { ThemedModal } from "../../components/ThemedModal";
import { useColors } from "../../hooks/useColors";

const API_BASE_URL = process.env.EXPO_PUBLIC_API_BASE_URL ?? "";

const GOOGLE_IOS_CLIENT_ID = (Constants.expoConfig?.extra?.googleIosClientId as string) ?? "";
const GOOGLE_WEB_CLIENT_ID = (Constants.expoConfig?.extra?.googleWebClientId as string) ?? "";
// Same placeholder-detection convention as app/(auth)/login.tsx.
const GOOGLE_CONFIGURED = GOOGLE_IOS_CLIENT_ID !== "YOUR_GOOGLE_IOS_CLIENT_ID"
  && GOOGLE_WEB_CLIENT_ID !== "YOUR_GOOGLE_WEB_CLIENT_ID";

WebBrowser.maybeCompleteAuthSession();

export default function ParentInvitationScreen() {
  const router = useRouter();
  const colors = useColors();
  const { t } = useTranslation();
  const params = useLocalSearchParams<{ token?: string; org?: string }>();

  const [password,        setPassword]        = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [loading,         setLoading]         = useState(false);
  const [error,           setError]           = useState("");
  const [succeeded,       setSucceeded]       = useState(false);
  const [appleAvailable,  setAppleAvailable]  = useState(false);

  const token = typeof params.token === "string" ? params.token : "";
  const organisationSlug = typeof params.org === "string" ? params.org : "";
  const linkValid = token.length > 0 && organisationSlug.length > 0;

  useEffect(() => {
    if (Platform.OS === "ios") {
      AppleAuthentication.isAvailableAsync().then(setAppleAvailable).catch(() => setAppleAvailable(false));
    }
  }, []);

  const [, googleResponse, promptGoogleSignIn] = Google.useAuthRequest({
    iosClientId: GOOGLE_IOS_CLIENT_ID,
    webClientId: GOOGLE_WEB_CLIENT_ID,
  });

  const reportOAuthError = (e: unknown) => {
    const errorKey = (e as Error).message;
    setError(t(`errors.${errorKey.replace("errors.", "")}`, { defaultValue: t("errors.auth.invalid_credentials") }));
  };

  useEffect(() => {
    if (googleResponse?.type !== "success" || !linkValid) return;
    const idToken = googleResponse.authentication?.idToken
      ?? (googleResponse.params as { id_token?: string } | undefined)?.id_token;
    if (!idToken) return;
    setError("");
    setLoading(true);
    // Google already carries a verified email — signing in here completes registration exactly
    // like the password flow below (ParentAccountLinker, backend/ChildCare.Application/
    // ParentInvitations/ParentAccountLinker.cs), without ever needing a password.
    loginWithGoogle(API_BASE_URL, organisationSlug, idToken)
      .then(() => router.replace("/(app)"))
      .catch(reportOAuthError)
      .finally(() => setLoading(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [googleResponse]);

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
      await loginWithApple(API_BASE_URL, organisationSlug, credential.identityToken, credential.email ?? undefined);
      router.replace("/(app)");
    } catch (e: unknown) {
      if ((e as { code?: string }).code === "ERR_REQUEST_CANCELED") return;
      reportOAuthError(e);
    } finally {
      setLoading(false);
    }
  };

  const handleSubmit = async () => {
    setError("");
    if (password !== confirmPassword) {
      setError(t("parentInvitation.passwordMismatch"));
      return;
    }
    setLoading(true);
    try {
      const result = await apiClient.POST("/api/parent-invitations/accept", {
        body: { organisationSlug, token, password },
      });
      if (!result.response.ok) {
        const errorKey = (result.error as { errorKey?: string } | undefined)?.errorKey ?? "errors.invitation.not_found";
        throw new Error(errorKey);
      }
      // AcceptParentInvitationCommand issues no session on success (contracts/api.md — the
      // accept endpoint returns 200 with no body), so there is nothing to log straight into;
      // sending the parent to sign in with the password they just set is the simplest correct
      // next step, and mirrors the staff invitation-accept flow's own precedent (feature 005).
      setSucceeded(true);
    } catch (e: unknown) {
      const errorKey = (e as Error).message;
      setError(t(`errors.${errorKey.replace("errors.", "")}`, { defaultValue: t("errors.invitation.not_found") }));
    } finally {
      setLoading(false);
    }
  };

  if (!linkValid) {
    return (
      <View className="flex-1 bg-background dark:bg-background-dark items-center justify-center px-6">
        <Text className="text-text dark:text-text-dark text-base text-center">{t("parentInvitation.invalidLink")}</Text>
      </View>
    );
  }

  if (succeeded) {
    return (
      <View className="flex-1 bg-background dark:bg-background-dark items-center justify-center px-6">
        <Text className="text-text dark:text-text-dark text-lg font-semibold text-center mb-6">
          {t("parentInvitation.success")}
        </Text>
        <TouchableOpacity
          onPress={() => router.replace("/(auth)/login")}
          className="rounded-lg py-4 px-6 items-center bg-primary dark:bg-primary-dark"
        >
          <Text className="text-white text-base font-bold">{t("parentInvitation.goToLogin")}</Text>
        </TouchableOpacity>
      </View>
    );
  }

  const canSubmit = password.length > 0 && confirmPassword.length > 0 && !loading;

  return (
    <KeyboardAvoidingView behavior="padding" className="flex-1 bg-background dark:bg-background-dark">
      <ScrollView contentContainerStyle={{ flexGrow: 1 }} keyboardShouldPersistTaps="handled">
        <View className="flex-1 px-6 justify-center pb-8">
          <Text className="text-text dark:text-text-dark text-2xl font-bold text-center mb-2">
            {t("parentInvitation.title")}
          </Text>
          <Text className="text-text-soft dark:text-text-soft-dark text-sm text-center mb-8">
            {t("parentInvitation.subtitle")}
          </Text>

          <ThemedModal
            config={error ? {
              title:   t("parentInvitation.title"),
              message: error,
              buttons: [{ label: t("common.ok"), style: "default", onPress: () => setError("") }],
            } : null}
            onDismiss={() => setError("")}
          />

          <Text className="text-text-soft dark:text-text-soft-dark text-sm font-medium mb-1">{t("parentInvitation.password")}</Text>
          <TextInput
            value={password}
            onChangeText={setPassword}
            placeholder="••••••••"
            placeholderTextColor={colors.placeholder}
            secureTextEntry
            returnKeyType="next"
            className="bg-surface-soft dark:bg-surface-soft-dark text-text dark:text-text-dark rounded-lg px-4 py-4 mb-4"
          />

          <Text className="text-text-soft dark:text-text-soft-dark text-sm font-medium mb-1">{t("parentInvitation.confirmPassword")}</Text>
          <TextInput
            value={confirmPassword}
            onChangeText={setConfirmPassword}
            placeholder="••••••••"
            placeholderTextColor={colors.placeholder}
            secureTextEntry
            returnKeyType="done"
            onSubmitEditing={handleSubmit}
            className="bg-surface-soft dark:bg-surface-soft-dark text-text dark:text-text-dark rounded-lg px-4 py-4 mb-6"
          />

          <TouchableOpacity
            onPress={handleSubmit}
            disabled={!canSubmit}
            className={`rounded-lg py-4 items-center ${canSubmit ? "bg-primary dark:bg-primary-dark" : "bg-border dark:bg-border-dark"}`}
          >
            {loading
              ? <ActivityIndicator color="#fff" />
              : <Text className="text-white text-lg font-bold">{t("parentInvitation.submit")}</Text>
            }
          </TouchableOpacity>

          <View className="flex-row items-center my-6">
            <View className="flex-1 h-px bg-border dark:bg-border-dark" />
            <Text className="text-text-soft dark:text-text-soft-dark text-xs mx-3">{t("login.orDivider")}</Text>
            <View className="flex-1 h-px bg-border dark:bg-border-dark" />
          </View>

          <TouchableOpacity
            onPress={() => promptGoogleSignIn()}
            disabled={loading || !GOOGLE_CONFIGURED}
            className="rounded-lg py-4 items-center border border-border dark:border-border-dark mb-3"
            style={{ opacity: GOOGLE_CONFIGURED ? 1 : 0.5 }}
          >
            <Text className="text-text dark:text-text-dark text-base font-semibold">{t("login.continueWithGoogle")}</Text>
          </TouchableOpacity>

          {Platform.OS === "ios" && (
            <TouchableOpacity
              onPress={handleAppleSignIn}
              disabled={loading || !appleAvailable}
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
