import React, { useCallback, useEffect, useRef, useState } from "react";
import { View, Text, ActivityIndicator, TouchableOpacity } from "react-native";
import { useLocalSearchParams } from "expo-router";
import { useTranslation } from "react-i18next";
import * as Network from "expo-network";
import QRCode from "react-native-qrcode-svg";
import { WifiOff, RefreshCw } from "lucide-react-native";
import { requestQrCode } from "../../../services/attendance";
import { useColors } from "../../../hooks/useColors";
import { useIsOffline } from "../../../hooks/useIsOffline";
import { ScreenContainer } from "../../../components/ScreenContainer";

// FR-006: refresh at the ~20s mark, comfortably before the code's own 30s expiry, so a code is
// essentially never shown expired under normal use.
const REFRESH_AFTER_MS = 20_000;

export default function QrCheckInScreen() {
  const { childId, name } = useLocalSearchParams<{ childId: string; name?: string }>();
  const { t } = useTranslation();
  const colors = useColors();

  const [code, setCode] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);
  const isOffline = useIsOffline(refreshKey);
  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const load = useCallback(async () => {
    setError(false);
    // useIsOffline's own connectivity check resolves asynchronously (starts optimistic/online
    // on first render), so a mount-time load() could otherwise fire one issuance request before
    // that check catches up — re-check connectivity directly, right before calling, so a fully
    // offline screen never attempts one (research.md R6).
    const state = await Network.getNetworkStateAsync();
    if (state.isConnected === false || state.isInternetReachable === false) return;
    try {
      const issued = await requestQrCode(childId);
      setCode(issued.code);
      timeoutRef.current = setTimeout(() => setRefreshKey((k) => k + 1), REFRESH_AFTER_MS);
    } catch {
      setError(true);
    }
  }, [childId]);

  useEffect(() => {
    if (isOffline) return;
    setLoading(true);
    load().finally(() => setLoading(false));
    return () => {
      if (timeoutRef.current) clearTimeout(timeoutRef.current);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [refreshKey, isOffline]);

  return (
    <ScreenContainer>
      <View className="flex-1 bg-background dark:bg-background-dark items-center" style={{ paddingTop: 48, paddingHorizontal: 24 }}>
        <Text className="text-text dark:text-text-dark text-lg font-bold text-center">
          {t("qrCheckIn.title", { name: name ?? "" })}
        </Text>
        <Text className="text-text-soft dark:text-text-soft-dark text-sm text-center mt-2 mb-8">
          {t("qrCheckIn.instructions")}
        </Text>

        {isOffline && (
          <View className="items-center" style={{ paddingVertical: 48 }}>
            <WifiOff color={colors.textSoft} size={24} strokeWidth={2} />
            <Text className="text-text-soft dark:text-text-soft-dark text-sm text-center mt-3">
              {t("qrCheckIn.offline")}
            </Text>
          </View>
        )}

        {!isOffline && loading && (
          <View style={{ paddingVertical: 48 }}>
            <ActivityIndicator size="large" color={colors.primary} />
            <Text className="text-text-soft dark:text-text-soft-dark text-sm text-center mt-3">
              {t("qrCheckIn.loading")}
            </Text>
          </View>
        )}

        {!isOffline && !loading && error && (
          <View className="items-center" style={{ paddingVertical: 24 }}>
            <Text className="text-danger dark:text-danger-dark text-sm text-center mb-4">
              {t("qrCheckIn.issueFailed")}
            </Text>
            <TouchableOpacity
              onPress={() => setRefreshKey((k) => k + 1)}
              className="flex-row items-center bg-surface-soft dark:bg-surface-soft-dark rounded-lg px-4"
              style={{ minHeight: 48 }}
            >
              <RefreshCw color={colors.text} size={20} strokeWidth={2} />
              <Text className="text-text dark:text-text-dark ml-2 font-medium">{t("qrCheckIn.retry")}</Text>
            </TouchableOpacity>
          </View>
        )}

        {!isOffline && !loading && !error && code && (
          <View className="bg-surface dark:bg-surface-dark rounded-xl p-6">
            <QRCode value={code} size={220} color={colors.text} backgroundColor="transparent" />
          </View>
        )}
      </View>
    </ScreenContainer>
  );
}
