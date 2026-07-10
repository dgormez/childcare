import React, { useEffect, useState } from "react";
import {
  View, Text, TextInput, TouchableOpacity, FlatList,
  KeyboardAvoidingView, ActivityIndicator,
} from "react-native";
import { useLocalSearchParams } from "expo-router";
import { useTranslation } from "react-i18next";
import dayjs from "dayjs";
import { Send } from "lucide-react-native";
import { apiClient } from "../../../services/apiClient";
import { useColors } from "../../../hooks/useColors";
import { useStore } from "../../../store/useStore";
import type { MessageResponse, MessageThreadResponse } from "../../../types";

export default function MessageThreadScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const { t } = useTranslation();
  const colors = useColors();
  const userId = useStore((s) => s.auth?.userId);

  const [thread, setThread] = useState<MessageThreadResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [body, setBody] = useState("");
  const [sending, setSending] = useState(false);
  const [error, setError] = useState("");

  const load = async () => {
    setError("");
    try {
      const result = await apiClient.GET("/api/parent/message-threads/{id}", { params: { path: { id } } });
      if (!result.response.ok) throw new Error("failed");
      setThread(result.data as unknown as MessageThreadResponse);
    } catch {
      setError(t("messages.loadFailed"));
    }
  };

  useEffect(() => {
    setLoading(true);
    load().finally(() => setLoading(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  const handleSend = async () => {
    if (!body.trim()) return;
    setSending(true);
    setError("");
    try {
      const result = await apiClient.POST("/api/parent/message-threads/{id}/messages", {
        params: { path: { id } },
        body: { body: body.trim() },
      });
      if (!result.response.ok) throw new Error("failed");
      const newMessage = result.data as unknown as MessageResponse;
      setThread((prev) => prev ? { ...prev, messages: [...prev.messages, newMessage] } : prev);
      setBody("");
    } catch {
      setError(t("messages.sendFailed"));
    } finally {
      setSending(false);
    }
  };

  if (loading) {
    return (
      <View style={{ flex: 1, backgroundColor: colors.background, alignItems: "center", justifyContent: "center" }}>
        <ActivityIndicator size="large" color={colors.primary} />
      </View>
    );
  }

  return (
    <KeyboardAvoidingView behavior="padding" className="flex-1 bg-background dark:bg-background-dark">
      {!!error && <Text className="text-danger dark:text-danger-dark text-sm mx-4 mt-2">{error}</Text>}

      <FlatList
        data={thread?.messages ?? []}
        keyExtractor={(item) => item.id}
        contentContainerStyle={{ padding: 16 }}
        renderItem={({ item }) => {
          const isMine = item.senderId === userId;
          return (
            <View
              className={isMine ? "bg-primary dark:bg-primary-dark" : "bg-surface dark:bg-surface-dark"}
              style={{
                borderRadius: 12,
                padding: 12,
                marginBottom: 12,
                maxWidth: "85%",
                alignSelf: isMine ? "flex-end" : "flex-start",
              }}
            >
              {!isMine && (
                <Text className="text-text-soft dark:text-text-soft-dark text-xs font-semibold mb-1">{item.senderName}</Text>
              )}
              <Text className={isMine ? "text-white" : "text-text dark:text-text-dark"}>{item.body}</Text>
              <Text
                className={isMine ? "text-white" : "text-text-soft dark:text-text-soft-dark"}
                style={{ fontSize: 11, marginTop: 4, opacity: 0.8 }}
              >
                {dayjs(item.sentAt).format("MMM D, HH:mm")}
              </Text>
            </View>
          );
        }}
      />

      <View className="flex-row items-end px-4 pb-4 pt-2">
        <TextInput
          value={body}
          onChangeText={setBody}
          placeholder={t("messages.messagePlaceholder")}
          placeholderTextColor={colors.placeholder}
          multiline
          className="flex-1 bg-surface-soft dark:bg-surface-soft-dark text-text dark:text-text-dark rounded-lg px-4 py-3 mr-2"
          style={{ maxHeight: 120 }}
        />
        <TouchableOpacity
          testID="send-message-button"
          onPress={handleSend}
          disabled={!body.trim() || sending}
          className="bg-primary dark:bg-primary-dark items-center justify-center"
          style={{ width: 48, height: 48, borderRadius: 24, opacity: !body.trim() || sending ? 0.5 : 1 }}
        >
          {sending ? <ActivityIndicator color="#fff" size="small" /> : <Send color="#fff" size={20} strokeWidth={2} />}
        </TouchableOpacity>
      </View>
    </KeyboardAvoidingView>
  );
}
