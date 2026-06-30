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
  default:     "text-blue-500 font-semibold",
  cancel:      "text-gray-500 dark:text-gray-300",
  destructive: "text-red-500 font-semibold",
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
          className="w-full bg-white dark:bg-gray-800 rounded-2xl overflow-hidden"
          style={{ maxWidth: 400 }}
        >
          {/* Content */}
          <View className="px-5 pt-5 pb-4">
            <Text className="text-gray-900 dark:text-white text-lg font-bold mb-2">{config.title}</Text>
            {!!config.message && (
              <Text className="text-gray-500 dark:text-gray-400 text-sm leading-5">{config.message}</Text>
            )}
          </View>

          {/* Divider */}
          <View className="h-px bg-gray-200 dark:bg-gray-700" />

          {/* Buttons */}
          {config.buttons.map((btn, i) => (
            <React.Fragment key={btn.label}>
              <TouchableOpacity
                onPress={btn.onPress}
                className="px-5 py-4 items-center active:opacity-60"
              >
                <Text className={`text-base ${BUTTON_STYLES[btn.style]}`}>{btn.label}</Text>
              </TouchableOpacity>
              {i < config.buttons.length - 1 && <View className="h-px bg-gray-200 dark:bg-gray-700" />}
            </React.Fragment>
          ))}
        </Pressable>
      </Pressable>
    </Modal>
  );
}
