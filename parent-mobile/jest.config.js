/** @type {import('jest').Config} */
module.exports = {
  preset: "jest-expo",
  setupFilesAfterEnv: ["@testing-library/react-native/matchers"],
  transform: {
    // lucide-react-native ships an ESM (.mjs) entry point that still needs Babel transpilation
    // under Jest (found while wiring this app's icon set — mobile/ doesn't use lucide-react-native
    // in any tested component yet per design-system.md, so this gap was never hit there).
    "\\.m?[jt]sx?$": "<rootDir>/jest-mock-component-transform.js",
  },
  transformIgnorePatterns: [
    "node_modules/(?!(" +
      "expo|expo-router|expo-modules-core|expo-localization|" +
      "expo-notifications|expo-auth-session|expo-apple-authentication|expo-web-browser|" +
      "expo-secure-store|expo-constants|expo-linking|expo-status-bar|" +
      "@expo|react-native|@react-native|nativewind|react-native-reanimated|" +
      "react-native-screens|react-native-safe-area-context|react-native-toast-message|" +
      "react-native-svg|lucide-react-native|" +
      "@react-navigation|zustand" +
    ")/)",
  ],
  moduleNameMapper: {
    "\\.css$": "<rootDir>/__mocks__/fileMock.js",
    "^nativewind$": "<rootDir>/__mocks__/nativewind.js",
  },
};
