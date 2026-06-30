import React, { useState } from "react";
import {
  View, Text, TextInput, TouchableOpacity,
  KeyboardAvoidingView, ScrollView, ActivityIndicator,
} from "react-native";
import { useRouter } from "expo-router";
import { ThemedModal } from "../../components/ThemedModal";
import { useColors } from "../../hooks/useColors";

const API_BASE_URL = process.env.EXPO_PUBLIC_API_BASE_URL ?? "";

export default function ForgotPasswordScreen() {
  const router = useRouter();
  const colors = useColors();

  const [email,   setEmail]   = useState("");
  const [loading, setLoading] = useState(false);
  const [sent,    setSent]    = useState(false);
  const [error,   setError]   = useState("");

  const handleSubmit = async () => {
    setError("");
    setLoading(true);
    try {
      const res = await fetch(`${API_BASE_URL}/api/auth/forgot-password`, {
        method:  "POST",
        headers: { "Content-Type": "application/json" },
        body:    JSON.stringify({ email: email.trim() }),
      });
      if (!res.ok) throw new Error("Something went wrong. Please try again.");
      setSent(true);
    } catch (e: unknown) {
      setError((e as Error).message ?? "Something went wrong.");
    } finally {
      setLoading(false);
    }
  };

  const canSubmit = email.trim().length > 0 && !loading;

  if (sent) {
    return (
      <View className="flex-1 bg-white dark:bg-gray-900 px-6 justify-center items-center">
        <Text className="text-5xl mb-5">📬</Text>
        <Text className="text-gray-900 dark:text-white text-2xl font-bold text-center mb-3">
          Check your inbox
        </Text>
        <Text className="text-gray-500 dark:text-gray-400 text-center mb-10 leading-6">
          If <Text className="font-semibold text-gray-700 dark:text-gray-200">{email}</Text> is registered,
          you&apos;ll receive a reset link shortly.
        </Text>
        <TouchableOpacity
          onPress={() => router.back()}
          className="bg-blue-600 rounded-2xl py-5 px-10 items-center"
        >
          <Text className="text-white text-base font-bold">Back to sign in</Text>
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

          <TouchableOpacity onPress={() => router.back()} className="mb-8 self-start">
            <Text className="text-blue-500 text-base font-medium">← Back</Text>
          </TouchableOpacity>

          <Text className="text-gray-900 dark:text-white text-3xl font-bold mb-2">
            Forgot password?
          </Text>
          <Text className="text-gray-500 dark:text-gray-400 text-base mb-8 leading-6">
            Enter your email and we&apos;ll send you a link to reset your password.
          </Text>

          <Text className="text-gray-700 dark:text-gray-300 text-sm font-medium mb-1">Email</Text>
          <TextInput
            value={email}
            onChangeText={setEmail}
            placeholder="you@example.com"
            placeholderTextColor={colors.placeholder}
            autoCapitalize="none"
            keyboardType="email-address"
            returnKeyType="send"
            onSubmitEditing={handleSubmit}
            className="bg-gray-100 dark:bg-gray-700 text-gray-900 dark:text-white rounded-xl px-4 py-4 mb-6"
          />

          <TouchableOpacity
            onPress={handleSubmit}
            disabled={!canSubmit}
            className={`rounded-2xl py-5 items-center ${canSubmit ? "bg-blue-600" : "bg-gray-300 dark:bg-gray-600"}`}
          >
            {loading
              ? <ActivityIndicator color="#fff" />
              : <Text className="text-white text-lg font-bold">Send reset link</Text>
            }
          </TouchableOpacity>

        </View>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}
