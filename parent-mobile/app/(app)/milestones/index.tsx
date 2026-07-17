import React, { useCallback, useEffect, useState } from "react";
import { View, Text, ScrollView, RefreshControl, ActivityIndicator, TouchableOpacity } from "react-native";
import { useTranslation } from "react-i18next";
import * as Sharing from "expo-sharing";
import { CheckCircle2, Sparkles, Circle, Download, Sprout } from "lucide-react-native";
import { apiClient } from "../../../services/apiClient";
import { getMilestonePortfolio, downloadMilestonePortfolioPdf } from "../../../services/milestones";
import { useColors } from "../../../hooks/useColors";
import { ScreenContainer } from "../../../components/ScreenContainer";
import type { DevelopmentalDomainResponse, DevelopmentalMilestoneResponse, MilestoneObservationStatus, ParentChildResponse } from "../../../types";

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

function StatusIcon({ status, colors }: { status: MilestoneObservationStatus | null; colors: ReturnType<typeof useColors> }) {
  if (status === "achieved") return <CheckCircle2 size={16} strokeWidth={2} color={colors.success} />;
  if (status === "emerging") return <Sparkles size={16} strokeWidth={2} color={colors.textSoft} />;
  return <Circle size={16} strokeWidth={2} color={colors.textSoft} />;
}

interface ChildPortfolioSectionProps {
  child: ParentChildResponse;
  domains: DevelopmentalDomainResponse[];
}

function ChildPortfolioSection({ child, domains }: ChildPortfolioSectionProps) {
  const { t, i18n } = useTranslation();
  const colors = useColors();
  const [downloading, setDownloading] = useState(false);
  const [downloadError, setDownloadError] = useState("");

  const hasAnyMilestones = domains.some((d) => d.milestones.length > 0);

  async function handleDownload() {
    setDownloading(true);
    setDownloadError("");
    try {
      const file = await downloadMilestonePortfolioPdf(child.id);
      if (await Sharing.isAvailableAsync()) {
        await Sharing.shareAsync(file.uri, { UTI: "com.adobe.pdf", mimeType: "application/pdf" });
      }
    } catch {
      setDownloadError(t("milestones.downloadFailed"));
    } finally {
      setDownloading(false);
    }
  }

  return (
    <View className="mb-6">
      <View className="flex-row items-center justify-between mb-3">
        <Text className="text-text dark:text-text-dark text-lg font-bold">{child.firstName}</Text>
        <TouchableOpacity onPress={handleDownload} disabled={downloading} className="flex-row items-center" style={{ minHeight: 48 }}>
          {downloading ? (
            <ActivityIndicator size="small" color={colors.primary} />
          ) : (
            <>
              <Download color={colors.primaryHover} size={16} strokeWidth={2} />
              <Text className="text-primary-hover dark:text-primary-hover-dark text-sm ml-1">{t("milestones.downloadPdf")}</Text>
            </>
          )}
        </TouchableOpacity>
      </View>

      {!!downloadError && <Text className="text-danger dark:text-danger-dark text-sm mb-2">{downloadError}</Text>}

      {!hasAnyMilestones ? (
        <View className="items-center" style={{ paddingVertical: 24 }}>
          <Sprout color={colors.textSoft} size={24} strokeWidth={2} />
          <Text className="text-text-soft dark:text-text-soft-dark text-sm text-center mt-2">{t("milestones.emptyState")}</Text>
        </View>
      ) : (
        domains.map((domain) => (
          <View key={domain.id} className="mb-4">
            <Text className="text-text dark:text-text-dark font-semibold text-sm mb-2">{localizedName(domain, i18n.language)}</Text>
            {domain.milestones.map((milestone: DevelopmentalMilestoneResponse) => (
              <View
                key={milestone.id}
                className={`flex-row items-center justify-between rounded-xl px-4 mb-2 ${
                  milestone.isCurrentFocus ? "bg-primary-soft dark:bg-primary-soft-dark" : "bg-surface-soft dark:bg-surface-soft-dark"
                }`}
                style={{ minHeight: 56, paddingVertical: 16 }}
              >
                <View style={{ flex: 1 }}>
                  <Text className="text-text dark:text-text-dark text-sm">{localizedDescription(milestone, i18n.language)}</Text>
                  {milestone.isCurrentFocus && (
                    <Text className="text-primary-hover dark:text-primary-hover-dark text-xs mt-1">{t("milestones.currentFocus")}</Text>
                  )}
                </View>
                <View className="flex-row items-center" style={{ gap: 4 }}>
                  <StatusIcon status={milestone.currentStatus} colors={colors} />
                  <Text className="text-text-soft dark:text-text-soft-dark text-xs">
                    {milestone.currentStatus ? t(`milestones.status.${milestone.currentStatus}`) : t("milestones.noObservations")}
                  </Text>
                </View>
              </View>
            ))}
          </View>
        ))
      )}
    </View>
  );
}

/**
 * "Development" section (spec.md US3/US4) — warm, plain-language milestone portfolio per
 * linked child, reached only via Home's quick actions (mirrors invoices/fiscal-attestations'
 * own href:null tab registration).
 */
export default function MilestonesScreen() {
  const { t } = useTranslation();
  const colors = useColors();

  const [children, setChildren] = useState<ParentChildResponse[]>([]);
  const [portfolios, setPortfolios] = useState<Record<string, DevelopmentalDomainResponse[]>>({});
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [unavailable, setUnavailable] = useState(false);

  const load = useCallback(async () => {
    try {
      const childrenResult = await apiClient.GET("/api/parent/children");
      if (!childrenResult.response.ok) throw new Error("failed");
      const fetchedChildren = childrenResult.data as unknown as ParentChildResponse[];
      setChildren(fetchedChildren);

      const entries = await Promise.all(
        fetchedChildren.map(async (child) => {
          const result = await getMilestonePortfolio(child.id);
          return [child.id, result.status === "loaded" ? result.domains : []] as const;
        })
      );
      setPortfolios(Object.fromEntries(entries));
      setUnavailable(false);
    } catch {
      setUnavailable(true);
    }
  }, []);

  useEffect(() => {
    setLoading(true);
    load().finally(() => setLoading(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const onRefresh = async () => {
    setRefreshing(true);
    await load();
    setRefreshing(false);
  };

  if (loading) {
    return (
      <View style={{ flex: 1, backgroundColor: colors.background, alignItems: "center", justifyContent: "center" }}>
        <ActivityIndicator size="large" color={colors.primary} />
      </View>
    );
  }

  return (
    <ScreenContainer>
      <ScrollView
        className="flex-1 bg-background dark:bg-background-dark"
        contentContainerStyle={{ padding: 16 }}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={colors.primary} />}
      >
        {unavailable && (
          <View className="items-center" style={{ paddingVertical: 48 }}>
            <Sprout color={colors.textSoft} size={24} strokeWidth={2} />
            <Text className="text-text-soft dark:text-text-soft-dark text-sm text-center mt-3">{t("milestones.loadFailed")}</Text>
          </View>
        )}

        {!unavailable &&
          children.map((child) => (
            <ChildPortfolioSection key={child.id} child={child} domains={portfolios[child.id] ?? []} />
          ))}
      </ScrollView>
    </ScreenContainer>
  );
}
