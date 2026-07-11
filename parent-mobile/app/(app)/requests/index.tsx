import React, { useCallback, useEffect, useState } from "react";
import { View, Text, FlatList, TouchableOpacity, RefreshControl, ActivityIndicator } from "react-native";
import { useTranslation } from "react-i18next";
import dayjs from "dayjs";
import { CalendarOff, CalendarPlus, ArrowLeftRight } from "lucide-react-native";
import { listMyDayReservations, cancelDayReservation } from "../../../services/dayReservations";
import { useColors } from "../../../hooks/useColors";
import { ScreenContainer } from "../../../components/ScreenContainer";
import { ThemedModal, type ModalConfig } from "../../../components/ThemedModal";
import type { DayReservationResponse, DayReservationType } from "../../../types";

const TYPE_ICON: Record<DayReservationType, typeof CalendarOff> = {
  absence: CalendarOff,
  extra: CalendarPlus,
  exchange: ArrowLeftRight,
};

export default function MyRequestsScreen() {
  const { t } = useTranslation();
  const colors = useColors();

  const [reservations, setReservations] = useState<DayReservationResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState("");
  const [modal, setModal] = useState<ModalConfig | null>(null);

  const load = useCallback(async () => {
    setError("");
    try {
      setReservations(await listMyDayReservations());
    } catch {
      setError(t("dayReservations.loadFailed"));
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
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

  const confirmCancel = (reservation: DayReservationResponse) => {
    setModal({
      title: t("dayReservations.cancelConfirmTitle"),
      message: t("dayReservations.cancelConfirmBody"),
      buttons: [
        { label: t("dayReservations.cancelDismiss"), style: "cancel", onPress: () => setModal(null) },
        {
          label: t("dayReservations.cancelConfirm"),
          style: "destructive",
          onPress: async () => {
            setModal(null);
            try {
              await cancelDayReservation(reservation.id);
              await load();
            } catch {
              setError(t("dayReservations.cancelFailed"));
            }
          },
        },
      ],
    });
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
        {!!error && <Text className="text-danger dark:text-danger-dark text-sm mx-4 mt-4">{error}</Text>}

        {reservations.length === 0 && !error && (
          <View className="items-center" style={{ paddingVertical: 48 }}>
            <Text className="text-text-soft dark:text-text-soft-dark text-sm">{t("dayReservations.noRequests")}</Text>
          </View>
        )}

        <FlatList
          data={reservations}
          keyExtractor={(item) => item.id}
          contentContainerStyle={{ padding: 16 }}
          refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={colors.primary} />}
          renderItem={({ item }) => {
            const Icon = TYPE_ICON[item.type];
            return (
              <View
                className="bg-surface dark:bg-surface-dark rounded-xl px-4 mb-3"
                style={{ minHeight: 56, justifyContent: "center", paddingVertical: 12 }}
              >
                <View className="flex-row items-center">
                  <Icon color={colors.textSoft} size={20} strokeWidth={2} />
                  <Text className="text-text dark:text-text-dark text-base font-semibold ml-2 flex-1">
                    {t(`dayReservations.type.${item.type}`)} — {dayjs(item.requestedDate).format("MMM D, YYYY")}
                  </Text>
                </View>
                <Text className="text-text-soft dark:text-text-soft-dark text-sm mt-1">
                  {t(`dayReservations.status.${item.status}`)}
                </Text>
                {item.status === "rejected" && item.directorNotes && (
                  <Text className="text-text-soft dark:text-text-soft-dark text-sm mt-1">{item.directorNotes}</Text>
                )}
                {item.status === "pending" && (
                  <TouchableOpacity onPress={() => confirmCancel(item)} className="mt-2" style={{ minHeight: 48, justifyContent: "center" }}>
                    <Text className="text-danger dark:text-danger-dark text-sm font-semibold">{t("dayReservations.cancel")}</Text>
                  </TouchableOpacity>
                )}
              </View>
            );
          }}
        />

        <ThemedModal config={modal} onDismiss={() => setModal(null)} />
      </View>
    </ScreenContainer>
  );
}
