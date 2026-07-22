import React, { useCallback, useEffect, useState } from "react";
import { View, Text, FlatList, TouchableOpacity, RefreshControl, ActivityIndicator } from "react-native";
import { useRouter } from "expo-router";
import { useTranslation } from "react-i18next";
import { ClipboardList, Plus } from "lucide-react-native";
import dayjs from "dayjs";
import { getMyLeaveRequests } from "../../../services/leaveRequests";
import { ScreenContainer } from "../../../components/ScreenContainer";
import { useColors } from "../../../hooks/useColors";
import type { StaffLeaveRequestResponse, StaffLeaveRequestStatus } from "../../../types";

const STATUS_COLOR: Record<StaffLeaveRequestStatus, string> = {
  pending: "text-text-soft dark:text-text-soft-dark",
  approved: "text-success dark:text-success-dark",
  rejected: "text-danger dark:text-danger-dark",
};

/** FR-012: staff member's own leave-request history with status (pending/approved/rejected). */
export default function LeaveRequestsScreen() {
  const { t } = useTranslation();
  const colors = useColors();
  const router = useRouter();

  const [requests, setRequests] = useState<StaffLeaveRequestResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState(false);

  const load = useCallback(async () => {
    const result = await getMyLeaveRequests();
    if (result === null) {
      setError(true);
      return;
    }
    setError(false);
    setRequests(result);
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
      <View className="flex-1 bg-background dark:bg-background-dark">
        <TouchableOpacity
          onPress={() => router.push("/(app)/leave-requests/new")}
          className="bg-primary dark:bg-primary-dark rounded-lg py-3 mx-4 mt-4 items-center flex-row justify-center gap-2"
          style={{ minHeight: 48 }}
          testID="new-leave-request-button"
        >
          <Plus color="#fff" size={18} strokeWidth={2} />
          <Text className="text-white text-base font-semibold">{t("leaveRequests.newAction")}</Text>
        </TouchableOpacity>

        {error && <Text className="text-danger dark:text-danger-dark text-sm mx-4 mt-4">{t("leaveRequests.loadFailed")}</Text>}

        {!error && requests.length === 0 && (
          <View className="items-center" style={{ paddingVertical: 48 }}>
            <ClipboardList color={colors.textSoft} size={28} strokeWidth={2} />
            <Text className="text-text-soft dark:text-text-soft-dark text-sm mt-3">{t("leaveRequests.empty")}</Text>
          </View>
        )}

        <FlatList
          data={requests}
          keyExtractor={(item) => item.id}
          contentContainerStyle={{ padding: 16 }}
          refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={colors.primary} />}
          renderItem={({ item }) => (
            <View className="bg-surface dark:bg-surface-dark rounded-xl px-4 py-3 mb-3" testID={`leave-request-${item.id}`}>
              <View className="flex-row justify-between items-start">
                <Text className="text-text dark:text-text-dark text-base font-medium">{t(`leaveRequests.type.${item.type}`)}</Text>
                <Text className={`text-sm font-semibold ${STATUS_COLOR[item.status]}`}>{t(`leaveRequests.status.${item.status}`)}</Text>
              </View>
              <Text className="text-text-soft dark:text-text-soft-dark text-sm mt-1">
                {item.dateFrom === item.dateTo
                  ? dayjs(item.dateFrom).format("D MMM YYYY")
                  : `${dayjs(item.dateFrom).format("D MMM")} – ${dayjs(item.dateTo).format("D MMM YYYY")}`}
              </Text>
              {item.notes && <Text className="text-text-soft dark:text-text-soft-dark text-sm mt-1">{item.notes}</Text>}
            </View>
          )}
        />
      </View>
    </ScreenContainer>
  );
}
