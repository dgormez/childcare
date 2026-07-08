import React, { useRef, useState } from "react";
import { View, Text, TouchableOpacity, Animated, ActivityIndicator } from "react-native";
import { useTranslation } from "react-i18next";
import { useColors } from "../hooks/useColors";

export interface PinKeypadResult {
  ok: boolean;
  errorKey?: string;
  lockedUntil?: string;
}

interface Props {
  /** Addressed by name (spec User Story 3: "Geef je code in, [Name]") — pass null for the
   * director-override keypad, which has no single-caregiver context. */
  name: string | null;
  pinLength: 4 | 6;
  onSubmit: (pin: string) => Promise<PinKeypadResult>;
  onCancel: () => void;
  onSuccess: () => void;
}

const KEYS = ["1", "2", "3", "4", "5", "6", "7", "8", "9", "", "0", "⌫"];

/**
 * Select-then-PIN confirmation overlay (spec User Story 3/FR-028) — 64pt touch targets, shake
 * animation + clear message on an incorrect PIN, a distinct message naming the cooldown when
 * locked (platform-rules.md).
 */
export function PinKeypad({ name, pinLength, onSubmit, onCancel, onSuccess }: Props) {
  const colors = useColors();
  const { t } = useTranslation();
  const [pin, setPin] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [message, setMessage] = useState<{ text: string; tone: "error" | "locked" } | null>(null);
  const shake = useRef(new Animated.Value(0)).current;

  const runShake = () => {
    shake.setValue(0);
    Animated.sequence([
      Animated.timing(shake, { toValue: 1, duration: 50, useNativeDriver: true }),
      Animated.timing(shake, { toValue: -1, duration: 50, useNativeDriver: true }),
      Animated.timing(shake, { toValue: 1, duration: 50, useNativeDriver: true }),
      Animated.timing(shake, { toValue: 0, duration: 50, useNativeDriver: true }),
    ]).start();
  };

  const submit = async (candidate: string) => {
    setSubmitting(true);
    setMessage(null);
    try {
      const result = await onSubmit(candidate);
      if (result.ok) {
        onSuccess();
        return;
      }
      runShake();
      setPin("");
      if (result.errorKey?.includes("locked")) {
        const until = result.lockedUntil ? new Date(result.lockedUntil).toLocaleTimeString() : "";
        setMessage({ text: t("pin.locked", { time: until }), tone: "locked" });
      } else if (result.errorKey === "errors.network") {
        setMessage({ text: t("offline.banner"), tone: "error" });
      } else {
        setMessage({ text: t("pin.invalid"), tone: "error" });
      }
    } finally {
      setSubmitting(false);
    }
  };

  const handleKey = (key: string) => {
    if (submitting) return;
    if (key === "⌫") {
      setPin((p) => p.slice(0, -1));
      return;
    }
    if (key === "") return;

    const next = (pin + key).slice(0, pinLength);
    setPin(next);
    if (next.length === pinLength) {
      submit(next);
    }
  };

  return (
    <View className="flex-1 bg-black/70 items-center justify-center px-6">
      <Animated.View
        style={{
          transform: [{ translateX: shake.interpolate({ inputRange: [-1, 1], outputRange: [-12, 12] }) }],
          width: "100%",
          maxWidth: 420,
        }}
        className="bg-surface dark:bg-surface-dark rounded-xl p-8"
      >
        <Text className="text-text dark:text-text-dark text-xl font-bold text-center mb-2">
          {name ? t("pin.enterPin", { name }) : t("roomSetup.overridePinLabel")}
        </Text>

        <View className="flex-row justify-center gap-3 my-6">
          {Array.from({ length: pinLength }).map((_, i) => (
            <View
              key={i}
              style={{ width: 20, height: 20, borderRadius: 10 }}
              className={i < pin.length ? "bg-primary dark:bg-primary-dark" : "bg-border dark:bg-border-dark"}
            />
          ))}
        </View>

        {submitting && <ActivityIndicator color={colors.primary} style={{ marginBottom: 12 }} />}

        {message && (
          <Text
            className={`text-center mb-4 font-medium ${message.tone === "locked" ? "text-warning dark:text-warning-dark" : "text-danger dark:text-danger-dark"}`}
          >
            {message.text}
          </Text>
        )}

        <View
          className="flex-row flex-wrap justify-center self-center"
          style={{ gap: 12, width: 3 * 72 + 2 * 12 }}
        >
          {KEYS.map((key, i) => (
            <TouchableOpacity
              key={i}
              disabled={key === "" || submitting}
              onPress={() => handleKey(key)}
              style={{ width: 72, height: 64, borderRadius: 12 }}
              className={key === "" ? "" : "bg-surface-soft dark:bg-surface-soft-dark items-center justify-center"}
            >
              <Text className="text-text dark:text-text-dark text-2xl font-semibold">{key}</Text>
            </TouchableOpacity>
          ))}
        </View>

        <TouchableOpacity onPress={onCancel} style={{ minHeight: 48 }} className="items-center justify-center mt-4">
          <Text className="text-text-soft dark:text-text-soft-dark font-medium">{t("logout.cancel")}</Text>
        </TouchableOpacity>
      </Animated.View>
    </View>
  );
}
