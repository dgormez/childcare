import React, { useState } from "react";
import {
  View, Text, TextInput, TouchableOpacity,
  KeyboardAvoidingView, ScrollView, ActivityIndicator,
} from "react-native";
import { useRouter } from "expo-router";
import { useTranslation } from "react-i18next";
import { login } from "../../services/auth";
import { ThemedModal } from "../../components/ThemedModal";
import { useColors } from "../../hooks/useColors";
import { useBreakpoint } from "../../hooks/useBreakpoint";

const API_BASE_URL = process.env.EXPO_PUBLIC_API_BASE_URL ?? "";

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

  const handleLogin = async () => {
    setError("");
    setLoading(true);
    try {
      await login(API_BASE_URL, organisationSlug.trim(), email.trim(), password);
      router.replace("/(app)");
    } catch (e: unknown) {
      const errorKey = (e as Error).message;
      if (errorKey === "NETWORK_ERROR") {
        setError(t("login.offlineFirstLogin"));
      } else {
        setError(t(`errors.${errorKey.replace("errors.", "")}`, { defaultValue: t("errors.auth.invalid_credentials") }));
      }
    } finally {
      setLoading(false);
    }
  };

  const canSubmit =
    organisationSlug.trim().length > 0 && email.trim().length > 0 && password.length > 0 && !loading;

  return (
    <KeyboardAvoidingView behavior="padding" className="flex-1 bg-white dark:bg-gray-900">
      <ScrollView
        contentContainerStyle={{ flexGrow: 1 }}
        keyboardShouldPersistTaps="handled"
      >
        <View className="flex-1 px-6 justify-center pb-10" style={isWide ? { alignSelf: "center", width: "100%", maxWidth: 480 } : undefined}>

          <Text className="text-gray-900 dark:text-white text-3xl font-bold text-center mb-10">
            {t("login.title")}
          </Text>

          <ThemedModal
            config={error ? {
              title:   t("login.title"),
              message: error,
              buttons: [{ label: "OK", style: "default", onPress: () => setError("") }],
            } : null}
            onDismiss={() => setError("")}
          />

          <Text className="text-gray-700 dark:text-gray-300 text-sm font-medium mb-1">{t("login.organisationSlug")}</Text>
          <TextInput
            value={organisationSlug}
            onChangeText={setOrganisationSlug}
            placeholder="acme-kdv"
            placeholderTextColor={colors.placeholder}
            autoCapitalize="none"
            returnKeyType="next"
            className="bg-gray-100 dark:bg-gray-700 text-gray-900 dark:text-white rounded-xl px-4 py-4 mb-4"
          />

          <Text className="text-gray-700 dark:text-gray-300 text-sm font-medium mb-1">{t("login.email")}</Text>
          <TextInput
            value={email}
            onChangeText={setEmail}
            placeholder="you@example.com"
            placeholderTextColor={colors.placeholder}
            autoCapitalize="none"
            keyboardType="email-address"
            returnKeyType="next"
            className="bg-gray-100 dark:bg-gray-700 text-gray-900 dark:text-white rounded-xl px-4 py-4 mb-4"
          />

          <Text className="text-gray-700 dark:text-gray-300 text-sm font-medium mb-1">{t("login.password")}</Text>
          <TextInput
            value={password}
            onChangeText={setPassword}
            placeholder="••••••••"
            placeholderTextColor={colors.placeholder}
            secureTextEntry
            returnKeyType="done"
            onSubmitEditing={handleLogin}
            className="bg-gray-100 dark:bg-gray-700 text-gray-900 dark:text-white rounded-xl px-4 py-4 mb-6"
          />

          <TouchableOpacity
            onPress={handleLogin}
            disabled={!canSubmit}
            className={`rounded-2xl py-5 items-center ${canSubmit ? "bg-blue-600" : "bg-gray-300 dark:bg-gray-600"}`}
          >
            {loading
              ? <ActivityIndicator color="#fff" />
              : <Text className="text-white text-lg font-bold">{t("login.submit")}</Text>
            }
          </TouchableOpacity>

        </View>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}
