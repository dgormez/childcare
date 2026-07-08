import React, { useEffect, useState } from "react";
import { View, Text, FlatList, TouchableOpacity, ActivityIndicator } from "react-native";
import { useTranslation } from "react-i18next";
import { getRoster, confirmAdministrator } from "../services/roomShift";
import { useNetworkStatus } from "../hooks/useNetworkStatus";
import { useColors } from "../hooks/useColors";
import { PinKeypad } from "./PinKeypad";
import { CaregiverCard } from "./CaregiverCard";
import type { RoomRosterCard } from "../types";

interface Props {
  /** Called once, either with the confirmed caregiver's id or null (skipped). */
  onComplete: (administeredByStaffProfileId: string | null) => void;
}

/**
 * Reusable select-then-PIN sensitive-action confirmation (spec User Story 5) — feature 009's
 * medical events will mount this the same way room-home mounts PinKeypad directly. Only
 * currently-checked-in caregivers are offered as cards (FR-017: a valid PIN alone isn't
 * enough, the caregiver must actually be present). Skip always succeeds; when offline it
 * resolves to null locally without an API call at all (US5 AC3) — there is nothing to verify
 * server-side for a skip, so there's no reason to queue one.
 */
export function AdministratorConfirmation({ onComplete }: Props) {
  const { t } = useTranslation();
  const colors = useColors();
  const { isConnected } = useNetworkStatus();

  const [checkedIn, setCheckedIn] = useState<RoomRosterCard[] | null>(null);
  const [selected, setSelected] = useState<RoomRosterCard | null>(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      const roster = await getRoster();
      if (!cancelled) setCheckedIn(roster.filter((c) => c.checkedIn));
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const handleSkip = async () => {
    if (!isConnected) {
      onComplete(null);
      return;
    }
    const result = await confirmAdministrator(null, null, true);
    onComplete(result.administeredByStaffProfileId);
  };

  if (checkedIn === null) {
    return (
      <View style={{ flex: 1, alignItems: "center", justifyContent: "center", backgroundColor: colors.background }}>
        <ActivityIndicator size="large" color={colors.primary} />
      </View>
    );
  }

  return (
    <View style={{ flex: 1, backgroundColor: colors.background }}>
      <Text className="text-text dark:text-text-dark text-lg font-bold text-center mt-6 mb-4">
        {t("pin.confirmAdministrator")}
      </Text>

      <FlatList
        testID="administrator-confirmation-list"
        data={checkedIn}
        keyExtractor={(c) => c.staffProfileId}
        numColumns={3}
        contentContainerStyle={{ padding: 16 }}
        renderItem={({ item }) => (
          <CaregiverCard card={item} onPress={() => setSelected(item)} />
        )}
      />

      <TouchableOpacity onPress={handleSkip} style={{ minHeight: 48 }} className="items-center justify-center mb-6">
        <Text className="text-text-soft dark:text-text-soft-dark font-medium">{t("pin.skip")}</Text>
      </TouchableOpacity>

      {selected && (
        <View style={{ position: "absolute", top: 0, left: 0, right: 0, bottom: 0 }}>
          <PinKeypad
            name={selected.firstName}
            pinLength={4}
            onCancel={() => setSelected(null)}
            onSuccess={() => onComplete(selected.staffProfileId)}
            onSubmit={(pin) => confirmAdministrator(selected.staffProfileId, pin, false)}
          />
        </View>
      )}
    </View>
  );
}
