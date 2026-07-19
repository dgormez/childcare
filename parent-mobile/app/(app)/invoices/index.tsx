import React, { useCallback, useEffect, useRef, useState } from "react";
import { View, Text, FlatList, TouchableOpacity, RefreshControl, ActivityIndicator } from "react-native";
import { useRouter } from "expo-router";
import { useTranslation } from "react-i18next";
import * as Sharing from "expo-sharing";
import * as WebBrowser from "expo-web-browser";
import { Receipt, Clock, AlertTriangle, CheckCircle2, Download, CreditCard } from "lucide-react-native";
import { getInvoices, downloadFamilyInvoicePdf } from "../../../services/invoices";
import { createPaymentLink, getPaymentStatus } from "../../../services/payments";
import { useColors } from "../../../hooks/useColors";
import { ScreenContainer } from "../../../components/ScreenContainer";
import { isFamilyInvoiceEntry } from "../../../types";
import type { ParentInvoiceListEntry, InvoiceStatus } from "../../../types";

// Mirrors invoices/[id].tsx's own poll window exactly (feature 014a FR-010) — this list screen
// pays the family tile's whole group via one representative invoiceId (any grouped invoice
// resolves the combined total, CreatePaymentLinkCommand), so the same bounded-poll approximation
// of "confirming payment" applies here too.
const CONFIRM_POLL_INTERVAL_MS = 2000;
const CONFIRM_POLL_MAX_ATTEMPTS = 10;

type FamilyPaymentAction = "idle" | "creating" | "confirming" | "not_connected" | "error";

function formatCents(cents: number): string {
  return (cents / 100).toLocaleString(undefined, { style: "currency", currency: "EUR" });
}

function periodParts(periodMonth: string): { month: string; year: number } {
  const [year, month] = periodMonth.split("-").map(Number);
  return { month: new Date(year, month - 1, 1).toLocaleDateString(undefined, { month: "long" }), year };
}

// FR-008: plain-language statuses only, each paired with an icon — never color alone
// (design-system.md's Status indicators rule). `draft` never appears here (backend never
// returns it to a parent).
const STATUS_META: Record<
  "sent" | "overdue" | "paid",
  { icon: typeof Clock; colorKey: "warning" | "danger" | "success"; labelKey: string }
> = {
  sent: { icon: Clock, colorKey: "warning", labelKey: "invoices.statusSent" },
  overdue: { icon: AlertTriangle, colorKey: "danger", labelKey: "invoices.statusOverdue" },
  paid: { icon: CheckCircle2, colorKey: "success", labelKey: "invoices.statusPaid" },
};

function statusKey(status: InvoiceStatus, isOverdue: boolean): "sent" | "overdue" | "paid" {
  if (status === "paid") return "paid";
  return isOverdue ? "overdue" : "sent";
}

export default function InvoicesScreen() {
  const { t } = useTranslation();
  const colors = useColors();
  const router = useRouter();

  const [invoices, setInvoices] = useState<ParentInvoiceListEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [unavailable, setUnavailable] = useState(false);
  const [downloadingFamilyId, setDownloadingFamilyId] = useState<string | null>(null);
  const [payingFamilyId, setPayingFamilyId] = useState<string | null>(null);
  const [payAction, setPayAction] = useState<FamilyPaymentAction>("idle");
  const payCancelled = useRef(false);

  const load = useCallback(async () => {
    const result = await getInvoices();
    if (result.status === "unavailable") {
      setUnavailable(true);
    } else {
      setUnavailable(false);
      setInvoices(result.invoices);
    }
  }, []);

  useEffect(() => {
    setLoading(true);
    load().finally(() => setLoading(false));
    return () => {
      payCancelled.current = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const onRefresh = async () => {
    setRefreshing(true);
    await load();
    setRefreshing(false);
  };

  async function handleDownloadFamily(familyGroupId: string) {
    setDownloadingFamilyId(familyGroupId);
    try {
      const file = await downloadFamilyInvoicePdf(familyGroupId);
      if (await Sharing.isAvailableAsync()) {
        await Sharing.shareAsync(file.uri, { UTI: "com.adobe.pdf", mimeType: "application/pdf" });
      }
    } catch {
      // Best-effort — the download button is retryable, mirrors the per-invoice detail screen's
      // own silent-retry shape rather than adding a second error surface to this list screen.
    } finally {
      setDownloadingFamilyId(null);
    }
  }

  // Feature 030 (US3, spec.md FR-009a/Clarifications) — "one payment action covers the whole
  // bundled group" applies to the online PSP path too: any one of the group's invoiceIds resolves
  // the combined total (CreatePaymentLinkCommand) and cascades Paid to every sibling
  // (ProcessPaymentWebhookCommand), so paying via the first child's invoiceId pays the whole
  // family. Mirrors invoices/[id].tsx's handlePayNow, scoped to this list screen's per-tile state.
  async function handlePayFamily(familyGroupId: string, representativeInvoiceId: string) {
    setPayingFamilyId(familyGroupId);
    setPayAction("creating");
    const result = await createPaymentLink(representativeInvoiceId);
    if (payCancelled.current) return;
    if (result.status === "not_connected") {
      setPayAction("not_connected");
      return;
    }
    if (result.status === "error") {
      setPayAction("error");
      return;
    }

    await WebBrowser.openBrowserAsync(result.checkoutUrl);
    if (payCancelled.current) return;
    setPayAction("confirming");

    for (let attempt = 0; attempt < CONFIRM_POLL_MAX_ATTEMPTS; attempt++) {
      if (payCancelled.current) return;
      await new Promise((resolve) => setTimeout(resolve, CONFIRM_POLL_INTERVAL_MS));
      if (payCancelled.current) return;

      const status = await getPaymentStatus(representativeInvoiceId);
      if (payCancelled.current) return;
      if (status?.invoiceStatus === "paid") {
        await load();
        if (payCancelled.current) return;
        setPayAction("idle");
        setPayingFamilyId(null);
        return;
      }
    }

    // Still not confirmed after the poll window — back to idle, "Pay now" stays available so
    // the parent can check again later (the webhook may simply be slow, not failed).
    if (payCancelled.current) return;
    setPayAction("idle");
    setPayingFamilyId(null);
  }

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
        {unavailable && (
          <View className="items-center" style={{ paddingVertical: 48, paddingHorizontal: 24 }}>
            <Receipt color={colors.textSoft} size={28} strokeWidth={2} />
            <Text className="text-text-soft dark:text-text-soft-dark text-sm text-center mt-3">{t("invoices.loadFailed")}</Text>
          </View>
        )}

        {!unavailable && invoices.length === 0 && (
          <View className="items-center" style={{ paddingVertical: 48 }}>
            <Receipt color={colors.textSoft} size={28} strokeWidth={2} />
            <Text className="text-text-soft dark:text-text-soft-dark text-sm mt-3">{t("invoices.noInvoices")}</Text>
          </View>
        )}

        {!unavailable && (
          <FlatList
            data={invoices}
            keyExtractor={(item) => (isFamilyInvoiceEntry(item) ? `family-${item.familyGroupId}` : item.id)}
            contentContainerStyle={{ padding: 16 }}
            refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={colors.primary} />}
            renderItem={({ item }) => {
              const meta = STATUS_META[statusKey(item.status, item.isOverdue)];
              const StatusIcon = meta.icon;

              // Feature 030 (US3) — siblings sharing a FamilyGroupId render as one combined
              // tile (per-child lines, combined total, single download action) instead of one
              // tile each; never navigable to the per-invoice detail screen (no single id).
              if (isFamilyInvoiceEntry(item)) {
                return (
                  <View
                    className="bg-surface dark:bg-surface-dark rounded-xl px-4 mb-3 py-3"
                    style={{ minHeight: 64 }}
                  >
                    {item.children.map((child) => (
                      <Text key={child.childId} className="text-text dark:text-text-dark text-sm" numberOfLines={1}>
                        {t("invoices.familyGroup.perChildLine", { childName: child.childName, amount: formatCents(child.subtotalCents) })}
                      </Text>
                    ))}
                    <View className="flex-row items-center justify-between mt-2">
                      <View>
                        <Text className="text-text-soft dark:text-text-soft-dark text-xs">{t("invoices.familyGroup.combinedTotal")}</Text>
                        <Text className="text-text dark:text-text-dark text-base font-semibold">{formatCents(item.totalCents)}</Text>
                        <View className="flex-row items-center mt-1">
                          <StatusIcon color={colors[meta.colorKey]} size={14} strokeWidth={2} />
                          <Text className="text-text-soft dark:text-text-soft-dark text-xs ml-1">{t(meta.labelKey)}</Text>
                        </View>
                      </View>
                      <View className="flex-row items-center">
                        {item.status === "sent" && (
                          <TouchableOpacity
                            onPress={() => handlePayFamily(item.familyGroupId, item.children[0].invoiceId)}
                            disabled={payingFamilyId === item.familyGroupId && (payAction === "creating" || payAction === "confirming")}
                            accessibilityLabel={t("invoices.payNow")}
                            style={{ minHeight: 48, minWidth: 48, alignItems: "center", justifyContent: "center" }}
                          >
                            {payingFamilyId === item.familyGroupId && (payAction === "creating" || payAction === "confirming") ? (
                              <ActivityIndicator color={colors.primary} />
                            ) : (
                              <CreditCard color={colors.primary} size={20} strokeWidth={2} />
                            )}
                          </TouchableOpacity>
                        )}
                        <TouchableOpacity
                          onPress={() => handleDownloadFamily(item.familyGroupId)}
                          disabled={downloadingFamilyId === item.familyGroupId}
                          accessibilityLabel={t("invoices.downloadPdf")}
                          style={{ minHeight: 48, minWidth: 48, alignItems: "center", justifyContent: "center" }}
                        >
                          {downloadingFamilyId === item.familyGroupId ? (
                            <ActivityIndicator color={colors.primary} />
                          ) : (
                            <Download color={colors.primary} size={20} strokeWidth={2} />
                          )}
                        </TouchableOpacity>
                      </View>
                    </View>
                    {payingFamilyId === item.familyGroupId && payAction === "confirming" && (
                      <Text className="text-text-soft dark:text-text-soft-dark text-xs mt-2">{t("invoices.confirmingPayment")}</Text>
                    )}
                    {payingFamilyId === item.familyGroupId && payAction === "not_connected" && (
                      <Text className="text-text-soft dark:text-text-soft-dark text-xs mt-2">{t("invoices.payNotAvailable")}</Text>
                    )}
                    {payingFamilyId === item.familyGroupId && payAction === "error" && (
                      <Text className="text-danger dark:text-danger-dark text-xs mt-2">{t("invoices.payLinkFailed")}</Text>
                    )}
                  </View>
                );
              }

              return (
                <TouchableOpacity
                  onPress={() => router.push(`/(app)/invoices/${item.id}`)}
                  className="bg-surface dark:bg-surface-dark rounded-xl px-4 mb-3"
                  style={{ minHeight: 64, justifyContent: "center", paddingVertical: 12 }}
                >
                  <View className="flex-row items-center justify-between">
                    <View className="flex-1">
                      <Text className="text-text dark:text-text-dark text-base font-medium" numberOfLines={1}>
                        {item.childName}
                      </Text>
                      <Text className="text-text-soft dark:text-text-soft-dark text-xs mt-1">
                        {t("invoices.period", periodParts(item.periodMonth))}
                      </Text>
                    </View>
                    <View className="items-end">
                      <Text className="text-text dark:text-text-dark text-base font-semibold">
                        {formatCents(item.totalCents)}
                      </Text>
                      <View className="flex-row items-center mt-1">
                        <StatusIcon color={colors[meta.colorKey]} size={14} strokeWidth={2} />
                        <Text className="text-text-soft dark:text-text-soft-dark text-xs ml-1">{t(meta.labelKey)}</Text>
                      </View>
                    </View>
                  </View>
                </TouchableOpacity>
              );
            }}
          />
        )}
      </View>
    </ScreenContainer>
  );
}
