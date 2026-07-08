import React, { useCallback, useEffect, useState } from "react";
import { View, Text, FlatList, RefreshControl, ActivityIndicator } from "react-native";
import { useFocusEffect } from "expo-router";
import { useTranslation } from "react-i18next";
import { getRoster, checkIn, checkOut } from "../../services/roomShift";
import { PinKeypad } from "../../components/PinKeypad";
import { CaregiverCard } from "../../components/CaregiverCard";
import { useColors } from "../../hooks/useColors";
import type { RoomRosterCard } from "../../types";

/**
 * Room home screen (spec User Story 3/FR-013): every location-eligible caregiver as a photo
 * card, checked-in cards visually distinct. Tap a card → PIN keypad overlay addressed by name
 * → check-in or check-out, whichever this card's current state calls for.
 */
export default function RoomHomeScreen() {
  const { t } = useTranslation();
  const colors = useColors();

  const [roster, setRoster] = useState<RoomRosterCard[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [selected, setSelected] = useState<RoomRosterCard | null>(null);

  const load = useCallback(async (isRefresh = false) => {
    if (isRefresh) setRefreshing(true); else setLoading(true);
    try {
      setRoster(await getRoster());
    } finally {
      if (isRefresh) setRefreshing(false); else setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // FR-013: the roster updates immediately after any check-in/check-out — refetch whenever
  // this screen regains focus (e.g. returning from the PIN overlay).
  useFocusEffect(useCallback(() => { load(); /* eslint-disable-line react-hooks/exhaustive-deps */ }, []));

  if (loading) {
    return (
      <View style={{ flex: 1, alignItems: "center", justifyContent: "center", backgroundColor: colors.background }}>
        <ActivityIndicator size="large" color={colors.primary} />
      </View>
    );
  }

  return (
    <View style={{ flex: 1, backgroundColor: colors.background }}>
      <FlatList
        testID="room-roster-list"
        data={roster ?? []}
        keyExtractor={(c) => c.staffProfileId}
        numColumns={3}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => load(true)} />}
        contentContainerStyle={{ padding: 16 }}
        ListEmptyComponent={
          <View style={{ flex: 1, alignItems: "center", justifyContent: "center", paddingTop: 64 }}>
            <Text style={{ color: colors.textSoft }}>{t("roomHome.noCaregivers")}</Text>
          </View>
        }
        renderItem={({ item }) => (
          <CaregiverCard card={item} onPress={() => setSelected(item)} showCheckedInState />
        )}
      />

      {selected && (
        <View style={{ position: "absolute", top: 0, left: 0, right: 0, bottom: 0 }}>
          <PinKeypad
            name={selected.firstName}
            pinLength={4}
            onCancel={() => setSelected(null)}
            onSuccess={() => { setSelected(null); load(); }}
            onSubmit={(pin) => (selected.checkedIn ? checkOut(selected.staffProfileId, pin) : checkIn(selected.staffProfileId, pin))}
          />
        </View>
      )}
    </View>
  );
}
