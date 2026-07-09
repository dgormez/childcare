import React, { useState } from "react";
import { Modal, View, Text, TextInput, TouchableOpacity, Pressable } from "react-native";
import { useTranslation } from "react-i18next";
import { markAbsent, todayDateString } from "../services/attendance";
import { useColors } from "../hooks/useColors";
import type { ChildResponse } from "../types";

interface Props {
  child: ChildResponse | null;
  isConnected: boolean;
  onClose: () => void;
  onSaved: () => void;
}

/**
 * FR-005/FR-017: a visually and interactionally distinct action from the one-tap check-in
 * gesture, to prevent an accidental one-tap absence — reached via a secondary affordance on the
 * child card (long-press), never the primary tap target. No more than 3 taps/selections: open
 * (already counted by the caller's long-press), select justified/unjustified, confirm.
 */
export function AbsenceDialog({ child, isConnected, onClose, onSaved }: Props) {
  const { t } = useTranslation();
  const colors = useColors();
  const [justified, setJustified] = useState<boolean | null>(null);
  const [reason, setReason] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!child) return null;

  const handleConfirm = async () => {
    if (justified === null) return;
    setSaving(true);
    setError(null);
    try {
      await markAbsent(child.id, todayDateString(), justified, reason.trim() || null, isConnected);
      onSaved();
      onClose();
    } catch (e) {
      setError(e instanceof Error ? e.message : "errors.attendance.mark_absent_failed");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal transparent visible animationType="fade" onRequestClose={onClose}>
      <Pressable className="flex-1 justify-center items-center bg-black/60 px-8" onPress={onClose}>
        <Pressable onPress={(e) => e.stopPropagation()} className="w-full bg-surface dark:bg-surface-dark rounded-xl p-4" style={{ maxWidth: 420 }}>
          <Text className="text-text dark:text-text-dark text-lg font-bold mb-1">
            {t("attendance.absence.title", { name: child.firstName })}
          </Text>
          <Text className="text-text-soft dark:text-text-soft-dark text-sm mb-4">{t("attendance.absence.prompt")}</Text>

          <View className="flex-row mb-4" style={{ gap: 12 }}>
            <TouchableOpacity
              onPress={() => setJustified(true)}
              style={{ minHeight: 48, flex: 1 }}
              className={`items-center justify-center rounded-lg border-2 ${justified === true ? "border-primary dark:border-primary-dark bg-primary-soft dark:bg-primary-soft-dark" : "border-border dark:border-border-dark"}`}
            >
              <Text className="text-text dark:text-text-dark font-medium">{t("attendance.absence.justified")}</Text>
            </TouchableOpacity>
            <TouchableOpacity
              onPress={() => setJustified(false)}
              style={{ minHeight: 48, flex: 1 }}
              className={`items-center justify-center rounded-lg border-2 ${justified === false ? "border-primary dark:border-primary-dark bg-primary-soft dark:bg-primary-soft-dark" : "border-border dark:border-border-dark"}`}
            >
              <Text className="text-text dark:text-text-dark font-medium">{t("attendance.absence.unjustified")}</Text>
            </TouchableOpacity>
          </View>

          <TextInput
            value={reason}
            onChangeText={setReason}
            placeholder={t("attendance.absence.reasonPlaceholder")}
            placeholderTextColor={colors.placeholder}
            className="bg-surface-soft dark:bg-surface-soft-dark rounded-lg px-3 text-text dark:text-text-dark mb-3"
            style={{ minHeight: 48 }}
          />

          {error && <Text className="text-danger dark:text-danger-dark text-sm mb-3">{t(error)}</Text>}

          <View className="flex-row justify-end" style={{ gap: 16 }}>
            <TouchableOpacity onPress={onClose} style={{ minHeight: 48 }} className="items-center justify-center px-4">
              <Text className="text-text-soft dark:text-text-soft-dark font-medium">{t("attendance.absence.cancel")}</Text>
            </TouchableOpacity>
            <TouchableOpacity
              onPress={handleConfirm}
              disabled={saving || justified === null}
              style={{ minHeight: 48 }}
              className="items-center justify-center px-4"
            >
              <Text className="text-primary-hover dark:text-primary-hover-dark font-semibold">
                {t("attendance.absence.confirm")}
              </Text>
            </TouchableOpacity>
          </View>
        </Pressable>
      </Pressable>
    </Modal>
  );
}
