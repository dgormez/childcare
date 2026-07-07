// EAS sets EAS_BUILD_PROFILE during cloud builds. Locally it is undefined (treated as dev).
const isProduction = process.env.EAS_BUILD_PROFILE === "production";

/** @type {import('expo/config').ExpoConfig} */
module.exports = {
  expo: {
    name: "ChildCare",
    slug: "childcare",
    version: "1.0.0",
    orientation: "landscape",
    icon: "./assets/images/icon.png",
    userInterfaceStyle: "automatic",
    newArchEnabled: true,
    splash: {
      image: "./assets/images/splash-icon.png",
      resizeMode: "contain",
      backgroundColor: "#111827",
    },
    ios: {
      supportsTablet: true,
      bundleIdentifier: "com.dgit.childcare",
      infoPlist: {
        ITSAppUsesNonExemptEncryption: false,
        // Allows plain HTTP to reach a local dev server. Omitted in production builds.
        ...(isProduction ? {} : { NSAppTransportSecurity: { NSAllowsArbitraryLoads: true } }),
        // Deep links: childcare://  (used by expo-router)
        CFBundleURLTypes: [{ CFBundleURLSchemes: ["childcare"] }],
      },
    },
    android: {
      package: "com.dgit.childcare",
      versionCode: 1,
      softwareKeyboardLayoutMode: "pan",
      adaptiveIcon: {
        foregroundImage: "./assets/images/adaptive-icon.png",
        backgroundColor: "#111827",
      },
    },
    plugins: [
      [
        "expo-build-properties",
        {
          android: {
            // Allows plain HTTP to reach a local dev server. Disabled in production builds.
            usesCleartextTraffic: !isProduction,
          },
        },
      ],
      "expo-router",
      "expo-sqlite",
      "expo-secure-store",
      [
        "expo-notifications",
        {
          icon: "./assets/images/icon.png",
          color: "#2563eb",
          sounds: [],
        },
      ],
      [
        "@sentry/react-native",
        {
          url: "https://sentry.io/",
          organization: "YOUR_SENTRY_ORG",
          project: "YOUR_SENTRY_PROJECT",
        },
      ],
    ],
    web: {
      bundler: "metro",
      output:  "static",
    },
    experiments: {
      typedRoutes: true,
    },
    scheme: "childcare",
    extra: {
      router: {},
      eas: {
        projectId: "YOUR_EAS_PROJECT_ID",
      },
    },
    runtimeVersion: {
      policy: "sdkVersion",
    },
    updates: {
      url: "https://u.expo.dev/YOUR_EAS_PROJECT_ID",
    },
  },
};
