import React, { useState } from "react";
import { Modal, View, Text, TextInput, TouchableOpacity, Pressable } from "react-native";
import { useTranslation } from "react-i18next";
import { updateChildEvent } from "../services/childEvents";
import type { ChildEventResponse } from "../types";

interface Props {
  event: ChildEventResponse | null;
  isConnected: boolean;
  onClose: () => void;
  onSaved: () => void;
}

// Maps a payload field name to its i18n label key (constitution Principle IV — a raw field name
// like "doseDescription" is exactly the "database/log phrasing" caregiver-facing text must
// never show).
const FIELD_LABEL_KEYS: Record<string, string> = {
  type: "childEvents.fieldLabels.type",
  notes: "childEvents.diaper.notes",
  value: "childEvents.fieldLabels.value",
  quality: "childEvents.fieldLabels.quality",
  celsius: "childEvents.temperature.celsius",
  name: "childEvents.fieldLabels.name",
  doseDescription: "childEvents.medication.doseDescription",
  reason: "childEvents.medication.reason",
  ml: "childEvents.feedingBottle.ml",
  description: "childEvents.feedingSolid.description",
  text: "childEvents.note.text",
  kg: "childEvents.weight.kg",
  weightKg: "childEvents.measurement.weightKg",
  heightCm: "childEvents.measurement.heightCm",
  headCm: "childEvents.measurement.headCm",
};

// Server-computed — editing it here would just be silently overwritten by
// SleepDurationEnricher on the next save (backend/ChildCare.Application/ChildEvents/
// SleepDurationEnricher.cs), so it's excluded from the editable field list entirely.
const READ_ONLY_FIELDS = new Set(["durationMinutes"]);

/**
 * Generic field-by-field editor over an existing event's payload — same-day correction (FR-006),
 * not a re-run of QuickActionSheet's guided per-type entry. Number-typed fields keep a numeric
 * keyboard; a save that fails validation (e.g. an enum field edited to something invalid)
 * surfaces the server's rejection rather than silently discarding the edit.
 */
export function EditEventModal({ event, isConnected, onClose, onSaved }: Props) {
  const { t } = useTranslation();
  const [fields, setFields] = useState<Record<string, string>>(() =>
    event
      ? Object.fromEntries(
          Object.entries(event.payload)
            .filter(([k]) => !READ_ONLY_FIELDS.has(k))
            .map(([k, v]) => [k, String(v)])
        )
      : {}
  );
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  if (!event) return null;

  const handleSave = async () => {
    setSaving(true);
    setError(null);
    try {
      const payload: Record<string, unknown> = {};
      for (const [key, value] of Object.entries(fields)) {
        const original = event.payload[key];
        payload[key] = typeof original === "number" ? Number(value) : value;
      }
      await updateChildEvent(event.id, { payload }, isConnected);
      onSaved();
      onClose();
    } catch (e) {
      setError(e instanceof Error ? e.message : "errors.validation");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal transparent visible animationType="fade" onRequestClose={onClose}>
      <Pressable className="flex-1 justify-center items-center bg-black/60 px-8" onPress={onClose}>
        <Pressable onPress={(e) => e.stopPropagation()} className="w-full bg-surface dark:bg-surface-dark rounded-xl p-5" style={{ maxWidth: 420 }}>
          <Text className="text-text dark:text-text-dark text-lg font-bold mb-4">
            {t(`childEvents.types.${event.eventType}`)}
          </Text>

          {Object.entries(fields).map(([field, value]) => (
            <View key={field} className="mb-3">
              <Text className="text-text-soft dark:text-text-soft-dark text-sm mb-1">
                {t(FIELD_LABEL_KEYS[field] ?? field)}
              </Text>
              <TextInput
                value={value}
                onChangeText={(v) => setFields((f) => ({ ...f, [field]: v }))}
                keyboardType={typeof event.payload[field] === "number" ? "decimal-pad" : "default"}
                className="bg-surface-soft dark:bg-surface-soft-dark rounded-lg px-3 text-text dark:text-text-dark"
                style={{ minHeight: 48 }}
              />
            </View>
          ))}

          {error && <Text className="text-danger dark:text-danger-dark text-sm mb-3">{t(error)}</Text>}

          <View className="flex-row justify-end" style={{ gap: 16 }}>
            <TouchableOpacity onPress={onClose} style={{ minHeight: 48 }} className="items-center justify-center px-4">
              <Text className="text-text-soft dark:text-text-soft-dark font-medium">{t("childEvents.cancel")}</Text>
            </TouchableOpacity>
            <TouchableOpacity onPress={handleSave} disabled={saving} style={{ minHeight: 48 }} className="items-center justify-center px-4">
              <Text className="text-primary-hover dark:text-primary-hover-dark font-semibold">{t("childEvents.save")}</Text>
            </TouchableOpacity>
          </View>
        </Pressable>
      </Pressable>
    </Modal>
  );
}
