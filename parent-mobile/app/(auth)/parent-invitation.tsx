// Deviation from tasks.md T041, which names this file `accept-invitation.tsx`: the invite email
// links to `childcareparent://parent-invitation?token=...&org=...` — hardcoded by the backend's
// ParentLinkBuilder (backend/ChildCare.Application/ParentInvitations/ParentLinkBuilder.cs) as
// the fallback when `App:ParentInviteBaseUrl` isn't configured. Expo Router derives the deep
// link's route from this file's path (route-group segments like `(auth)` are stripped), so the
// screen that actually receives the link must be named `parent-invitation.tsx` here, matching
// the backend's URL exactly — not `accept-invitation.tsx`. Do not rename this back to match
// tasks.md; that would silently break the real invite link.
import React, { useState } from "react";
import { View, Text, TextInput, TouchableOpacity, KeyboardAvoidingView, ScrollView, ActivityIndicator } from "react-native";
import { useLocalSearchParams, useRouter } from "expo-router";
import { useTranslation } from "react-i18next";
import { apiClient } from "../../services/apiClient";
import { ThemedModal } from "../../components/ThemedModal";
import { useColors } from "../../hooks/useColors";

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

  const token = typeof params.token === "string" ? params.token : "";
  const organisationSlug = typeof params.org === "string" ? params.org : "";
  const linkValid = token.length > 0 && organisationSlug.length > 0;

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
        </View>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}
