import { Redirect } from "expo-router";

// Expo Router resolves "/(app)" to this file first; the schedule screen (spec.md's headline
// screen — "where am I working next Wednesday?") is the app's actual home, one tap past login
// (spec.md Success Criteria SC-001), so this immediately hands off to it rather than rendering
// its own content.
export default function AppIndex() {
  return <Redirect href="/(app)/schedule" />;
}
