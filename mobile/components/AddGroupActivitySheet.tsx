import React, { useState } from "react";
import { Modal, View, Text, TouchableOpacity, TextInput, ScrollView, ActivityIndicator, Pressable, Image, Alert } from "react-native";
import { useTranslation } from "react-i18next";
import * as ImagePicker from "expo-image-picker";
import { Trees, Palette, Music, BookOpen, PartyPopper, Ellipsis, Camera, ImagePlus, X } from "lucide-react-native";
import { useColors } from "../hooks/useColors";
import { useNetworkStatus } from "../hooks/useNetworkStatus";
import { createGroupActivity } from "../services/groupActivities";
import { enqueuePhotoUpload, uploadPendingPhotos } from "../services/photoUploadQueue";
import type { GroupActivityResponse, GroupActivityType } from "../types";

interface Props {
  visible: boolean;
  onClose: () => void;
  onActivityRecorded: (activity: GroupActivityResponse) => void;
}

const ACTIVITY_TYPES: { type: GroupActivityType; Icon: typeof Trees }[] = [
  { type: "outdoor", Icon: Trees },
  { type: "creative", Icon: Palette },
  { type: "music", Icon: Music },
  { type: "story", Icon: BookOpen },
  { type: "celebration", Icon: PartyPopper },
  { type: "other", Icon: Ellipsis },
];

const MAX_PHOTOS = 10;

/**
 * Bottom sheet mirroring QuickActionSheet.tsx's Modal pattern (design-system.md). Photos are
 * captured here but uploaded asynchronously by photoUploadQueue.ts — save never blocks on
 * resize/upload completing (spec.md UX Requirements' "not a blocking spinner over the whole
 * form").
 */
export function AddGroupActivitySheet({ visible, onClose, onActivityRecorded }: Props) {
  const { t } = useTranslation();
  const colors = useColors();
  const { isConnected } = useNetworkStatus();

  const [selectedType, setSelectedType] = useState<GroupActivityType | null>(null);
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [photos, setPhotos] = useState<string[]>([]);
  const [submitting, setSubmitting] = useState(false);

  const reset = () => {
    setSelectedType(null);
    setTitle("");
    setDescription("");
    setPhotos([]);
  };

  const close = () => {
    reset();
    onClose();
  };

  const handleSelectType = (type: GroupActivityType) => {
    setSelectedType(type);
    setTitle(t(`groupActivities.types.${type}`));
  };

  const addPhotos = async (fromCamera: boolean) => {
    const remaining = MAX_PHOTOS - photos.length;
    if (remaining <= 0) {
      Alert.alert(t("groupActivities.photoLimitTitle"), t("groupActivities.photoLimitMessage"));
      return;
    }

    const permission = fromCamera
      ? await ImagePicker.requestCameraPermissionsAsync()
      : await ImagePicker.requestMediaLibraryPermissionsAsync();
    if (!permission.granted) return;

    const result = fromCamera
      ? await ImagePicker.launchCameraAsync({ quality: 0.9 })
      : await ImagePicker.launchImageLibraryAsync({ quality: 0.9, allowsMultipleSelection: true, selectionLimit: remaining });

    if (result.canceled) return;
    const uris = result.assets.slice(0, remaining).map((a) => a.uri);
    setPhotos((prev) => [...prev, ...uris]);
  };

  const removePhoto = (uri: string) => setPhotos((prev) => prev.filter((p) => p !== uri));

  const handleSave = async () => {
    if (!selectedType || !title.trim()) return;
    setSubmitting(true);
    try {
      const activity = await createGroupActivity(
        { activityType: selectedType, title: title.trim(), description: description.trim() || null, occurredAt: new Date().toISOString() },
        isConnected,
      );

      for (const uri of photos) {
        await enqueuePhotoUpload(activity.id, uri);
      }
      if (photos.length > 0) uploadPendingPhotos(); // fire-and-forget — GroupTimeline shows progress

      onActivityRecorded(activity);
      close();
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Modal transparent visible={visible} animationType="slide" onRequestClose={close}>
      <Pressable className="flex-1 justify-end bg-black/50" onPress={close}>
        <Pressable
          onPress={(e) => e.stopPropagation()}
          className="bg-surface dark:bg-surface-dark rounded-t-xl"
          style={{ maxHeight: "85%" }}
        >
          <View className="items-center pt-3 pb-1">
            <View style={{ width: 40, height: 4, borderRadius: 2 }} className="bg-border dark:bg-border-dark" />
          </View>

          <View className="flex-row items-center justify-between px-4 pt-2 pb-3">
            <Text className="text-text dark:text-text-dark text-lg font-bold">{t("groupActivities.addTitle")}</Text>
            <TouchableOpacity onPress={close} style={{ minWidth: 48, minHeight: 48 }} className="items-center justify-center">
              <X size={24} strokeWidth={2} color={colors.textSoft} />
            </TouchableOpacity>
          </View>

          <ScrollView contentContainerStyle={{ padding: 16 }}>
            {submitting ? (
              <ActivityIndicator size="large" color={colors.primary} style={{ marginVertical: 24 }} />
            ) : selectedType === null ? (
              <View className="flex-row flex-wrap" style={{ gap: 12 }}>
                {ACTIVITY_TYPES.map(({ type, Icon }) => (
                  <TouchableOpacity
                    key={type}
                    onPress={() => handleSelectType(type)}
                    style={{ width: 88, height: 88 }}
                    className="items-center justify-center rounded-xl bg-surface-soft dark:bg-surface-soft-dark active:opacity-60"
                  >
                    <Icon size={24} strokeWidth={2} color={colors.text} />
                    <Text className="text-text dark:text-text-dark text-xs font-medium mt-2 text-center">
                      {t(`groupActivities.types.${type}`)}
                    </Text>
                  </TouchableOpacity>
                ))}
              </View>
            ) : (
              <View>
                <Text className="text-text-soft dark:text-text-soft-dark text-sm mb-1">{t("groupActivities.fields.title")}</Text>
                <TextInput
                  value={title}
                  onChangeText={setTitle}
                  className="bg-surface-soft dark:bg-surface-soft-dark rounded-lg px-3 text-text dark:text-text-dark mb-3"
                  style={{ minHeight: 48 }}
                />

                <Text className="text-text-soft dark:text-text-soft-dark text-sm mb-1">{t("groupActivities.fields.description")}</Text>
                <TextInput
                  multiline
                  value={description}
                  onChangeText={setDescription}
                  className="bg-surface-soft dark:bg-surface-soft-dark rounded-lg p-3 text-text dark:text-text-dark mb-3"
                  style={{ minHeight: 80 }}
                />

                <View className="flex-row flex-wrap mb-2" style={{ gap: 8 }}>
                  {photos.map((uri) => (
                    <View key={uri} style={{ width: 72, height: 72 }}>
                      <Image source={{ uri }} style={{ width: 72, height: 72, borderRadius: 8 }} />
                      <TouchableOpacity
                        onPress={() => removePhoto(uri)}
                        hitSlop={{ top: 12, bottom: 12, left: 12, right: 12 }}
                        style={{ position: "absolute", top: -6, right: -6, minWidth: 24, minHeight: 24 }}
                        className="items-center justify-center rounded-full bg-danger dark:bg-danger-dark"
                      >
                        <X size={14} strokeWidth={2.5} color="white" />
                      </TouchableOpacity>
                    </View>
                  ))}
                </View>

                <View className="flex-row mb-3" style={{ gap: 8 }}>
                  <TouchableOpacity
                    onPress={() => addPhotos(true)}
                    disabled={photos.length >= MAX_PHOTOS}
                    style={{ minHeight: 48, flex: 1 }}
                    className="flex-row items-center justify-center rounded-lg bg-surface-soft dark:bg-surface-soft-dark active:opacity-60"
                  >
                    <Camera size={18} strokeWidth={2} color={colors.text} />
                    <Text className="text-text dark:text-text-dark font-medium ml-2">{t("groupActivities.photoCamera")}</Text>
                  </TouchableOpacity>
                  <TouchableOpacity
                    onPress={() => addPhotos(false)}
                    disabled={photos.length >= MAX_PHOTOS}
                    style={{ minHeight: 48, flex: 1 }}
                    className="flex-row items-center justify-center rounded-lg bg-surface-soft dark:bg-surface-soft-dark active:opacity-60"
                  >
                    <ImagePlus size={18} strokeWidth={2} color={colors.text} />
                    <Text className="text-text dark:text-text-dark font-medium ml-2">{t("groupActivities.photoGallery")}</Text>
                  </TouchableOpacity>
                </View>

                {photos.length > 0 && (
                  <Text className="text-text-soft dark:text-text-soft-dark text-xs mb-3">{t("groupActivities.photoReminder")}</Text>
                )}

                <TouchableOpacity
                  onPress={handleSave}
                  disabled={!title.trim()}
                  style={{ minHeight: 48 }}
                  className={`items-center justify-center rounded-lg ${title.trim() ? "bg-primary dark:bg-primary-dark" : "bg-border dark:bg-border-dark"}`}
                >
                  <Text className="text-white font-semibold">{t("groupActivities.save")}</Text>
                </TouchableOpacity>
              </View>
            )}
          </ScrollView>
        </Pressable>
      </Pressable>
    </Modal>
  );
}
