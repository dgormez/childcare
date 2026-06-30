import React, { useState } from "react";
import {
  View, Text, TextInput, TouchableOpacity,
  KeyboardAvoidingView, ScrollView, ActivityIndicator,
} from "react-native";
import { useRouter, useLocalSearchParams } from "expo-router";
import { ThemedModal } from "../../components/ThemedModal";
import { useColors } from "../../hooks/useColors";

const API_BASE_URL = process.env.EXPO_PUBLIC_API_BASE_URL ?? "";

export default function ResetPasswordScreen() {
  const router = useRouter();
  const colors = useColors();
  const { token } = useLocalSearchParams<{ token: string }>();

  const [password,        setPassword]        = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [loading,         setLoading]         = useState(false);
  const [done,            setDone]            = useState(false);
  const [error,           setError]           = useState("");

  const handleReset = async () => {
    setError("");
    if (password !== confirmPassword) { setError("Passwords do not match.");                return; }
    if (password.length < 8)          { setError("Password must be at least 8 characters."); return; }

    setLoading(true);
    try {
      const res  = await fetch(`${API_BASE_URL}/api/auth/reset-password`, {
        method:  "POST",
        headers: { "Content-Type": "application/json" },
        body:    JSON.stringify({ token, newPassword: password }),
      });
      const body = await res.json().catch(() => ({}));
      if (!res.ok) throw new Error((body as any).error ?? "Reset failed. Please request a new link.");
      setDone(true);
    } catch (e: unknown) {
      setError((e as Error).message ?? "Something went wrong.");
    } finally {
      setLoading(false);
    }
  };

  const canSubmit = password.length >= 8 && confirmPassword.length > 0 && !loading;

  // No token in the deep-link params
  if (!token) {
    return (
      <View className="flex-1 bg-white dark:bg-gray-900 px-6 justify-center items-center">
        <Text className="text-5xl mb-4">🔗</Text>
        <Text className="text-gray-900 dark:text-white text-xl font-bold text-center mb-3">
          Invalid reset link
        </Text>
        <Text className="text-gray-500 dark:text-gray-400 text-center mb-8">
          This link is missing a token. Please request a new one.
        </Text>
        <TouchableOpacity
          onPress={() => router.replace("/(auth)/forgot-password")}
          className="bg-blue-600 rounded-2xl px-8 py-4"
        >
          <Text className="text-white font-bold">Request new link</Text>
        </TouchableOpacity>
      </View>
    );
  }

  if (done) {
    return (
      <View className="flex-1 bg-white dark:bg-gray-900 px-6 justify-center items-center">
        <Text className="text-5xl mb-5">✅</Text>
        <Text className="text-gray-900 dark:text-white text-2xl font-bold text-center mb-3">
          Password updated
        </Text>
        <Text className="text-gray-500 dark:text-gray-400 text-center mb-10 leading-6">
          Your password has been reset. You can now sign in with your new password.
        </Text>
        <TouchableOpacity
          onPress={() => router.replace("/(auth)/login")}
          className="bg-blue-600 rounded-2xl py-5 px-10 items-center"
        >
          <Text className="text-white text-base font-bold">Sign in</Text>
        </TouchableOpacity>
      </View>
    );
  }

  return (
    <KeyboardAvoidingView behavior="padding" className="flex-1 bg-white dark:bg-gray-900">
      <ScrollView
        contentContainerStyle={{ flexGrow: 1 }}
        keyboardShouldPersistTaps="handled"
      >
        <View className="flex-1 px-6 justify-center pb-10">

          <ThemedModal
            config={error ? {
              title:   "Error",
              message: error,
              buttons: [{ label: "OK", style: "default", onPress: () => setError("") }],
            } : null}
            onDismiss={() => setError("")}
          />

          <Text className="text-gray-900 dark:text-white text-3xl font-bold mb-2">
            Set new password
          </Text>
          <Text className="text-gray-500 dark:text-gray-400 text-base mb-8 leading-6">
            Choose a strong password for your account.
          </Text>

          <Text className="text-gray-700 dark:text-gray-300 text-sm font-medium mb-1">New password</Text>
          <TextInput
            value={password}
            onChangeText={setPassword}
            placeholder="Min 8 characters"
            placeholderTextColor={colors.placeholder}
            secureTextEntry
            returnKeyType="next"
            className="bg-gray-100 dark:bg-gray-700 text-gray-900 dark:text-white rounded-xl px-4 py-4 mb-4"
          />

          <Text className="text-gray-700 dark:text-gray-300 text-sm font-medium mb-1">Confirm password</Text>
          <TextInput
            value={confirmPassword}
            onChangeText={setConfirmPassword}
            placeholder="••••••••"
            placeholderTextColor={colors.placeholder}
            secureTextEntry
            returnKeyType="done"
            onSubmitEditing={handleReset}
            className="bg-gray-100 dark:bg-gray-700 text-gray-900 dark:text-white rounded-xl px-4 py-4 mb-6"
          />

          <TouchableOpacity
            onPress={handleReset}
            disabled={!canSubmit}
            className={`rounded-2xl py-5 items-center ${canSubmit ? "bg-blue-600" : "bg-gray-300 dark:bg-gray-600"}`}
          >
            {loading
              ? <ActivityIndicator color="#fff" />
              : <Text className="text-white text-lg font-bold">Reset password</Text>
            }
          </TouchableOpacity>

        </View>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}
