import React from "react";
import { Modal, View, Text, TouchableOpacity, Pressable } from "react-native";

export interface ModalButton {
  label:   string;
  style:   "default" | "cancel" | "destructive";
  onPress: () => void;
}

export interface ModalConfig {
  title:    string;
  message?: string;
  buttons:  ModalButton[];
}

interface Props {
  config:    ModalConfig | null;
  onDismiss: () => void;
}

const BUTTON_STYLES: Record<ModalButton["style"], string> = {
  // primary-hover, not primary — verified ~5.9:1 contrast as a text color, primary itself is
  // fill-only (~4.36:1, fine for a filled button, not for text on a light background).
  default:     "text-primary-hover dark:text-primary-hover-dark font-semibold",
  cancel:      "text-text-soft dark:text-text-soft-dark",
  destructive: "text-danger dark:text-danger-dark font-semibold",
};

export function ThemedModal({ config, onDismiss }: Props) {
  if (!config) return null;

  return (
    <Modal transparent animationType="fade" visible onRequestClose={onDismiss}>
      <Pressable
        className="flex-1 justify-center items-center bg-black/60 px-8"
        onPress={onDismiss}
      >
        <Pressable
          onPress={(e) => e.stopPropagation()}
          className="w-full bg-surface dark:bg-surface-dark rounded-xl overflow-hidden"
          style={{ maxWidth: 400 }}
        >
          {/* Content */}
          <View className="px-5 pt-5 pb-4">
            <Text className="text-text dark:text-text-dark text-lg font-bold mb-2">{config.title}</Text>
            {!!config.message && (
              <Text className="text-text-soft dark:text-text-soft-dark text-sm leading-5">{config.message}</Text>
            )}
          </View>

          {/* Divider */}
          <View className="h-px bg-border dark:bg-border-dark" />

          {/* Buttons */}
          {config.buttons.map((btn, i) => (
            <React.Fragment key={btn.label}>
              <TouchableOpacity
                onPress={btn.onPress}
                className="px-5 py-4 items-center active:opacity-60"
              >
                <Text className={`text-base ${BUTTON_STYLES[btn.style]}`}>{btn.label}</Text>
              </TouchableOpacity>
              {i < config.buttons.length - 1 && <View className="h-px bg-border dark:bg-border-dark" />}
            </React.Fragment>
          ))}
        </Pressable>
      </Pressable>
    </Modal>
  );
}
