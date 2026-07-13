import React, { useCallback, useEffect, useState } from "react";
import { View, Text, FlatList, RefreshControl, ActivityIndicator } from "react-native";
import { useFocusEffect } from "expo-router";
import { useTranslation } from "react-i18next";
import { AlertTriangle } from "lucide-react-native";
import { getRoster, checkIn, checkOut } from "../../services/roomShift";
import { PinKeypad } from "../../components/PinKeypad";
import { CaregiverCard } from "../../components/CaregiverCard";
import { useColors } from "../../hooks/useColors";
import type { RoomRosterCard } from "../../types";

/**
 * Room home screen (spec User Story 3/FR-013): every location-eligible caregiver as a photo
 * card, checked-in cards visually distinct. Tap a card → PIN keypad overlay addressed by name
 * → check-in or check-out, whichever this card's current state calls for.
 *
 * Feature 008b: when the roster reports `requiresCaregiverPin: false` for this location, a tap
 * completes check-in/out immediately with no keypad shown at all (FR-004/FR-008), rather than a
 * keypad that would accept anything.
 */
export default function RoomHomeScreen() {
  const { t } = useTranslation();
  const colors = useColors();

  const [roster, setRoster] = useState<RoomRosterCard[] | null>(null);
  const [requiresCaregiverPin, setRequiresCaregiverPin] = useState(true);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [selected, setSelected] = useState<RoomRosterCard | null>(null);
  const [processingId, setProcessingId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async (isRefresh = false) => {
    if (isRefresh) setRefreshing(true); else setLoading(true);
    try {
      const response = await getRoster();
      setRoster(response.caregivers);
      setRequiresCaregiverPin(response.requiresCaregiverPin);
    } finally {
      if (isRefresh) setRefreshing(false); else setLoading(false);
    }
  }, []);

  const handleCardPress = useCallback(async (card: RoomRosterCard) => {
    if (requiresCaregiverPin) {
      setSelected(card);
      return;
    }
    if (processingId) return; // ignore taps while another card's request is in flight
    setError(null);
    setProcessingId(card.staffProfileId);
    const result = card.checkedIn ? await checkOut(card.staffProfileId) : await checkIn(card.staffProfileId);
    setProcessingId(null);
    if (!result.ok) {
      setError(t("roomHome.checkInFailed"));
      return;
    }
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [requiresCaregiverPin, processingId, load]);

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
        ListHeaderComponent={
          error ? (
            <View style={{ flexDirection: "row", alignItems: "center", justifyContent: "center", gap: 8, marginBottom: 8 }}>
              <AlertTriangle size={16} color={colors.danger} strokeWidth={2} />
              <Text style={{ color: colors.danger }}>{error}</Text>
            </View>
          ) : null
        }
        renderItem={({ item }) => (
          <CaregiverCard
            card={item}
            onPress={() => handleCardPress(item)}
            showCheckedInState
          />
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
