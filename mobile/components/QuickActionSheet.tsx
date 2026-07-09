import React, { useState } from "react";
import { Modal, View, Text, TouchableOpacity, TextInput, ScrollView, ActivityIndicator, Pressable } from "react-native";
import { useTranslation } from "react-i18next";
import type { TFunction } from "i18next";
import {
  Moon, Thermometer, Pill, Milk, UtensilsCrossed, Droplets, Smile, Activity as ActivityIcon,
  StickyNote, Scale, Ruler, Tag, X,
} from "lucide-react-native";
import { useColors } from "../hooks/useColors";
import { useNetworkStatus } from "../hooks/useNetworkStatus";
import { recordChildEvent, endSleepEvent } from "../services/childEvents";
import { AdministratorConfirmation } from "./AdministratorConfirmation";
import type { ChildEventResponse, ChildEventType } from "../types";

interface Props {
  visible: boolean;
  childId: string;
  /** Set when this child has an open (unended) sleep event — tapping "sleep" then offers to
   * end it instead of starting a second one. */
  inProgressSleepEventId: string | null;
  onClose: () => void;
  onEventRecorded: (event: ChildEventResponse) => void;
}

const EVENT_TYPES: { type: ChildEventType; Icon: typeof Moon }[] = [
  { type: "diaper", Icon: Droplets },
  { type: "feeding_bottle", Icon: Milk },
  { type: "mood", Icon: Smile },
  { type: "sleep", Icon: Moon },
  { type: "temperature", Icon: Thermometer },
  { type: "medication", Icon: Pill },
  { type: "feeding_solid", Icon: UtensilsCrossed },
  { type: "activity", Icon: ActivityIcon },
  { type: "note", Icon: StickyNote },
  { type: "weight", Icon: Scale },
  { type: "growth_check", Icon: Ruler },
  { type: "custom", Icon: Tag },
];

// FR-016/User Story 2: medication and temperature route through the select-then-PIN
// administrator-confirmation step (reused from feature 008a) before the event is submitted.
const NEEDS_ADMIN_CONFIRMATION: ChildEventType[] = ["medication", "temperature"];
const FREE_TEXT_FIELD: Partial<Record<ChildEventType, string>> = {
  feeding_solid: "description",
  activity: "description",
  note: "text",
};

/**
 * Bottom sheet (not full-screen modal), icon-based quick entry (design-system.md/
 * platform-rules.md). Routine types (diaper, mood, feeding_bottle) resolve in 2 taps after
 * opening (FR-021); everything else is a short single-screen form. Diaper's optional `notes`
 * field is intentionally not exposed here — surfacing it would cost every diaper entry a third
 * tap; a director can add it later via a correction if ever needed.
 */
export function QuickActionSheet({ visible, childId, inProgressSleepEventId, onClose, onEventRecorded }: Props) {
  const { t } = useTranslation();
  const colors = useColors();
  const { isConnected } = useNetworkStatus();

  const [selectedType, setSelectedType] = useState<ChildEventType | null>(null);
  const [freeText, setFreeText] = useState("");
  const [numericFields, setNumericFields] = useState<Record<string, string>>({});
  const [pendingSubmit, setPendingSubmit] = useState<{ eventType: ChildEventType; payload: Record<string, unknown> } | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const reset = () => {
    setSelectedType(null);
    setFreeText("");
    setNumericFields({});
    setPendingSubmit(null);
  };

  const close = () => {
    reset();
    onClose();
  };

  const submit = async (eventType: ChildEventType, payload: Record<string, unknown>, administeredByStaffId?: string | null) => {
    setSubmitting(true);
    try {
      const event = await recordChildEvent(
        { childId, eventType, occurredAt: new Date().toISOString(), payload, administeredByStaffId },
        isConnected,
      );
      onEventRecorded(event);
      close();
    } finally {
      setSubmitting(false);
    }
  };

  const handleSelectType = (type: ChildEventType) => {
    if (type === "sleep" && inProgressSleepEventId) {
      setSelectedType("sleep");
      return;
    }
    if (type === "sleep") {
      submit("sleep", {}); // starting a nap needs no fields — 1 tap
      return;
    }
    setSelectedType(type);
  };

  const handleQuickSelect = (payload: Record<string, unknown>) => {
    if (!selectedType) return;
    if (NEEDS_ADMIN_CONFIRMATION.includes(selectedType)) {
      setPendingSubmit({ eventType: selectedType, payload });
      return;
    }
    submit(selectedType, payload);
  };

  const handleEndSleep = async (quality: string) => {
    if (!inProgressSleepEventId) return;
    setSubmitting(true);
    try {
      const updated = await endSleepEvent(inProgressSleepEventId, new Date().toISOString(), quality, isConnected);
      onEventRecorded(updated ?? ({ id: inProgressSleepEventId } as ChildEventResponse));
      close();
    } finally {
      setSubmitting(false);
    }
  };

  if (pendingSubmit) {
    return (
      <Modal transparent visible animationType="fade" onRequestClose={() => setPendingSubmit(null)}>
        <AdministratorConfirmation
          onComplete={(administeredByStaffProfileId) => submit(pendingSubmit.eventType, pendingSubmit.payload, administeredByStaffProfileId)}
        />
      </Modal>
    );
  }

  return (
    <Modal transparent visible={visible} animationType="slide" onRequestClose={close}>
      <Pressable className="flex-1 justify-end bg-black/50" onPress={close}>
        <Pressable
          onPress={(e) => e.stopPropagation()}
          className="bg-surface dark:bg-surface-dark rounded-t-xl"
          style={{ maxHeight: "80%" }}
        >
          <View className="items-center pt-3 pb-1">
            <View style={{ width: 40, height: 4, borderRadius: 2 }} className="bg-border dark:bg-border-dark" />
          </View>

          <View className="flex-row items-center justify-between px-4 pt-2 pb-3">
            <Text className="text-text dark:text-text-dark text-lg font-bold">{t("childEvents.quickActionTitle")}</Text>
            <TouchableOpacity onPress={close} style={{ minWidth: 48, minHeight: 48 }} className="items-center justify-center">
              <X size={24} strokeWidth={2} color={colors.textSoft} />
            </TouchableOpacity>
          </View>

          <ScrollView contentContainerStyle={{ padding: 16 }}>
            {submitting ? (
              <ActivityIndicator size="large" color={colors.primary} style={{ marginVertical: 24 }} />
            ) : selectedType === null ? (
              <View className="flex-row flex-wrap" style={{ gap: 12 }}>
                {EVENT_TYPES.map(({ type, Icon }) => (
                  <TouchableOpacity
                    key={type}
                    onPress={() => handleSelectType(type)}
                    style={{ width: 88, height: 88 }}
                    className="items-center justify-center rounded-xl bg-surface-soft dark:bg-surface-soft-dark active:opacity-60"
                  >
                    <Icon size={24} strokeWidth={2} color={colors.text} />
                    <Text className="text-text dark:text-text-dark text-xs font-medium mt-2 text-center">
                      {t(`childEvents.types.${type}`)}
                    </Text>
                  </TouchableOpacity>
                ))}
              </View>
            ) : selectedType === "sleep" && inProgressSleepEventId ? (
              <View>
                <Text className="text-text dark:text-text-dark font-semibold mb-3">{t("childEvents.endNap")}</Text>
                <View className="flex-row" style={{ gap: 12 }}>
                  {(["good", "okay", "restless"] as const).map((quality) => (
                    <TouchableOpacity
                      key={quality}
                      onPress={() => handleEndSleep(quality)}
                      style={{ minHeight: 64, flex: 1 }}
                      className="items-center justify-center rounded-xl bg-surface-soft dark:bg-surface-soft-dark active:opacity-60"
                    >
                      <Text className="text-text dark:text-text-dark font-medium">{t(`childEvents.sleep.quality.${quality}`)}</Text>
                    </TouchableOpacity>
                  ))}
                </View>
              </View>
            ) : selectedType === "diaper" ? (
              <QuickChoiceRow
                choices={["wet", "dirty", "both"]}
                labelPrefix="childEvents.diaper"
                onSelect={(value) => handleQuickSelect({ type: value })}
                t={t}
              />
            ) : selectedType === "mood" ? (
              <QuickChoiceRow
                choices={["great", "good", "okay", "difficult"]}
                labelPrefix="childEvents.mood"
                onSelect={(value) => handleQuickSelect({ value })}
                t={t}
              />
            ) : selectedType === "feeding_bottle" ? (
              <QuickChoiceRow
                choices={["60", "90", "120", "150", "180"]}
                labelPrefix={null}
                onSelect={(ml) => handleQuickSelect({ ml: Number(ml) })}
                t={t}
              />
            ) : selectedType === "temperature" ? (
              <NumericEntry
                labelKey="childEvents.temperature.celsius"
                value={numericFields.celsius ?? ""}
                onChange={(v) => setNumericFields((f) => ({ ...f, celsius: v }))}
                onSubmit={() => handleQuickSelect({ celsius: Number(numericFields.celsius) })}
                t={t}
              />
            ) : selectedType === "weight" ? (
              <NumericEntry
                labelKey="childEvents.weight.kg"
                value={numericFields.kg ?? ""}
                onChange={(v) => setNumericFields((f) => ({ ...f, kg: v }))}
                onSubmit={() => handleQuickSelect({ kg: Number(numericFields.kg) })}
                t={t}
              />
            ) : selectedType === "growth_check" ? (
              <GrowthCheckForm
                values={numericFields}
                onChange={(field, v) => setNumericFields((f) => ({ ...f, [field]: v }))}
                onSubmit={() => {
                  const payload: Record<string, unknown> = {};
                  for (const field of ["weightKg", "heightCm", "headCm"] as const) {
                    if (numericFields[field]) payload[field] = Number(numericFields[field]);
                  }
                  handleQuickSelect(payload);
                }}
                t={t}
              />
            ) : selectedType === "medication" ? (
              <MedicationForm onSubmit={handleQuickSelect} t={t} />
            ) : selectedType === "custom" ? (
              <CustomForm onSubmit={handleQuickSelect} t={t} />
            ) : (
              <View>
                <TextInput
                  multiline
                  value={freeText}
                  onChangeText={setFreeText}
                  className="bg-surface-soft dark:bg-surface-soft-dark rounded-lg p-3 text-text dark:text-text-dark"
                  style={{ minHeight: 96 }}
                />
                <TouchableOpacity
                  onPress={() => handleQuickSelect({ [FREE_TEXT_FIELD[selectedType]!]: freeText })}
                  style={{ minHeight: 48, marginTop: 12 }}
                  className="items-center justify-center rounded-lg bg-primary dark:bg-primary-dark"
                >
                  <Text className="text-white font-semibold">{t("childEvents.save")}</Text>
                </TouchableOpacity>
              </View>
            )}
          </ScrollView>
        </Pressable>
      </Pressable>
    </Modal>
  );
}

function QuickChoiceRow({
  choices, labelPrefix, onSelect, t,
}: {
  choices: string[];
  labelPrefix: string | null;
  onSelect: (value: string) => void;
  t: TFunction;
}) {
  return (
    <View className="flex-row flex-wrap" style={{ gap: 12 }}>
      {choices.map((choice) => (
        <TouchableOpacity
          key={choice}
          onPress={() => onSelect(choice)}
          style={{ minHeight: 64, minWidth: 72 }}
          className="items-center justify-center flex-1 rounded-xl bg-surface-soft dark:bg-surface-soft-dark active:opacity-60"
        >
          <Text className="text-text dark:text-text-dark font-medium">{labelPrefix ? t(`${labelPrefix}.${choice}`) : choice}</Text>
        </TouchableOpacity>
      ))}
    </View>
  );
}

function NumericEntry({
  labelKey, value, onChange, onSubmit, t,
}: {
  labelKey: string;
  value: string;
  onChange: (v: string) => void;
  onSubmit: () => void;
  t: TFunction;
}) {
  return (
    <View>
      <Text className="text-text-soft dark:text-text-soft-dark text-sm mb-1">{t(labelKey)}</Text>
      <TextInput
        keyboardType="decimal-pad"
        value={value}
        onChangeText={onChange}
        className="bg-surface-soft dark:bg-surface-soft-dark rounded-lg px-3 text-text dark:text-text-dark mb-3"
        style={{ minHeight: 48 }}
      />
      <TouchableOpacity
        onPress={onSubmit}
        disabled={!value}
        style={{ minHeight: 48 }}
        className={`items-center justify-center rounded-lg ${value ? "bg-primary dark:bg-primary-dark" : "bg-border dark:bg-border-dark"}`}
      >
        <Text className="text-white font-semibold">{t("childEvents.save")}</Text>
      </TouchableOpacity>
    </View>
  );
}

function GrowthCheckForm({
  values, onChange, onSubmit, t,
}: {
  values: Record<string, string>;
  onChange: (field: "weightKg" | "heightCm" | "headCm", v: string) => void;
  onSubmit: () => void;
  t: TFunction;
}) {
  const hasAnyValue = !!(values.weightKg || values.heightCm || values.headCm);
  return (
    <View>
      {(["weightKg", "heightCm", "headCm"] as const).map((field) => (
        <View key={field} className="mb-3">
          <Text className="text-text-soft dark:text-text-soft-dark text-sm mb-1">{t(`childEvents.growthCheck.${field}`)}</Text>
          <TextInput
            keyboardType="decimal-pad"
            value={values[field] ?? ""}
            onChangeText={(v) => onChange(field, v)}
            className="bg-surface-soft dark:bg-surface-soft-dark rounded-lg px-3 text-text dark:text-text-dark"
            style={{ minHeight: 48 }}
          />
        </View>
      ))}
      <TouchableOpacity
        onPress={onSubmit}
        disabled={!hasAnyValue}
        style={{ minHeight: 48 }}
        className={`items-center justify-center rounded-lg ${hasAnyValue ? "bg-primary dark:bg-primary-dark" : "bg-border dark:bg-border-dark"}`}
      >
        <Text className="text-white font-semibold">{t("childEvents.save")}</Text>
      </TouchableOpacity>
    </View>
  );
}

const MEDICATION_NAMES = ["perdolan", "nurofen", "antibiotics", "other"] as const;

function MedicationForm({ onSubmit, t }: { onSubmit: (payload: Record<string, unknown>) => void; t: TFunction }) {
  const [name, setName] = useState<string | null>(null);
  const [doseDescription, setDoseDescription] = useState("");
  const [reason, setReason] = useState("");

  const canSubmit = !!name && !!doseDescription && !!reason;

  return (
    <View>
      <Text className="text-text-soft dark:text-text-soft-dark text-sm mb-1">{t("childEvents.types.medication")}</Text>
      <View className="flex-row flex-wrap mb-3" style={{ gap: 8 }}>
        {MEDICATION_NAMES.map((option) => (
          <TouchableOpacity
            key={option}
            onPress={() => setName(option)}
            style={{ minHeight: 48 }}
            className={`px-4 items-center justify-center rounded-lg ${name === option ? "bg-primary-soft dark:bg-primary-soft-dark" : "bg-surface-soft dark:bg-surface-soft-dark"}`}
          >
            <Text className="text-text dark:text-text-dark font-medium">{t(`childEvents.medication.name.${option}`)}</Text>
          </TouchableOpacity>
        ))}
      </View>

      <Text className="text-text-soft dark:text-text-soft-dark text-sm mb-1">{t("childEvents.medication.doseDescription")}</Text>
      <TextInput
        value={doseDescription}
        onChangeText={setDoseDescription}
        className="bg-surface-soft dark:bg-surface-soft-dark rounded-lg px-3 text-text dark:text-text-dark mb-3"
        style={{ minHeight: 48 }}
      />

      <Text className="text-text-soft dark:text-text-soft-dark text-sm mb-1">{t("childEvents.medication.reason")}</Text>
      <TextInput
        value={reason}
        onChangeText={setReason}
        className="bg-surface-soft dark:bg-surface-soft-dark rounded-lg px-3 text-text dark:text-text-dark mb-3"
        style={{ minHeight: 48 }}
      />

      <TouchableOpacity
        onPress={() => onSubmit({ name, doseDescription, reason })}
        disabled={!canSubmit}
        style={{ minHeight: 48 }}
        className={`items-center justify-center rounded-lg ${canSubmit ? "bg-primary dark:bg-primary-dark" : "bg-border dark:bg-border-dark"}`}
      >
        <Text className="text-white font-semibold">{t("childEvents.save")}</Text>
      </TouchableOpacity>
    </View>
  );
}

const CUSTOM_LABEL_MAX_LENGTH = 100;

// feature 009a: `custom` event — a caregiver-supplied label (required, plain free text, no
// autocomplete per the 2026-07-09 clarification) plus optional detail text, distinct from
// `note`'s body-only shape.
function CustomForm({ onSubmit, t }: { onSubmit: (payload: Record<string, unknown>) => void; t: TFunction }) {
  const [label, setLabel] = useState("");
  const [text, setText] = useState("");

  const canSubmit = label.trim().length > 0 && label.length <= CUSTOM_LABEL_MAX_LENGTH;

  return (
    <View>
      <Text className="text-text-soft dark:text-text-soft-dark text-sm mb-1">{t("childEvents.custom.label")}</Text>
      <TextInput
        value={label}
        onChangeText={setLabel}
        maxLength={CUSTOM_LABEL_MAX_LENGTH}
        placeholder={t("childEvents.custom.labelPlaceholder")}
        className="bg-surface-soft dark:bg-surface-soft-dark rounded-lg px-3 text-text dark:text-text-dark mb-3"
        style={{ minHeight: 48 }}
      />

      <Text className="text-text-soft dark:text-text-soft-dark text-sm mb-1">{t("childEvents.custom.text")}</Text>
      <TextInput
        multiline
        value={text}
        onChangeText={setText}
        className="bg-surface-soft dark:bg-surface-soft-dark rounded-lg p-3 text-text dark:text-text-dark mb-3"
        style={{ minHeight: 96 }}
      />

      <TouchableOpacity
        onPress={() => onSubmit(text.trim() ? { label: label.trim(), text: text.trim() } : { label: label.trim() })}
        disabled={!canSubmit}
        style={{ minHeight: 48 }}
        className={`items-center justify-center rounded-lg ${canSubmit ? "bg-primary dark:bg-primary-dark" : "bg-border dark:bg-border-dark"}`}
      >
        <Text className="text-white font-semibold">{t("childEvents.save")}</Text>
      </TouchableOpacity>
    </View>
  );
}
