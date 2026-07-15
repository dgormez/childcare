import React from "react";
import { Tabs } from "expo-router";
import { useTranslation } from "react-i18next";
import { Home, MessageCircle, Bell, Settings, Images, UtensilsCrossed } from "lucide-react-native";
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
          title: t("home.title"),
          tabBarIcon: ({ color, size }) => <Home color={color} size={size} strokeWidth={2} />,
        }}
      />
      <Tabs.Screen
        name="gallery"
        options={{
          title: t("gallery.title"),
          tabBarIcon: ({ color, size }) => <Images color={color} size={size} strokeWidth={2} />,
        }}
      />
      <Tabs.Screen
        name="menu"
        options={{
          title: t("menu.title"),
          tabBarIcon: ({ color, size }) => <UtensilsCrossed color={color} size={size} strokeWidth={2} />,
        }}
      />
      <Tabs.Screen
        name="messages"
        options={{
          title: t("messages.title"),
          tabBarIcon: ({ color, size }) => <MessageCircle color={color} size={size} strokeWidth={2} />,
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
        name="announcements"
        options={{
          href: null, // reached only via a notification tap, not a tab of its own
        }}
      />
      <Tabs.Screen
        name="requests"
        options={{
          href: null, // reached only via Home's quick actions, not a tab of its own
        }}
      />
      <Tabs.Screen
        name="invoices"
        options={{
          href: null, // reached only via Home's quick actions, not a tab of its own
        }}
      />
      <Tabs.Screen
        name="settings"
        options={{
          title: t("settings.title"),
          tabBarIcon: ({ color, size }) => <Settings color={color} size={size} strokeWidth={2} />,
        }}
      />
    </Tabs>
  );
}
