import React, { useCallback, useEffect, useState } from "react";
import { View, Text, FlatList, Image, RefreshControl, ActivityIndicator, Modal, Pressable, TouchableOpacity } from "react-native";
import { useTranslation } from "react-i18next";
import Toast from "react-native-toast-message";
import * as Sharing from "expo-sharing";
import { ImageOff, X, Download } from "lucide-react-native";
import { getGroupActivityGallery, downloadGroupActivityPhotoOriginal } from "../../services/groupActivityGallery";
import { useColors } from "../../hooks/useColors";
import { useIsOffline } from "../../hooks/useIsOffline";
import { ScreenContainer } from "../../components/ScreenContainer";
import type { GalleryItemResponse } from "../../types";

const GRID_COLUMNS = 3;

/**
 * "Galerij" tab (feature 009b, User Story 3) — current calendar month's consented group-activity
 * photos, most recent first. Distinguishes "no consent" from "consent but nothing recorded yet"
 * (spec.md Acceptance Scenario 2) via the response's own `hasConsent` flag, not an empty list.
 */
export default function GalleryScreen() {
  const { t } = useTranslation();
  const colors = useColors();

  const [items, setItems] = useState<GalleryItemResponse[]>([]);
  const [hasConsent, setHasConsent] = useState(true);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState("");

  const [selectedItem, setSelectedItem] = useState<GalleryItemResponse | null>(null);
  const [downloading, setDownloading] = useState(false);
  const isOffline = useIsOffline(selectedItem?.photo.id);

  const load = useCallback(async () => {
    setError("");
    try {
      const gallery = await getGroupActivityGallery();
      setItems(gallery.items);
      setHasConsent(gallery.hasConsent);
    } catch {
      setError(t("gallery.loadFailed"));
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

  async function handleDownload() {
    if (!selectedItem) return;
    setDownloading(true);
    try {
      const file = await downloadGroupActivityPhotoOriginal(selectedItem.photo.id);
      if (await Sharing.isAvailableAsync()) {
        await Sharing.shareAsync(file.uri, { UTI: "public.jpeg", mimeType: "image/jpeg" });
      }
    } catch {
      Toast.show({ type: "error", text1: t("gallery.downloadFailed") });
    } finally {
      setDownloading(false);
    }
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
        {!!error && <Text className="text-danger dark:text-danger-dark text-sm mx-4 mt-4">{error}</Text>}

        {!error && !hasConsent && (
          <View className="items-center" style={{ paddingVertical: 48, paddingHorizontal: 24 }}>
            <ImageOff color={colors.textSoft} size={28} strokeWidth={2} />
            <Text className="text-text-soft dark:text-text-soft-dark text-sm text-center mt-3">
              {t("gallery.noConsent")}
            </Text>
          </View>
        )}

        {!error && hasConsent && items.length === 0 && (
          <View className="items-center" style={{ paddingVertical: 48 }}>
            <Text className="text-text-soft dark:text-text-soft-dark text-sm">{t("gallery.empty")}</Text>
          </View>
        )}

        {!error && hasConsent && items.length > 0 && (
          <FlatList
            data={items}
            numColumns={GRID_COLUMNS}
            keyExtractor={(item) => item.photo.id}
            contentContainerStyle={{ padding: 4 }}
            refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={colors.primary} />}
            renderItem={({ item }) =>
              item.photo.thumbnailDownloadUrl ? (
                <TouchableOpacity
                  testID={`gallery-photo-${item.photo.id}`}
                  accessibilityLabel={item.photo.caption ?? t("gallery.title")}
                  onPress={() => setSelectedItem(item)}
                  style={{ flex: 1 / GRID_COLUMNS, aspectRatio: 1, margin: 4 }}
                >
                  <Image
                    source={{ uri: item.photo.thumbnailDownloadUrl }}
                    style={{ flex: 1, borderRadius: 8, backgroundColor: colors.border }}
                  />
                </TouchableOpacity>
              ) : (
                <View style={{ flex: 1 / GRID_COLUMNS, aspectRatio: 1, margin: 4, borderRadius: 8, backgroundColor: colors.border, alignItems: "center", justifyContent: "center" }}>
                  <ImageOff color={colors.textSoft} size={20} strokeWidth={2} />
                </View>
              )
            }
          />
        )}
      </View>

      <Modal transparent animationType="fade" visible={!!selectedItem} onRequestClose={() => setSelectedItem(null)}>
        <View className="flex-1 bg-black">
          <Pressable
            accessibilityLabel={t("gallery.closeDetail")}
            onPress={() => setSelectedItem(null)}
            className="absolute z-10"
            style={{ top: 48, right: 16, minWidth: 48, minHeight: 48, alignItems: "center", justifyContent: "center" }}
          >
            <X color="#ffffff" size={24} strokeWidth={2} />
          </Pressable>

          {selectedItem?.photo.downloadUrl && (
            <Image
              source={{ uri: selectedItem.photo.downloadUrl }}
              style={{ flex: 1 }}
              resizeMode="contain"
            />
          )}

          {!isOffline && (
            <TouchableOpacity
              onPress={handleDownload}
              disabled={downloading}
              className="flex-row items-center justify-center"
              style={{ minHeight: 48, paddingVertical: 12, paddingBottom: 32 }}
            >
              {downloading ? (
                <ActivityIndicator size="small" color="#ffffff" />
              ) : (
                <>
                  <Download color="#ffffff" size={20} strokeWidth={2} />
                  <Text className="text-white text-sm ml-2">{t("gallery.downloadOriginal")}</Text>
                </>
              )}
            </TouchableOpacity>
          )}
        </View>
      </Modal>
    </ScreenContainer>
  );
}
