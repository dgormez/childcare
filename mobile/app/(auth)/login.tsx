import React, { useState } from "react";
import {
  View, Text, TextInput, TouchableOpacity,
  KeyboardAvoidingView, ScrollView, ActivityIndicator, Platform,
} from "react-native";
import { useRouter } from "expo-router";
import { login } from "../../services/auth";
import { useGoogleSignIn } from "../../services/googleAuth";
import { useAppleSignIn } from "../../services/appleAuth";
import { ThemedModal } from "../../components/ThemedModal";
import { useColors } from "../../hooks/useColors";
import { useBreakpoint } from "../../hooks/useBreakpoint";

const API_BASE_URL = process.env.EXPO_PUBLIC_API_BASE_URL ?? "";

export default function LoginScreen() {
  const router = useRouter();
  const colors = useColors();
  const { isWide } = useBreakpoint();

  const [email,    setEmail]    = useState("");
  const [password, setPassword] = useState("");
  const [loading,  setLoading]  = useState(false);
  const [error,    setError]    = useState("");

  const googleSignIn = useGoogleSignIn(API_BASE_URL);
  const appleSignIn  = useAppleSignIn(API_BASE_URL);

  const handleLogin = async () => {
    setError("");
    setLoading(true);
    try {
      await login(API_BASE_URL, email.trim(), password);
      router.replace("/(tabs)");
    } catch (e: unknown) {
      setError((e as Error).message ?? "Login failed. Please try again.");
    } finally {
      setLoading(false);
    }
  };

  const anyLoading = loading || googleSignIn.loading || appleSignIn.loading;
  const canSubmit  = email.trim().length > 0 && password.length > 0 && !anyLoading;

  const combinedError = error || googleSignIn.error || appleSignIn.error;
  const clearErrors   = () => { setError(""); googleSignIn.clearError(); appleSignIn.clearError(); };

  return (
    <KeyboardAvoidingView behavior="padding" className="flex-1 bg-white dark:bg-gray-900">
      <ScrollView
        contentContainerStyle={{ flexGrow: 1 }}
        keyboardShouldPersistTaps="handled"
      >
        <View className="flex-1 px-6 justify-center pb-10" style={isWide ? { alignSelf: "center", width: "100%", maxWidth: 480 } : undefined}>

          {/* Header */}
          <Text className="text-5xl text-center mb-3">✅</Text>
          <Text className="text-gray-900 dark:text-white text-3xl font-bold text-center mb-2">ChildCare</Text>
          <Text className="text-gray-500 dark:text-gray-400 text-center text-base mb-10">
            Sign in to your account
          </Text>

          <ThemedModal
            config={combinedError ? {
              title:   "Sign-in failed",
              message: combinedError,
              buttons: [{ label: "OK", style: "default", onPress: clearErrors }],
            } : null}
            onDismiss={clearErrors}
          />

          {/* Email */}
          <Text className="text-gray-700 dark:text-gray-300 text-sm font-medium mb-1">Email</Text>
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

          {/* Password */}
          <Text className="text-gray-700 dark:text-gray-300 text-sm font-medium mb-1">Password</Text>
          <TextInput
            value={password}
            onChangeText={setPassword}
            placeholder="••••••••"
            placeholderTextColor={colors.placeholder}
            secureTextEntry
            returnKeyType="done"
            onSubmitEditing={handleLogin}
            className="bg-gray-100 dark:bg-gray-700 text-gray-900 dark:text-white rounded-xl px-4 py-4 mb-2"
          />

          {/* Forgot password */}
          <TouchableOpacity
            onPress={() => router.push("/(auth)/forgot-password")}
            className="self-end mb-6"
          >
            <Text className="text-blue-500 text-sm font-medium">Forgot password?</Text>
          </TouchableOpacity>

          {/* Sign in button */}
          <TouchableOpacity
            onPress={handleLogin}
            disabled={!canSubmit}
            className={`rounded-2xl py-5 items-center ${canSubmit ? "bg-blue-600" : "bg-gray-300 dark:bg-gray-600"}`}
          >
            {loading
              ? <ActivityIndicator color="#fff" />
              : <Text className="text-white text-lg font-bold">Sign in</Text>
            }
          </TouchableOpacity>

          {/* Divider */}
          <View className="flex-row items-center my-5">
            <View className="flex-1 h-px bg-gray-200 dark:bg-gray-700" />
            <Text className="text-gray-400 dark:text-gray-500 text-sm mx-3">or</Text>
            <View className="flex-1 h-px bg-gray-200 dark:bg-gray-700" />
          </View>

          {/* Apple Sign In — iOS only, shown when available */}
          {Platform.OS === "ios" && appleSignIn.isAvailable && (
            <TouchableOpacity
              onPress={appleSignIn.signIn}
              disabled={anyLoading}
              className="flex-row items-center justify-center bg-black dark:bg-white rounded-2xl py-4 mb-3"
            >
              {appleSignIn.loading
                ? <ActivityIndicator color="#fff" />
                : <Text className="text-white dark:text-black text-base font-semibold">Continue with Apple</Text>
              }
            </TouchableOpacity>
          )}

          {/* Google Sign In */}
          <TouchableOpacity
            onPress={googleSignIn.signIn}
            disabled={!googleSignIn.ready || anyLoading}
            className="flex-row items-center justify-center bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-2xl py-4"
          >
            {googleSignIn.loading
              ? <ActivityIndicator color="#111827" />
              : <Text className="text-gray-900 dark:text-white text-base font-semibold">Continue with Google</Text>
            }
          </TouchableOpacity>

          {/* Register link */}
          <TouchableOpacity
            onPress={() => router.push("/(auth)/register")}
            className="items-center py-4 mt-2"
          >
            <Text className="text-gray-500 dark:text-gray-400">
              Don&apos;t have an account?{" "}
              <Text className="text-blue-500 font-semibold">Create one</Text>
            </Text>
          </TouchableOpacity>

        </View>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}
