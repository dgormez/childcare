import React, { useCallback, useEffect, useState } from "react";
import { View, Text, FlatList, TouchableOpacity, RefreshControl, ActivityIndicator } from "react-native";
import { useTranslation } from "react-i18next";
import * as Sharing from "expo-sharing";
import { FileCheck2, Download } from "lucide-react-native";
import { getFiscalAttestations, downloadFiscalAttestationPdf } from "../../../services/fiscalAttestations";
import { useColors } from "../../../hooks/useColors";
import { ScreenContainer } from "../../../components/ScreenContainer";
import type { FiscalAttestationResponse } from "../../../types";

function formatCents(cents: number): string {
  return (cents / 100).toLocaleString(undefined, { style: "currency", currency: "EUR" });
}

export default function FiscalAttestationsScreen() {
  const { t } = useTranslation();
  const colors = useColors();

  const [attestations, setAttestations] = useState<FiscalAttestationResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [unavailable, setUnavailable] = useState(false);
  const [downloadingId, setDownloadingId] = useState<string | null>(null);
  const [downloadError, setDownloadError] = useState("");

  const load = useCallback(async () => {
    const result = await getFiscalAttestations();
    if (result.status === "unavailable") {
      setUnavailable(true);
    } else {
      setUnavailable(false);
      setAttestations(result.attestations);
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

  async function handleDownload(attestation: FiscalAttestationResponse) {
    if (!attestation.id) return;
    setDownloadingId(attestation.id);
    setDownloadError("");
    try {
      const file = await downloadFiscalAttestationPdf(attestation.id);
      if (await Sharing.isAvailableAsync()) {
        await Sharing.shareAsync(file.uri, { UTI: "com.adobe.pdf", mimeType: "application/pdf" });
      }
    } catch {
      setDownloadError(t("fiscalAttestations.downloadFailed"));
    } finally {
      setDownloadingId(null);
    }
  }

  if (loading) {
    return (
      <View style={{ flex: 1, backgroundColor: colors.background, alignItems: "center", justifyContent: "center" }}>
        <ActivityIndicator size="large" color={colors.primary} />
      </View>
    );
  }

  return (
    <ScreenContainer>
      <View className="flex-1 bg-background dark:bg-background-dark">
        {unavailable && (
          <View className="items-center" style={{ paddingVertical: 48, paddingHorizontal: 24 }}>
            <FileCheck2 color={colors.textSoft} size={28} strokeWidth={2} />
            <Text className="text-text-soft dark:text-text-soft-dark text-sm text-center mt-3">{t("fiscalAttestations.loadFailed")}</Text>
          </View>
        )}

        {!unavailable && attestations.length === 0 && (
          <View className="items-center" style={{ paddingVertical: 48, paddingHorizontal: 24 }}>
            <FileCheck2 color={colors.textSoft} size={28} strokeWidth={2} />
            <Text className="text-text-soft dark:text-text-soft-dark text-sm text-center mt-3">{t("fiscalAttestations.notAvailableYet")}</Text>
          </View>
        )}

        {!!downloadError && <Text className="text-danger dark:text-danger-dark text-sm mx-4 mt-4">{downloadError}</Text>}

        {!unavailable && attestations.length > 0 && (
          <FlatList
            data={attestations}
            keyExtractor={(item) => item.id ?? `${item.childId}:${item.locationId}:${item.taxYear}`}
            contentContainerStyle={{ padding: 16 }}
            refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={colors.primary} />}
            renderItem={({ item }) => (
              <TouchableOpacity
                onPress={() => handleDownload(item)}
                disabled={downloadingId === item.id}
                className="bg-surface dark:bg-surface-dark rounded-xl px-4 mb-3"
                style={{ minHeight: 64, justifyContent: "center", paddingVertical: 12 }}
              >
                <View className="flex-row items-center justify-between">
                  <View className="flex-1">
                    <Text className="text-text dark:text-text-dark text-base font-medium" numberOfLines={1}>
                      {item.childName}
                    </Text>
                    <Text className="text-text-soft dark:text-text-soft-dark text-xs mt-1">
                      {t("fiscalAttestations.taxYear", { year: item.taxYear })} · {item.locationName}
                    </Text>
                  </View>
                  <View className="items-end">
                    {item.totalAmountCents !== null && (
                      <Text className="text-text dark:text-text-dark text-base font-semibold mb-1">
                        {formatCents(item.totalAmountCents)}
                      </Text>
                    )}
                    {downloadingId === item.id ? (
                      <ActivityIndicator size="small" color={colors.primary} />
                    ) : (
                      <View className="flex-row items-center">
                        <Download color={colors.primaryHover} size={14} strokeWidth={2} />
                        <Text className="text-primary-hover dark:text-primary-hover-dark text-xs ml-1">
                          {t("fiscalAttestations.downloadPdf")}
                        </Text>
                      </View>
                    )}
                  </View>
                </View>
              </TouchableOpacity>
            )}
          />
        )}
      </View>
    </ScreenContainer>
  );
}
