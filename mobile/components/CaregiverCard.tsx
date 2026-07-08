import React from "react";
import { TouchableOpacity, Image, View, Text } from "react-native";
import { useTranslation } from "react-i18next";
import { useColors } from "../hooks/useColors";
import type { RoomRosterCard } from "../types";

interface Props {
  card: RoomRosterCard;
  onPress: () => void;
  /** Room home distinguishes checked-in cards visually and shows the check-in time;
   * AdministratorConfirmation only ever lists already-checked-in caregivers, so neither
   * applies there. */
  showCheckedInState?: boolean;
}

/** Shared photo-card tile (spec User Story 3/FR-013) — reused by room-home and
 * AdministratorConfirmation rather than each reimplementing the same card. */
export function CaregiverCard({ card, onPress, showCheckedInState = false }: Props) {
  const { t } = useTranslation();
  const colors = useColors();
  const checkedIn = showCheckedInState && card.checkedIn;

  return (
    <TouchableOpacity
      onPress={onPress}
      style={{ minHeight: 64, flex: 1, margin: 8 }}
      className={`items-center rounded-xl p-3 ${checkedIn ? "bg-success-bg dark:bg-success-bg-dark border-2 border-success dark:border-success-dark" : "bg-surface-soft dark:bg-surface-soft-dark"}`}
    >
      {card.photoUrl ? (
        <Image source={{ uri: card.photoUrl }} style={{ width: 64, height: 64, borderRadius: 32, marginBottom: 8 }} />
      ) : (
        <View style={{ width: 64, height: 64, borderRadius: 32, marginBottom: 8, backgroundColor: colors.border }} />
      )}
      <Text className="text-text dark:text-text-dark font-semibold text-base text-center">{card.firstName}</Text>
      {checkedIn && card.checkedInAt && (
        <Text className="text-success dark:text-success-dark text-xs mt-1 text-center">
          {t("roomHome.checkedInAt", { time: new Date(card.checkedInAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" }) })}
        </Text>
      )}
    </TouchableOpacity>
  );
}
