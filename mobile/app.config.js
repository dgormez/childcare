const iosClientId     = process.env.EXPO_PUBLIC_GOOGLE_IOS_CLIENT_ID     ?? "";
const androidClientId = process.env.EXPO_PUBLIC_GOOGLE_ANDROID_CLIENT_ID ?? "";

const iosReverseScheme = iosClientId
  ? `com.googleusercontent.apps.${iosClientId.split(".apps.googleusercontent.com")[0]}`
  : "";

const androidReverseScheme = androidClientId
  ? `com.googleusercontent.apps.${androidClientId.split(".apps.googleusercontent.com")[0]}`
  : "";

// EAS sets EAS_BUILD_PROFILE during cloud builds. Locally it is undefined (treated as dev).
const isProduction = process.env.EAS_BUILD_PROFILE === "production";

/** @type {import('expo/config').ExpoConfig} */
module.exports = {
  expo: {
    name: "ChildCare",
    slug: "childcare",
    version: "1.0.0",
    orientation: "default",
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
      entitlements: {
        "com.apple.developer.applesignin": ["Default"],
      },
      infoPlist: {
        ITSAppUsesNonExemptEncryption: false,
        // Allows plain HTTP to reach a local dev server. Omitted in production builds.
        ...(isProduction ? {} : { NSAppTransportSecurity: { NSAllowsArbitraryLoads: true } }),
        CFBundleURLTypes: [
          // Deep links: childcare://  (used by expo-router + password reset)
          { CFBundleURLSchemes: ["childcare"] },
          // Google OAuth callback scheme
          ...(iosReverseScheme ? [{ CFBundleURLSchemes: [iosReverseScheme] }] : []),
        ],
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
      // Registers the Google OAuth reverse-scheme intent filter so that after
      // the user authenticates in the Chrome Custom Tab, Android routes the
      // redirect back to this app instead of leaving it in the browser.
      // Mirrors what CFBundleURLTypes does for iOS above.
      ...(androidReverseScheme && {
        intentFilters: [
          {
            action: "VIEW",
            data: [{ scheme: androidReverseScheme }],
            category: ["BROWSABLE", "DEFAULT"],
          },
        ],
      }),
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
      "expo-apple-authentication",
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
      "expo-web-browser",
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
