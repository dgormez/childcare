import React, { useState } from "react";
import {
  View, Text, TouchableOpacity, ScrollView, Alert,
} from "react-native";
import { useStore } from "../../store/useStore";
import { useBreakpoint } from "../../hooks/useBreakpoint";
import { logout } from "../../services/auth";
import { deleteAccount } from "../../services/api";
import { deleteLocalUserData } from "../../services/localDb";
import { ThemedModal, type ModalConfig } from "../../components/ThemedModal";
import dayjs from "dayjs";
import relativeTime from "dayjs/plugin/relativeTime";

dayjs.extend(relativeTime);

const API_BASE_URL = process.env.EXPO_PUBLIC_API_BASE_URL ?? "";

export default function SettingsScreen() {
  const { auth, lastSyncAt, isOnline } = useStore();
  const [modal, setModal]       = useState<ModalConfig | null>(null);
  const [loggingOut, setLoggingOut] = useState(false);
  const { isWide } = useBreakpoint();

  const closeModal = () => setModal(null);

  const handleLogout = () => {
    setModal({
      title:   "Sign out",
      message: "Are you sure you want to sign out?",
      buttons: [
        { label: "Cancel", style: "cancel", onPress: closeModal },
        {
          label: "Sign out",
          style: "destructive",
          onPress: async () => {
            closeModal();
            setLoggingOut(true);
            try {
              if (auth?.userId) deleteLocalUserData(auth.userId);
              await logout(API_BASE_URL);
            } finally {
              setLoggingOut(false);
            }
          },
        },
      ],
    });
  };

  const handleDeleteAccount = () => {
    setModal({
      title:   "Delete account",
      message: "This will permanently delete your account and all your data. This cannot be undone.",
      buttons: [
        { label: "Cancel", style: "cancel", onPress: closeModal },
        {
          label: "Delete forever",
          style: "destructive",
          onPress: async () => {
            closeModal();
            try {
              await deleteAccount();
              if (auth?.userId) deleteLocalUserData(auth.userId);
              await logout(API_BASE_URL);
            } catch {
              Alert.alert("Error", "Could not delete account. Please try again.");
            }
          },
        },
      ],
    });
  };

  return (
    <ScrollView className="flex-1 bg-white dark:bg-gray-900">
      <View className="px-5 pt-16 pb-10" style={isWide ? { maxWidth: 640, alignSelf: "center", width: "100%" } : undefined}>

        {/* Header */}
        <Text className="text-gray-900 dark:text-white text-2xl font-bold mb-8">Settings</Text>

        {/* Account */}
        <Text className="text-gray-500 dark:text-gray-400 text-xs font-semibold uppercase tracking-wider mb-3">
          Account
        </Text>
        <View className="bg-gray-100 dark:bg-gray-800 rounded-2xl px-4 py-4 mb-6">
          <Text className="text-gray-500 dark:text-gray-400 text-xs mb-1">Signed in as</Text>
          <Text className="text-gray-900 dark:text-white font-medium">{auth?.email ?? "—"}</Text>
        </View>

        {/* Sync status */}
        <Text className="text-gray-500 dark:text-gray-400 text-xs font-semibold uppercase tracking-wider mb-3">
          Sync
        </Text>
        <View className="bg-gray-100 dark:bg-gray-800 rounded-2xl px-4 py-4 mb-6">
          <View className="flex-row items-center justify-between mb-1">
            <Text className="text-gray-500 dark:text-gray-400 text-sm">Status</Text>
            <View className="flex-row items-center gap-1">
              <View className={`w-2 h-2 rounded-full ${isOnline ? "bg-green-400" : "bg-yellow-400"}`} />
              <Text className="text-gray-900 dark:text-white text-sm">{isOnline ? "Online" : "Offline"}</Text>
            </View>
          </View>
          <View className="flex-row items-center justify-between">
            <Text className="text-gray-500 dark:text-gray-400 text-sm">Last synced</Text>
            <Text className="text-gray-900 dark:text-white text-sm">
              {lastSyncAt ? dayjs(lastSyncAt).fromNow() : "Never"}
            </Text>
          </View>
        </View>

        {/* Danger zone */}
        <Text className="text-gray-500 dark:text-gray-400 text-xs font-semibold uppercase tracking-wider mb-3">
          Danger zone
        </Text>
        <View className="bg-gray-100 dark:bg-gray-800 rounded-2xl overflow-hidden mb-3">
          <TouchableOpacity
            onPress={handleLogout}
            disabled={loggingOut}
            className="px-4 py-4 active:opacity-70"
          >
            <Text className="text-yellow-500 dark:text-yellow-400 font-semibold">
              {loggingOut ? "Signing out…" : "Sign out"}
            </Text>
          </TouchableOpacity>
        </View>
        <View className="bg-gray-100 dark:bg-gray-800 rounded-2xl overflow-hidden">
          <TouchableOpacity
            onPress={handleDeleteAccount}
            className="px-4 py-4 active:opacity-70"
          >
            <Text className="text-red-500 dark:text-red-400 font-semibold">Delete account</Text>
          </TouchableOpacity>
        </View>

      </View>

      <ThemedModal config={modal} onDismiss={closeModal} />
    </ScrollView>
  );
}
