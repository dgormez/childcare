import { Tabs } from "expo-router";
import { Text, View, TouchableOpacity } from "react-native";
import { usePathname, useRouter } from "expo-router";
import { useColors } from "../../hooks/useColors";
import { useBreakpoint } from "../../hooks/useBreakpoint";

const NAV_ITEMS = [
  { name: "index",        label: "Today",    icon: "📅", href: "/(tabs)"               },
  { name: "habits",       label: "Habits",   icon: "📋", href: "/(tabs)/habits"        },
  { name: "subscription", label: "Pro",      icon: "⭐", href: "/(tabs)/subscription"  },
  { name: "settings",     label: "Settings", icon: "⚙️", href: "/(tabs)/settings"      },
] as const;

function Sidebar() {
  const pathname   = usePathname();
  const router     = useRouter();
  const colors     = useColors();
  return (
    <View style={{
      width: 220,
      backgroundColor: colors.tabBar,
      borderRightWidth: 1,
      borderRightColor: colors.tabBorder,
      paddingTop: 60,
      paddingHorizontal: 12,
    }}>
      <Text style={{
        color: "#9ca3af",
        fontSize: 11,
        fontWeight: "600",
        textTransform: "uppercase",
        letterSpacing: 1,
        paddingHorizontal: 12,
        marginBottom: 12,
      }}>
        ChildCare
      </Text>

      {NAV_ITEMS.map((item) => {
        const active = item.name === "index"
          ? pathname === "/" || pathname === "/(tabs)"
          : pathname.includes(item.name);
        return (
          <TouchableOpacity
            key={item.name}
            onPress={() => router.push(item.href)}
            style={{
              flexDirection: "row",
              alignItems: "center",
              paddingHorizontal: 12,
              paddingVertical: 12,
              borderRadius: 10,
              marginBottom: 4,
              backgroundColor: active ? "rgba(59, 130, 246, 0.12)" : "transparent",
            }}
          >
            <Text style={{ fontSize: 18, marginRight: 10 }}>{item.icon}</Text>
            <Text style={{
              color: active ? "#3b82f6" : "#6b7280",
              fontWeight: active ? "600" : "normal",
              fontSize: 15,
            }}>
              {item.label}
            </Text>
          </TouchableOpacity>
        );
      })}
    </View>
  );
}

export default function TabsLayout() {
  const { isWide } = useBreakpoint();
  const colors     = useColors();

  return (
    <View style={{ flex: 1, flexDirection: isWide ? "row" : "column" }}>
      {isWide && <Sidebar />}
      <Tabs
        screenOptions={{
          headerShown: false,
          tabBarStyle: isWide
            ? { display: "none" }
            : { backgroundColor: colors.tabBar, borderTopColor: colors.tabBorder },
          tabBarActiveTintColor:   "#3b82f6",
          tabBarInactiveTintColor: "#6b7280",
        }}
      >
        <Tabs.Screen
          name="index"
          options={{
            title:      "Today",
            tabBarIcon: ({ color }) => <Text style={{ color, fontSize: 20 }}>📅</Text>,
          }}
        />
        <Tabs.Screen
          name="habits"
          options={{
            title:      "Habits",
            tabBarIcon: ({ color }) => <Text style={{ color, fontSize: 20 }}>📋</Text>,
          }}
        />
        <Tabs.Screen
          name="subscription"
          options={{
            title:      "Pro",
            tabBarIcon: ({ color }) => <Text style={{ color, fontSize: 20 }}>⭐</Text>,
          }}
        />
        <Tabs.Screen
          name="settings"
          options={{
            title:      "Settings",
            tabBarIcon: ({ color }) => <Text style={{ color, fontSize: 20 }}>⚙️</Text>,
          }}
        />
      </Tabs>
    </View>
  );
}
