/** @type {import('jest').Config} */
module.exports = {
  preset: "jest-expo",
  setupFilesAfterEnv: ["@testing-library/react-native/matchers"],
  transform: {
    "\\.[jt]sx?$": "<rootDir>/jest-mock-component-transform.js",
  },
  transformIgnorePatterns: [
    "node_modules/(?!(" +
      "expo|expo-router|expo-modules-core|expo-localization|" +
      "expo-notifications|expo-updates|" +
      "expo-secure-store|expo-sqlite|expo-constants|expo-linking|expo-status-bar|" +
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
