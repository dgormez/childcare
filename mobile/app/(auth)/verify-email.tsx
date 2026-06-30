import React, { useEffect, useState } from "react";
import { View, Text, TouchableOpacity, ActivityIndicator } from "react-native";
import { useRouter, useLocalSearchParams } from "expo-router";
import { verifyEmail } from "../../services/api";
import { useStore } from "../../store/useStore";

export default function VerifyEmailScreen() {
  const router = useRouter();
  const { token } = useLocalSearchParams<{ token: string }>();
  const auth = useStore((s) => s.auth);

  const [status, setStatus] = useState<"loading" | "success" | "error">("loading");

  useEffect(() => {
    if (!token) { setStatus("error"); return; }
    verifyEmail(token)
      .then(() => setStatus("success"))
      .catch(() => setStatus("error"));
  }, [token]);

  const handleContinue = () => {
    router.replace(auth ? "/(tabs)" : "/(auth)/login");
  };

  if (status === "loading") {
    return (
      <View className="flex-1 bg-white dark:bg-gray-900 items-center justify-center px-6">
        <ActivityIndicator size="large" color="#3b82f6" />
        <Text className="text-gray-500 dark:text-gray-400 mt-4">Verifying your email…</Text>
      </View>
    );
  }

  if (status === "success") {
    return (
      <View className="flex-1 bg-white dark:bg-gray-900 items-center justify-center px-6">
        <Text className="text-5xl mb-5">✅</Text>
        <Text className="text-gray-900 dark:text-white text-2xl font-bold text-center mb-3">
          Email verified!
        </Text>
        <Text className="text-gray-500 dark:text-gray-400 text-center mb-10 leading-6">
          Your account is now fully active.
        </Text>
        <TouchableOpacity
          onPress={handleContinue}
          className="bg-blue-600 rounded-2xl py-5 px-10 items-center"
        >
          <Text className="text-white text-base font-bold">Continue to app</Text>
        </TouchableOpacity>
      </View>
    );
  }

  return (
    <View className="flex-1 bg-white dark:bg-gray-900 items-center justify-center px-6">
      <Text className="text-5xl mb-5">❌</Text>
      <Text className="text-gray-900 dark:text-white text-2xl font-bold text-center mb-3">
        Link expired
      </Text>
      <Text className="text-gray-500 dark:text-gray-400 text-center mb-10 leading-6">
        This verification link is invalid or has expired.
      </Text>
      <TouchableOpacity
        onPress={() => router.replace("/(auth)/login")}
        className="bg-blue-600 rounded-2xl py-5 px-10 items-center"
      >
        <Text className="text-white text-base font-bold">Back to sign in</Text>
      </TouchableOpacity>
    </View>
  );
}
