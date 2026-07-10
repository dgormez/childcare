import React, { useEffect, useState } from "react";
import { View, Text, TextInput, TouchableOpacity, ScrollView, ActivityIndicator } from "react-native";
import { useRouter } from "expo-router";
import { useTranslation } from "react-i18next";
import { apiClient } from "../../../services/apiClient";
import { useColors } from "../../../hooks/useColors";
import type { MessageThreadResponse, ParentChildResponse } from "../../../types";

export default function NewMessageThreadScreen() {
  const { t } = useTranslation();
  const colors = useColors();
  const router = useRouter();

  const [children, setChildren] = useState<ParentChildResponse[]>([]);
  const [childId, setChildId] = useState<string | null>(null);
  const [subject, setSubject] = useState("");
  const [body, setBody] = useState("");
  const [sending, setSending] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    apiClient.GET("/api/parent/children").then((result) => {
      if (result.response.ok) setChildren(result.data as unknown as ParentChildResponse[]);
    }).catch(() => {});
  }, []);

  const canSubmit = subject.trim().length > 0 && body.trim().length > 0 && !sending;

  const handleSubmit = async () => {
    setError("");
    setSending(true);
    try {
      const result = await apiClient.POST("/api/parent/message-threads", {
        body: { childId, subject: subject.trim(), body: body.trim() },
      });
      if (!result.response.ok) throw new Error("failed");
      const created = result.data as unknown as MessageThreadResponse;
      router.replace(`/(app)/messages/${created.id}`);
    } catch {
      setError(t("messages.sendFailed"));
    } finally {
      setSending(false);
    }
  };

  return (
    <ScrollView className="flex-1 bg-background dark:bg-background-dark" contentContainerStyle={{ padding: 16 }}>
      {!!error && <Text className="text-danger dark:text-danger-dark text-sm mb-4">{error}</Text>}

      <Text className="text-text-soft dark:text-text-soft-dark text-sm font-medium mb-1">{t("messages.aboutChild")}</Text>
      <View className="mb-4">
        <TouchableOpacity
          onPress={() => setChildId(null)}
          className={`rounded-lg px-4 mb-2 ${childId === null ? "bg-primary-soft dark:bg-primary-soft-dark" : "bg-surface-soft dark:bg-surface-soft-dark"}`}
          style={{ minHeight: 48, justifyContent: "center" }}
        >
          <Text className="text-text dark:text-text-dark">{t("messages.noChild")}</Text>
        </TouchableOpacity>
        {children.map((child) => (
          <TouchableOpacity
            key={child.id}
            onPress={() => setChildId(child.id)}
            className={`rounded-lg px-4 mb-2 ${childId === child.id ? "bg-primary-soft dark:bg-primary-soft-dark" : "bg-surface-soft dark:bg-surface-soft-dark"}`}
            style={{ minHeight: 48, justifyContent: "center" }}
          >
            <Text className="text-text dark:text-text-dark">{child.firstName} {child.lastName}</Text>
          </TouchableOpacity>
        ))}
      </View>

      <Text className="text-text-soft dark:text-text-soft-dark text-sm font-medium mb-1">{t("messages.subject")}</Text>
      <TextInput
        value={subject}
        onChangeText={setSubject}
        placeholder={t("messages.subjectPlaceholder")}
        placeholderTextColor={colors.placeholder}
        className="bg-surface-soft dark:bg-surface-soft-dark text-text dark:text-text-dark rounded-lg px-4 py-4 mb-4"
      />

      <Text className="text-text-soft dark:text-text-soft-dark text-sm font-medium mb-1">{t("messages.messagePlaceholder")}</Text>
      <TextInput
        value={body}
        onChangeText={setBody}
        placeholder={t("messages.firstMessagePlaceholder")}
        placeholderTextColor={colors.placeholder}
        multiline
        className="bg-surface-soft dark:bg-surface-soft-dark text-text dark:text-text-dark rounded-lg px-4 py-4 mb-6"
        style={{ minHeight: 120, textAlignVertical: "top" }}
      />

      <TouchableOpacity
        onPress={handleSubmit}
        disabled={!canSubmit}
        className={`rounded-lg py-4 items-center ${canSubmit ? "bg-primary dark:bg-primary-dark" : "bg-border dark:bg-border-dark"}`}
      >
        {sending ? <ActivityIndicator color="#fff" /> : <Text className="text-white text-lg font-bold">{t("messages.send")}</Text>}
      </TouchableOpacity>
    </ScrollView>
  );
}
