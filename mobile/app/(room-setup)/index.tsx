import React, { useEffect, useState } from "react";
import { View, Text, TextInput, TouchableOpacity, ScrollView, ActivityIndicator } from "react-native";
import { useRouter } from "expo-router";
import { useTranslation } from "react-i18next";
import { apiClient } from "../../services/apiClient";
import { pairDevice } from "../../services/deviceAuth";
import { ThemedModal } from "../../components/ThemedModal";
import { useColors } from "../../hooks/useColors";
import type { LocationResponse, GroupResponse } from "../../types";

/**
 * FR-001: one-time director pairing flow (spec User Story 1). Reached after feature 008's
 * existing email/password login (director role) — see login.tsx's post-login redirect.
 * Location/group selection uses tap-to-select rows rather than a native picker component,
 * consistent with this app's large-touch-target kiosk surfaces (platform-rules.md).
 */
export default function RoomSetupScreen() {
  const router = useRouter();
  const colors = useColors();
  const { t } = useTranslation();

  const [locations, setLocations] = useState<LocationResponse[]>([]);
  const [groups, setGroups] = useState<GroupResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [locationId, setLocationId] = useState<string | null>(null);
  const [groupId, setGroupId] = useState<string | null>(null);
  const [overridePin, setOverridePin] = useState("");
  const [overridePinConfirm, setOverridePinConfirm] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");
  const [showOverridePin, setShowOverridePin] = useState(false);

  useEffect(() => {
    async function load() {
      try {
        const [locationsResult, groupsResult] = await Promise.all([
          apiClient.GET("/api/locations"),
          apiClient.GET("/api/groups"),
        ]);
        // openapi-fetch already parses the body into result.data — result.response.json()
        // would throw ("body already read") since the stream is already consumed.
        if (locationsResult.response.ok) setLocations(locationsResult.data as unknown as LocationResponse[]);
        if (groupsResult.response.ok) setGroups(groupsResult.data as unknown as GroupResponse[]);
      } finally {
        setLoading(false);
      }
    }
    load();
  }, []);

  const groupsForLocation = groups.filter((g) => g.locationId === locationId);

  const canSubmit =
    !!locationId && !!groupId &&
    /^[0-9]{6}$/.test(overridePin) &&
    overridePin === overridePinConfirm &&
    !submitting;

  const handleSubmit = async () => {
    if (!locationId || !groupId) return;
    if (overridePin !== overridePinConfirm) {
      setError(t("roomSetup.overridePinMismatch"));
      return;
    }

    setError("");
    setSubmitting(true);
    try {
      const location = locations.find((l) => l.id === locationId)!;
      const group = groups.find((g) => g.id === groupId)!;
      await pairDevice(locationId, groupId, location.name, group.name, overridePin);
      router.replace("/(room)");
    } catch (e: unknown) {
      const errorKey = (e as Error).message;
      setError(t(errorKey, { defaultValue: errorKey }));
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <View style={{ flex: 1, alignItems: "center", justifyContent: "center", backgroundColor: colors.background }}>
        <ActivityIndicator size="large" color={colors.primary} />
      </View>
    );
  }

  return (
    <ScrollView className="flex-1 bg-background dark:bg-background-dark" contentContainerStyle={{ padding: 24 }}>
      <ThemedModal
        config={error ? {
          title: t("roomSetup.title"),
          message: error,
          buttons: [{ label: "OK", style: "default", onPress: () => setError("") }],
        } : null}
        onDismiss={() => setError("")}
      />

      <Text className="text-text dark:text-text-dark text-2xl font-bold mb-6">{t("roomSetup.title")}</Text>

      <Text className="text-text-soft dark:text-text-soft-dark text-sm font-medium mb-2">{t("roomSetup.locationLabel")}</Text>
      {locations.map((location) => (
        <TouchableOpacity
          key={location.id}
          onPress={() => { setLocationId(location.id); setGroupId(null); }}
          style={{ minHeight: 64 }}
          className={`rounded-lg px-4 justify-center mb-2 ${locationId === location.id ? "bg-primary-soft dark:bg-primary-soft-dark border-2 border-primary dark:border-primary-dark" : "bg-surface-soft dark:bg-surface-soft-dark"}`}
        >
          <Text className="text-text dark:text-text-dark text-base font-medium">{location.name}</Text>
        </TouchableOpacity>
      ))}

      {!!locationId && (
        <>
          <Text className="text-text-soft dark:text-text-soft-dark text-sm font-medium mb-2 mt-4">{t("roomSetup.groupLabel")}</Text>
          {groupsForLocation.map((group) => (
            <TouchableOpacity
              key={group.id}
              onPress={() => setGroupId(group.id)}
              style={{ minHeight: 64 }}
              className={`rounded-lg px-4 justify-center mb-2 ${groupId === group.id ? "bg-primary-soft dark:bg-primary-soft-dark border-2 border-primary dark:border-primary-dark" : "bg-surface-soft dark:bg-surface-soft-dark"}`}
            >
              <Text className="text-text dark:text-text-dark text-base font-medium">{group.name}</Text>
            </TouchableOpacity>
          ))}
        </>
      )}

      {!!groupId && (
        <>
          <Text className="text-text-soft dark:text-text-soft-dark text-sm font-medium mb-1 mt-4">{t("roomSetup.overridePinLabel")}</Text>
          <View className="flex-row items-center bg-surface-soft dark:bg-surface-soft-dark rounded-lg mb-4">
            <TextInput
              value={overridePin}
              onChangeText={setOverridePin}
              keyboardType="number-pad"
              secureTextEntry={!showOverridePin}
              maxLength={6}
              placeholderTextColor={colors.placeholder}
              style={{ minHeight: 64 }}
              className="flex-1 text-text dark:text-text-dark px-4 text-lg"
            />
            <TouchableOpacity
              onPress={() => setShowOverridePin((v) => !v)}
              style={{ minWidth: 48, minHeight: 48, alignItems: "center", justifyContent: "center" }}
            >
              <Text className="text-text-soft dark:text-text-soft-dark text-sm font-medium">
                {showOverridePin ? t("login.hidePassword") : t("login.showPassword")}
              </Text>
            </TouchableOpacity>
          </View>

          <Text className="text-text-soft dark:text-text-soft-dark text-sm font-medium mb-1">{t("roomSetup.overridePinConfirmLabel")}</Text>
          <View className="flex-row items-center bg-surface-soft dark:bg-surface-soft-dark rounded-lg mb-6">
            <TextInput
              value={overridePinConfirm}
              onChangeText={setOverridePinConfirm}
              keyboardType="number-pad"
              secureTextEntry={!showOverridePin}
              maxLength={6}
              placeholderTextColor={colors.placeholder}
              style={{ minHeight: 64 }}
              className="flex-1 text-text dark:text-text-dark px-4 text-lg"
            />
            <TouchableOpacity
              onPress={() => setShowOverridePin((v) => !v)}
              style={{ minWidth: 48, minHeight: 48, alignItems: "center", justifyContent: "center" }}
            >
              <Text className="text-text-soft dark:text-text-soft-dark text-sm font-medium">
                {showOverridePin ? t("login.hidePassword") : t("login.showPassword")}
              </Text>
            </TouchableOpacity>
          </View>

          {!!overridePin && !!overridePinConfirm && overridePin !== overridePinConfirm && (
            <Text className="text-danger dark:text-danger-dark text-sm mb-4">{t("roomSetup.overridePinMismatch")}</Text>
          )}

          <TouchableOpacity
            onPress={handleSubmit}
            disabled={!canSubmit}
            style={{ minHeight: 64 }}
            className={`rounded-lg items-center justify-center ${canSubmit ? "bg-primary dark:bg-primary-dark" : "bg-border dark:bg-border-dark"}`}
          >
            {submitting
              ? <ActivityIndicator color="#fff" />
              : <Text className="text-white text-lg font-bold">{t("roomSetup.submit")}</Text>
            }
          </TouchableOpacity>
        </>
      )}
    </ScrollView>
  );
}
