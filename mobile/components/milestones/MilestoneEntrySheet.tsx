import React, { useEffect, useState } from "react";
import { Modal, View, Text, TextInput, TouchableOpacity, Pressable, ScrollView } from "react-native";
import { useTranslation } from "react-i18next";
import { ChevronLeft } from "lucide-react-native";
import { fetchDevelopmentalDomains, recordMilestoneObservation } from "../../services/milestones";
import { useColors } from "../../hooks/useColors";
import type { DevelopmentalDomainResponse, DevelopmentalMilestoneResponse, MilestoneObservationResponse, MilestoneObservationStatus } from "../../types";

interface Props {
  visible: boolean;
  childId: string;
  isConnected: boolean;
  onClose: () => void;
  onSaved: (observation: MilestoneObservationResponse, milestoneDescription: string, isPending: boolean) => void;
}

const STATUSES: MilestoneObservationStatus[] = ["achieved", "emerging", "not_yet"];

function localizedName(entry: { nameNl: string; nameFr: string; nameEn: string }, language: string): string {
  if (language.startsWith("fr")) return entry.nameFr;
  if (language.startsWith("en")) return entry.nameEn;
  return entry.nameNl;
}

function localizedDescription(entry: { descriptionNl: string; descriptionFr: string; descriptionEn: string }, language: string): string {
  if (language.startsWith("fr")) return entry.descriptionFr;
  if (language.startsWith("en")) return entry.descriptionEn;
  return entry.descriptionNl;
}

/**
 * Domain -> milestone -> status tap, matching the tablet's quick-action pattern (SC-001: 3 taps
 * or fewer). Minimal typing — the optional note is the only text field (per platform-rules.md's
 * "prefer selection over typing" for caregiver forms).
 */
export function MilestoneEntrySheet({ visible, childId, isConnected, onClose, onSaved }: Props) {
  const { t, i18n } = useTranslation();
  const colors = useColors();
  const [domains, setDomains] = useState<DevelopmentalDomainResponse[]>([]);
  const [selectedDomain, setSelectedDomain] = useState<DevelopmentalDomainResponse | null>(null);
  const [selectedMilestone, setSelectedMilestone] = useState<DevelopmentalMilestoneResponse | null>(null);
  const [status, setStatus] = useState<MilestoneObservationStatus | null>(null);
  const [notes, setNotes] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!visible) return;
    setSelectedDomain(null);
    setSelectedMilestone(null);
    setStatus(null);
    setNotes("");
    setError(null);
    fetchDevelopmentalDomains()
      .then(setDomains)
      .catch(() => setError("errors.milestones.catalog_failed"));
  }, [visible]);

  if (!visible) return null;

  const handleSave = async () => {
    if (!selectedMilestone || !status) return;
    setSaving(true);
    setError(null);
    try {
      const observation = await recordMilestoneObservation(
        {
          childId,
          milestoneId: selectedMilestone.id,
          status,
          observedAt: new Date().toISOString().slice(0, 10),
          notes: notes.trim().length > 0 ? notes.trim() : null,
        },
        isConnected
      );
      onSaved(observation, localizedDescription(selectedMilestone, i18n.language), !isConnected);
      onClose();
    } catch (e) {
      setError(e instanceof Error ? e.message : "errors.milestones.save_failed");
    } finally {
      setSaving(false);
    }
  };

  const title = selectedMilestone
    ? localizedDescription(selectedMilestone, i18n.language)
    : selectedDomain
      ? localizedName(selectedDomain, i18n.language)
      : t("milestones.entryPoint");

  return (
    <Modal transparent visible animationType="fade" onRequestClose={onClose}>
      <Pressable className="flex-1 justify-end bg-black/60" onPress={onClose}>
        <Pressable onPress={(e) => e.stopPropagation()} className="bg-surface dark:bg-surface-dark rounded-t-xl p-4" style={{ maxHeight: "80%" }}>
          <View className="flex-row items-center mb-4">
            {(selectedDomain || selectedMilestone) && (
              <TouchableOpacity
                onPress={() => (selectedMilestone ? setSelectedMilestone(null) : setSelectedDomain(null))}
                style={{ minHeight: 48, minWidth: 48 }}
                className="items-center justify-center active:opacity-60"
              >
                <ChevronLeft size={24} strokeWidth={2} color={colors.textSoft} />
              </TouchableOpacity>
            )}
            <Text className="text-text dark:text-text-dark text-lg font-bold flex-1">{title}</Text>
          </View>

          <ScrollView>
            {!selectedDomain &&
              domains.map((domain) => (
                <TouchableOpacity
                  key={domain.id}
                  onPress={() => setSelectedDomain(domain)}
                  style={{ minHeight: 48 }}
                  className="justify-center px-4 mb-2 rounded-lg bg-surface-soft dark:bg-surface-soft-dark active:opacity-60"
                >
                  <Text className="text-text dark:text-text-dark font-medium">{localizedName(domain, i18n.language)}</Text>
                </TouchableOpacity>
              ))}

            {selectedDomain && !selectedMilestone &&
              selectedDomain.milestones.map((milestone) => (
                <TouchableOpacity
                  key={milestone.id}
                  onPress={() => setSelectedMilestone(milestone)}
                  style={{ minHeight: 48 }}
                  className="justify-center px-4 mb-2 rounded-lg bg-surface-soft dark:bg-surface-soft-dark active:opacity-60"
                >
                  <Text className="text-text dark:text-text-dark">{localizedDescription(milestone, i18n.language)}</Text>
                  {milestone.isCurrentFocus && (
                    <Text className="text-primary-hover dark:text-primary-hover-dark text-xs font-medium mt-1">
                      {t("milestones.currentFocus")}
                    </Text>
                  )}
                </TouchableOpacity>
              ))}

            {selectedMilestone && (
              <>
                <View className="flex-row mb-3" style={{ gap: 8 }}>
                  {STATUSES.map((s) => (
                    <TouchableOpacity
                      key={s}
                      onPress={() => setStatus(s)}
                      style={{ minHeight: 48, flex: 1 }}
                      className={`items-center justify-center rounded-lg ${status === s ? "bg-primary dark:bg-primary-dark" : "bg-surface-soft dark:bg-surface-soft-dark"} active:opacity-60`}
                    >
                      <Text className={status === s ? "text-white font-semibold" : "text-text dark:text-text-dark"}>
                        {t(`milestones.status.${s}`)}
                      </Text>
                    </TouchableOpacity>
                  ))}
                </View>

                <TextInput
                  value={notes}
                  onChangeText={setNotes}
                  placeholder={t("milestones.notesPlaceholder") ?? undefined}
                  placeholderTextColor={colors.placeholder}
                  className="bg-surface-soft dark:bg-surface-soft-dark rounded-lg px-3 text-text dark:text-text-dark mb-3"
                  style={{ minHeight: 48 }}
                  multiline
                />

                {error && <Text className="text-danger dark:text-danger-dark text-sm mb-3">{t(error)}</Text>}

                <View className="flex-row justify-end" style={{ gap: 16 }}>
                  <TouchableOpacity onPress={onClose} style={{ minHeight: 48 }} className="items-center justify-center px-4">
                    <Text className="text-text-soft dark:text-text-soft-dark font-medium">{t("milestones.cancel")}</Text>
                  </TouchableOpacity>
                  <TouchableOpacity
                    onPress={handleSave}
                    disabled={saving || !status}
                    style={{ minHeight: 48 }}
                    className="items-center justify-center px-4"
                  >
                    <Text className="text-primary-hover dark:text-primary-hover-dark font-semibold">{t("milestones.save")}</Text>
                  </TouchableOpacity>
                </View>
              </>
            )}
          </ScrollView>
        </Pressable>
      </Pressable>
    </Modal>
  );
}
