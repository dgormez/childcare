// EAS sets EAS_BUILD_PROFILE during cloud builds. Locally it is undefined (treated as dev).
const isProduction = process.env.EAS_BUILD_PROFILE === "production";

/** @type {import('expo/config').ExpoConfig} */
module.exports = {
  expo: {
    name: "ChildCare Parent",
    slug: "childcare-parent",
    version: "1.0.0",
    orientation: "portrait",
    icon: "./assets/images/icon.png",
    userInterfaceStyle: "automatic",
    newArchEnabled: true,
    splash: {
      image: "./assets/images/splash-icon.png",
      resizeMode: "contain",
      backgroundColor: "#FAF9F6",
    },
    ios: {
      supportsTablet: false,
      bundleIdentifier: "com.dgit.childcareparent",
      infoPlist: {
        ITSAppUsesNonExemptEncryption: false,
        // Allows plain HTTP to reach a local dev server. Omitted in production builds.
        ...(isProduction ? {} : { NSAppTransportSecurity: { NSAllowsArbitraryLoads: true } }),
        // Deep links: childcareparent://  (used by expo-router and the parent-invitation email
        // link — backend/ChildCare.Application/ParentInvitations/ParentLinkBuilder.cs)
        CFBundleURLTypes: [{ CFBundleURLSchemes: ["childcareparent"] }],
      },
    },
    android: {
      package: "com.dgit.childcareparent",
      versionCode: 1,
      softwareKeyboardLayoutMode: "pan",
      adaptiveIcon: {
        foregroundImage: "./assets/images/adaptive-icon.png",
        backgroundColor: "#FAF9F6",
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
      "expo-secure-store",
      "expo-apple-authentication",
      [
        "expo-notifications",
        {
          icon: "./assets/images/icon.png",
          color: "#4F7CAC",
          sounds: [],
        },
      ],
    ],
    web: {
      bundler: "metro",
      output: "static",
    },
    experiments: {
      typedRoutes: true,
    },
    scheme: "childcareparent",
    extra: {
      router: {},
      eas: {
        projectId: "YOUR_EAS_PROJECT_ID",
      },
      // Placeholders per the convention already established in mobile/app.config.js /
      // mobile/eas.json — real values are provisioned per-environment, never committed.
      googleIosClientId: "YOUR_GOOGLE_IOS_CLIENT_ID",
      googleWebClientId: "YOUR_GOOGLE_WEB_CLIENT_ID",
    },
  },
};
