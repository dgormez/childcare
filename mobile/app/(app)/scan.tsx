import React, { useCallback, useRef, useState } from "react";
import { View, Text, TouchableOpacity, ActivityIndicator, Image } from "react-native";
import { CameraView, useCameraPermissions, type BarcodeScanningResult } from "expo-camera";
import { useRouter } from "expo-router";
import { useTranslation } from "react-i18next";
import { CheckCircle2, WifiOff, CameraOff, X } from "lucide-react-native";
import { scanCheckInCode } from "../../services/attendance";
import { useColors } from "../../hooks/useColors";
import { useNetworkStatus } from "../../hooks/useNetworkStatus";
import type { VerifyCheckInCodeResponse } from "../../types";

type ErrorReason = "wrongLocation" | "expired" | "invalid" | "cooldown";

const ERROR_KEY_TO_REASON: Record<string, ErrorReason> = {
  "errors.qrCheckIn.wrong_location": "wrongLocation",
  "errors.qrCheckIn.code_expired": "expired",
  "errors.qrCheckIn.invalid_code": "invalid",
  "errors.qrCheckIn.already_used": "cooldown",
};

// User Story 2: a successful scan's confirmation, then the "already processed" cooldown state,
// auto-return to scan mode after a brief pause — the caregiver never has to tap anything to
// resume scanning the next child.
const RETURN_TO_SCAN_DELAY_MS = 1500;

/**
 * Feature 021, User Story 2 — the caregiver-tablet scan-mode screen. FR-013: a camera-permission
 * failure surfaces a fallback with a path back to manual tap rather than a dead end.
 * research.md R6: fully offline never attempts a scan — the tablet doesn't have the server's
 * signing key, so verification is structurally impossible without connectivity.
 */
export default function ScanScreen() {
  const { t } = useTranslation();
  const router = useRouter();
  const colors = useColors();
  const { isConnected } = useNetworkStatus();
  const [permission, requestPermission] = useCameraPermissions();

  const [result, setResult] = useState<VerifyCheckInCodeResponse | null>(null);
  const [errorReason, setErrorReason] = useState<ErrorReason | null>(null);
  const processingRef = useRef(false);

  const resumeScanning = useCallback(() => {
    setResult(null);
    setErrorReason(null);
    processingRef.current = false;
  }, []);

  const handleBarcodeScanned = useCallback(async ({ data }: BarcodeScanningResult) => {
    if (processingRef.current) return; // FR-019-adjacent client-side debounce: ignore re-fires for the same frame
    processingRef.current = true;

    try {
      const response = await scanCheckInCode(data);
      setResult(response);
      setTimeout(resumeScanning, RETURN_TO_SCAN_DELAY_MS);
    } catch (err) {
      const message = err instanceof Error ? err.message : "";
      setErrorReason(ERROR_KEY_TO_REASON[message] ?? "invalid");
      setTimeout(resumeScanning, RETURN_TO_SCAN_DELAY_MS);
    }
  }, [resumeScanning]);

  if (!isConnected) {
    return (
      <View style={{ flex: 1, backgroundColor: colors.background, alignItems: "center", justifyContent: "center", padding: 24 }}>
        <WifiOff color={colors.textSoft} size={24} strokeWidth={2} />
        <Text className="text-text-soft dark:text-text-soft-dark text-sm text-center mt-3 mb-6">
          {t("qrCheckIn.offlineUseManual")}
        </Text>
        <TouchableOpacity
          onPress={() => router.back()}
          className="flex-row items-center bg-surface-soft dark:bg-surface-soft-dark rounded-lg px-4"
          style={{ minHeight: 48 }}
        >
          <Text className="text-text dark:text-text-dark font-medium">{t("qrCheckIn.useManualInstead")}</Text>
        </TouchableOpacity>
      </View>
    );
  }

  if (!permission || !permission.granted) {
    return (
      <View style={{ flex: 1, backgroundColor: colors.background, alignItems: "center", justifyContent: "center", padding: 24 }}>
        <CameraOff color={colors.textSoft} size={24} strokeWidth={2} />
        <Text className="text-text-soft dark:text-text-soft-dark text-sm text-center mt-3 mb-6">
          {t("qrCheckIn.cameraUnavailable")}
        </Text>
        {permission?.canAskAgain !== false && (
          <TouchableOpacity
            onPress={requestPermission}
            className="flex-row items-center bg-primary rounded-lg px-4 mb-3"
            style={{ minHeight: 48 }}
          >
            <Text className="text-white font-medium">{t("qrCheckIn.scanAction")}</Text>
          </TouchableOpacity>
        )}
        <TouchableOpacity
          onPress={() => router.back()}
          className="flex-row items-center bg-surface-soft dark:bg-surface-soft-dark rounded-lg px-4"
          style={{ minHeight: 48 }}
        >
          <Text className="text-text dark:text-text-dark font-medium">{t("qrCheckIn.useManualInstead")}</Text>
        </TouchableOpacity>
      </View>
    );
  }

  return (
    <View style={{ flex: 1, backgroundColor: "#000000" }}>
      <CameraView
        style={{ flex: 1 }}
        facing="back"
        barcodeScannerSettings={{ barcodeTypes: ["qr"] }}
        onBarcodeScanned={result || errorReason ? undefined : handleBarcodeScanned}
      />

      <TouchableOpacity
        accessibilityLabel={t("qrCheckIn.useManualInstead")}
        onPress={() => router.back()}
        className="absolute"
        style={{ top: 48, right: 16, minWidth: 48, minHeight: 48, alignItems: "center", justifyContent: "center" }}
      >
        <X color="#ffffff" size={24} strokeWidth={2} />
      </TouchableOpacity>

      {!result && !errorReason && (
        <View className="absolute" style={{ bottom: 48, left: 24, right: 24, alignItems: "center" }}>
          <Text className="text-white text-sm text-center">{t("qrCheckIn.scanInstructions")}</Text>
        </View>
      )}

      {processingRef.current && !result && !errorReason && (
        <View className="absolute" style={{ top: "50%", left: 0, right: 0, alignItems: "center" }}>
          <ActivityIndicator size="large" color="#ffffff" />
        </View>
      )}

      {result && (
        <View
          testID="scan-confirmation"
          className="absolute bg-black/80"
          style={{ top: 0, left: 0, right: 0, bottom: 0, alignItems: "center", justifyContent: "center" }}
        >
          {result.childPhotoDownloadUrl ? (
            <Image
              source={{ uri: result.childPhotoDownloadUrl }}
              style={{ width: 96, height: 96, borderRadius: 48, marginBottom: 16 }}
            />
          ) : (
            <View style={{ width: 96, height: 96, borderRadius: 48, marginBottom: 16, backgroundColor: colors.border }} />
          )}
          <CheckCircle2 color={colors.success} size={24} strokeWidth={2} />
          <Text className="text-white text-lg font-bold text-center mt-4">
            {t(result.direction === "check-in" ? "qrCheckIn.checkedIn" : "qrCheckIn.checkedOut", {
              name: `${result.childFirstName} ${result.childLastName}`,
            })}
          </Text>
        </View>
      )}

      {errorReason && (
        <View
          testID="scan-error"
          className="absolute bg-black/80"
          style={{ top: 0, left: 0, right: 0, bottom: 0, alignItems: "center", justifyContent: "center", paddingHorizontal: 32 }}
        >
          <Text className="text-white text-base text-center">
            {t(`qrCheckIn.errors.${errorReason}`)}
          </Text>
        </View>
      )}
    </View>
  );
}

// Named export for the Jest fallback-state test (T040a) to exercise permission handling
// deterministically without mounting the full camera stack.
export { ERROR_KEY_TO_REASON };
