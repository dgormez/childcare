import React, { useEffect } from "react";
import { View, Text, TouchableOpacity, ScrollView, ActivityIndicator } from "react-native";
import { useColors } from "../../hooks/useColors";
import { useSubscription } from "../../hooks/useSubscription";
import dayjs from "dayjs";

export default function SubscriptionScreen() {
  const colors = useColors();
  const { subscription, loading, error, refresh, subscribe, manage } = useSubscription();

  useEffect(() => { refresh(); }, []);

  const isActive = subscription?.isActive ?? false;
  const renewsOn = subscription?.currentPeriodEnd
    ? dayjs(subscription.currentPeriodEnd).format("MMM D, YYYY")
    : null;

  return (
    <ScrollView
      style={{ flex: 1, backgroundColor: colors.background }}
      contentContainerStyle={{ padding: 24, paddingTop: 60 }}
    >
      <Text style={{ fontSize: 28, fontWeight: "700", color: colors.text, marginBottom: 8 }}>
        Pro Plan
      </Text>
      <Text style={{ fontSize: 15, color: colors.secondaryText, marginBottom: 32 }}>
        Unlock unlimited habits and premium features.
      </Text>

      {/* Status card */}
      <View style={{
        backgroundColor: colors.card,
        borderRadius: 16,
        padding: 20,
        marginBottom: 24,
        borderWidth: 1,
        borderColor: isActive ? "#22c55e" : colors.border,
      }}>
        <View style={{ flexDirection: "row", alignItems: "center", marginBottom: 8 }}>
          <View style={{
            width: 10,
            height: 10,
            borderRadius: 5,
            backgroundColor: isActive ? "#22c55e" : "#9ca3af",
            marginRight: 8,
          }} />
          <Text style={{ color: colors.text, fontWeight: "600", fontSize: 16 }}>
            {isActive ? "Active" : (subscription ? subscription.status : "No subscription")}
          </Text>
        </View>

        {renewsOn && (
          <Text style={{ color: colors.secondaryText, fontSize: 14 }}>
            {subscription?.status === "Canceled" ? "Access until" : "Renews"} {renewsOn}
          </Text>
        )}
      </View>

      {error && (
        <Text style={{ color: "#ef4444", marginBottom: 16, fontSize: 14 }}>{error}</Text>
      )}

      {loading ? (
        <ActivityIndicator color="#3b82f6" style={{ marginVertical: 16 }} />
      ) : isActive ? (
        <TouchableOpacity
          onPress={manage}
          style={{
            backgroundColor: colors.card,
            borderRadius: 12,
            padding: 16,
            alignItems: "center",
            borderWidth: 1,
            borderColor: colors.border,
          }}
        >
          <Text style={{ color: colors.text, fontWeight: "600", fontSize: 16 }}>
            Manage Subscription
          </Text>
          <Text style={{ color: colors.secondaryText, fontSize: 13, marginTop: 4 }}>
            Cancel, update payment, or view invoices
          </Text>
        </TouchableOpacity>
      ) : (
        <TouchableOpacity
          onPress={subscribe}
          style={{
            backgroundColor: "#3b82f6",
            borderRadius: 12,
            padding: 18,
            alignItems: "center",
          }}
        >
          <Text style={{ color: "#fff", fontWeight: "700", fontSize: 17 }}>
            Subscribe to Pro
          </Text>
          <Text style={{ color: "rgba(255,255,255,0.75)", fontSize: 13, marginTop: 4 }}>
            Billed monthly via Stripe
          </Text>
        </TouchableOpacity>
      )}
    </ScrollView>
  );
}
