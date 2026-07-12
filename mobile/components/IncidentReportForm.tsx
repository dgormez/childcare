import React, { useState } from "react";
import { Modal, View, Text, TextInput, TouchableOpacity, Pressable, ScrollView, Switch } from "react-native";
import { useTranslation } from "react-i18next";
import { fileIncidentReport } from "../services/incidentReports";
import type { IncidentReportResponse } from "../types";

interface Props {
  visible: boolean;
  childId: string;
  isConnected: boolean;
  onClose: () => void;
  onSaved: (report: IncidentReportResponse) => void;
}

const INJURY_TYPES = ["none", "scrape", "bump", "cut", "fall", "bite", "burn", "allergic_reaction", "other"];
// data-model.md: LocationDetail is free text — these three are quick-select shortcuts, not an
// exhaustive enum, so a free-text field always coexists alongside them (not gated behind a 4th
// "other" chip).
const LOCATION_DETAILS = ["indoor", "outdoor", "transit"];
const PARENT_NOTIFIED_HOWS = ["phone", "app", "in_person"];

/**
 * User Story 1 — "Incident melden": a caregiver files an incident report from the child's
 * profile. Only description + injuryType are required (FR-002); every other field is
 * independently optional regardless of injuryType (Acceptance Scenario 5). `reportedBy` is
 * resolved server-side (FR-004) — never collected here.
 */
export function IncidentReportForm({ visible, childId, isConnected, onClose, onSaved }: Props) {
  const { t } = useTranslation();

  const [description, setDescription] = useState("");
  const [injuryType, setInjuryType] = useState<string | null>(null);
  const [locationDetail, setLocationDetail] = useState<string | null>(null);
  const [customLocationDetail, setCustomLocationDetail] = useState("");
  const [firstAidGiven, setFirstAidGiven] = useState("");
  const [doctorCalled, setDoctorCalled] = useState(false);
  const [doctorNotes, setDoctorNotes] = useState("");
  const [parentNotified, setParentNotified] = useState(false);
  const [parentNotifiedHow, setParentNotifiedHow] = useState<string | null>(null);
  const [witnesses, setWitnesses] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  if (!visible) return null;

  const reset = () => {
    setDescription("");
    setInjuryType(null);
    setLocationDetail(null);
    setCustomLocationDetail("");
    setFirstAidGiven("");
    setDoctorCalled(false);
    setDoctorNotes("");
    setParentNotified(false);
    setParentNotifiedHow(null);
    setWitnesses("");
    setError(null);
  };

  const handleClose = () => {
    reset();
    onClose();
  };

  const handleSubmit = async () => {
    if (!description.trim()) {
      setError(t("incidentReports.validation.descriptionRequired"));
      return;
    }
    if (!injuryType) {
      setError(t("incidentReports.validation.injuryTypeRequired"));
      return;
    }

    setSubmitting(true);
    setError(null);
    try {
      const report = await fileIncidentReport(
        {
          childId,
          occurredAt: new Date().toISOString(),
          locationDetail: customLocationDetail.trim() || locationDetail,
          description: description.trim(),
          injuryType,
          firstAidGiven: firstAidGiven.trim() || null,
          doctorCalled,
          doctorNotes: doctorNotes.trim() || null,
          parentNotified,
          parentNotifiedAt: parentNotified ? new Date().toISOString() : null,
          parentNotifiedHow: parentNotified ? parentNotifiedHow : null,
          witnesses: witnesses.trim() || null,
        },
        isConnected
      );
      onSaved(report);
      reset();
    } catch (e) {
      setError(e instanceof Error ? t(e.message) : t("incidentReports.submitError"));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Modal transparent visible animationType="slide" onRequestClose={handleClose}>
      <Pressable className="flex-1 justify-end bg-black/60" onPress={handleClose}>
        <Pressable onPress={(e) => e.stopPropagation()} className="bg-surface dark:bg-surface-dark rounded-t-2xl" style={{ maxHeight: "88%" }}>
          <ScrollView contentContainerStyle={{ padding: 16 }} keyboardShouldPersistTaps="handled">
            <Text className="text-text dark:text-text-dark text-lg font-bold mb-4">{t("incidentReports.title")}</Text>

            <Field label={t("incidentReports.description")}>
              <TextInput
                value={description}
                onChangeText={setDescription}
                placeholder={t("incidentReports.descriptionPlaceholder")}
                multiline
                className="bg-surface-soft dark:bg-surface-soft-dark rounded-lg px-3 py-3 text-text dark:text-text-dark"
                style={{ minHeight: 80, textAlignVertical: "top" }}
              />
            </Field>

            <Field label={t("incidentReports.injuryType")}>
              <ChipRow
                options={INJURY_TYPES}
                selected={injuryType}
                onSelect={setInjuryType}
                labelFor={(v) => t(`incidentReports.injuryTypes.${v}`)}
              />
            </Field>

            <Field label={t("incidentReports.locationDetail")}>
              <ChipRow
                options={LOCATION_DETAILS}
                selected={locationDetail}
                onSelect={(v) => {
                  setLocationDetail(v);
                  setCustomLocationDetail("");
                }}
                labelFor={(v) => t(`incidentReports.locationDetails.${v}`)}
              />
              <TextInput
                value={customLocationDetail}
                onChangeText={(v) => {
                  setCustomLocationDetail(v);
                  if (v) setLocationDetail(null);
                }}
                placeholder={t("incidentReports.locationDetailPlaceholder")}
                className="bg-surface-soft dark:bg-surface-soft-dark rounded-lg px-3 mt-3 text-text dark:text-text-dark"
                style={{ minHeight: 48 }}
              />
            </Field>

            <Field label={t("incidentReports.firstAidGiven")}>
              <TextInput
                value={firstAidGiven}
                onChangeText={setFirstAidGiven}
                placeholder={t("incidentReports.firstAidGivenPlaceholder")}
                className="bg-surface-soft dark:bg-surface-soft-dark rounded-lg px-3 text-text dark:text-text-dark"
                style={{ minHeight: 48 }}
              />
            </Field>

            <ToggleRow label={t("incidentReports.doctorCalled")} value={doctorCalled} onChange={setDoctorCalled} />
            {doctorCalled && (
              <Field label={t("incidentReports.doctorNotes")}>
                <TextInput
                  value={doctorNotes}
                  onChangeText={setDoctorNotes}
                  placeholder={t("incidentReports.doctorNotesPlaceholder")}
                  className="bg-surface-soft dark:bg-surface-soft-dark rounded-lg px-3 text-text dark:text-text-dark"
                  style={{ minHeight: 48 }}
                />
              </Field>
            )}

            <ToggleRow label={t("incidentReports.parentNotified")} value={parentNotified} onChange={setParentNotified} />
            {parentNotified && (
              <Field label={t("incidentReports.parentNotifiedHow")}>
                <ChipRow
                  options={PARENT_NOTIFIED_HOWS}
                  selected={parentNotifiedHow}
                  onSelect={setParentNotifiedHow}
                  labelFor={(v) => t(`incidentReports.parentNotifiedHows.${v}`)}
                />
              </Field>
            )}

            <Field label={t("incidentReports.witnesses")}>
              <TextInput
                value={witnesses}
                onChangeText={setWitnesses}
                placeholder={t("incidentReports.witnessesPlaceholder")}
                className="bg-surface-soft dark:bg-surface-soft-dark rounded-lg px-3 text-text dark:text-text-dark"
                style={{ minHeight: 48 }}
              />
            </Field>

            {error && <Text className="text-danger dark:text-danger-dark text-sm mb-3">{error}</Text>}

            <View className="flex-row justify-end mt-2" style={{ gap: 16 }}>
              <TouchableOpacity onPress={handleClose} style={{ minHeight: 48 }} className="items-center justify-center px-4">
                <Text className="text-text-soft dark:text-text-soft-dark font-medium">{t("incidentReports.cancel")}</Text>
              </TouchableOpacity>
              <TouchableOpacity onPress={handleSubmit} disabled={submitting} style={{ minHeight: 48 }} className="items-center justify-center px-4">
                <Text className="text-primary-hover dark:text-primary-hover-dark font-semibold">{t("incidentReports.submit")}</Text>
              </TouchableOpacity>
            </View>
          </ScrollView>
        </Pressable>
      </Pressable>
    </Modal>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <View className="mb-4">
      <Text className="text-text-soft dark:text-text-soft-dark text-sm mb-1">{label}</Text>
      {children}
    </View>
  );
}

function ToggleRow({ label, value, onChange }: { label: string; value: boolean; onChange: (v: boolean) => void }) {
  return (
    <View className="flex-row items-center justify-between mb-4" style={{ minHeight: 48 }}>
      <Text className="text-text dark:text-text-dark">{label}</Text>
      <Switch value={value} onValueChange={onChange} />
    </View>
  );
}

function ChipRow({
  options, selected, onSelect, labelFor,
}: {
  options: string[];
  selected: string | null;
  onSelect: (value: string) => void;
  labelFor: (value: string) => string;
}) {
  return (
    <View className="flex-row flex-wrap" style={{ gap: 8 }}>
      {options.map((option) => {
        const isSelected = selected === option;
        return (
          <TouchableOpacity
            key={option}
            onPress={() => onSelect(option)}
            style={{ minHeight: 48, paddingHorizontal: 16 }}
            className={
              isSelected
                ? "items-center justify-center rounded-full bg-primary dark:bg-primary-dark active:opacity-60"
                : "items-center justify-center rounded-full bg-surface-soft dark:bg-surface-soft-dark active:opacity-60"
            }
          >
            <Text className={isSelected ? "text-white font-medium" : "text-text dark:text-text-dark font-medium"}>
              {labelFor(option)}
            </Text>
          </TouchableOpacity>
        );
      })}
    </View>
  );
}
