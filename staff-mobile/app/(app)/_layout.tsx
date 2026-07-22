import React from "react";
import { Tabs } from "expo-router";
import { useTranslation } from "react-i18next";
import { CalendarDays, ClipboardList, Bell } from "lucide-react-native";
import { useColors } from "../../hooks/useColors";

export default function AppLayout() {
  const { t } = useTranslation();
  const colors = useColors();

  return (
    <Tabs
      screenOptions={{
        headerStyle: { backgroundColor: colors.surface },
        headerTintColor: colors.text,
        tabBarStyle: { backgroundColor: colors.surface, borderTopColor: colors.border },
        tabBarActiveTintColor: colors.primaryHover,
        tabBarInactiveTintColor: colors.textSoft,
      }}
    >
      <Tabs.Screen
        name="index"
        options={{
          href: null, // redirects straight to schedule/index — never its own tab
        }}
      />
      <Tabs.Screen
        name="schedule/index"
        options={{
          title: t("schedule.title"),
          tabBarIcon: ({ color, size }) => <CalendarDays color={color} size={size} strokeWidth={2} />,
        }}
      />
      <Tabs.Screen
        name="leave-requests/index"
        options={{
          title: t("leaveRequests.title"),
          tabBarIcon: ({ color, size }) => <ClipboardList color={color} size={size} strokeWidth={2} />,
        }}
      />
      <Tabs.Screen
        name="notifications"
        options={{
          title: t("notifications.title"),
          tabBarIcon: ({ color, size }) => <Bell color={color} size={size} strokeWidth={2} />,
        }}
      />
      <Tabs.Screen
        name="report-sick"
        options={{
          href: null, // reached only via the schedule screen's "Ik ben ziek" action
          title: t("reportSick.title"),
        }}
      />
      <Tabs.Screen
        name="leave-requests/new"
        options={{
          href: null, // reached only via the leave-requests screen's "new request" action
          title: t("leaveRequests.newTitle"),
        }}
      />
    </Tabs>
  );
}
